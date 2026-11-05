Funcionalidade: documenta o módulo de detecção por visão computacional baseado em YOLO (modelo, versão, motivo da escolha).

# Detecção por Visão Computacional (YOLO) no RadarTorres

> Explica o papel real do modelo YOLO dentro do pipeline do RadarTorres: o que ele faz, o que
> NÃO faz, como a confiança da câmera chega até a decisão de disparo, e por que o projeto está
> no modelo **YOLO26n** (Ultralytics) em vez de versões anteriores.

## 1. O que o YOLO faz no projeto — e o que não faz

O YOLO roda dentro de `OnnxYoloDetectionService` (`IObjectDetectionService`), sobre o modelo
`yolo26n.onnx`, local, sem nenhum serviço externo. A cada quadro de câmera avaliado, ele faz
**uma única coisa**: devolve uma lista de caixas candidatas (`VisionDetection`), cada uma com
posição/tamanho em pixels e uma confiança (0–1), para objetos de qualquer uma das 80 classes do
dataset COCO (pessoa, bola, garrafa, cadeira, etc.).

Não é tão amplo quanto "identifica os alvos", nem tão restrito quanto "filtra a imagem":

- **Não filtra a imagem** no sentido de limpar ruído, realçar contraste ou qualquer
  pré-processamento clássico de imagem. O único "pré-processamento" é geométrico: o quadro é
  redimensionado para 640×640 e convertido de BGR (como a câmera entrega) para RGB normalizado
  0–1, porque é esse o formato que a rede espera como entrada (`images`, tensor `[1,3,640,640]`).
- **Não identifica *o* alvo cadastrado.** O YOLO26n é um detector genérico treinado no COCO — ele
  não sabe nada sobre o perfil de alvo que o usuário cadastrou (cor, formato, fotos de
  referência). A classe que ele atribui a cada caixa (ex.: "person", "sports ball") é **ignorada
  por todo o resto do pipeline** (ver comentário em `AlvoVisualCandidato.cs`). Ele serve só para
  *propor regiões* onde "pode ter algum objeto ali".
- Quem decide se uma dessas caixas (ou uma mancha de cor, ver abaixo) É o alvo procurado é o
  `AlvoVisualService`, comparando o recorte da caixa com as fotos de referência do
  `PerfilAlvo` ativo (cor cadastrada + similaridade de histograma de matiz/saturação). O YOLO é
  uma das DUAS fontes de candidatos que esse serviço avalia — a outra é segmentação pura por cor
  (`PropostasPorCor`), que roda primeiro por ser muito mais barata e resolve a maioria dos casos
  (o alvo é cadastrado JÁ com uma cor). O YOLO só entra quando a cor não encontra nada.

Resumindo o pipeline de identificação:

```
quadro da câmera
      │
      ▼
OnnxYoloDetectionService.Detect()  ──► caixas candidatas genéricas (80 classes COCO, score)
      │                                 (classe COCO é descartada)
      ▼
AlvoVisualService.AvaliarFrame()
      │  compara cada candidato (do YOLO OU da segmentação por cor) com as fotos do
      │  PerfilAlvo ativo → similaridade de histograma matiz/saturação
      ▼
AlvoVisualResultado (a caixa escolhida, se alguma bateu o limiar) + distância estimada
```

## 2. Como a distância é estimada (sem profundidade/LIDAR)

Com a caixa final escolhida, `AlvoVisualService.EstimarDistanciaMetros` calcula a distância por
geometria de câmera (modelo de câmera pinhole), usando o tamanho FÍSICO real do objeto cadastrado
no perfil (`PerfilAlvo.MaiorDimensaoCm`, em centímetros) contra o tamanho em PIXELS da caixa
detectada e o campo de visão horizontal da câmera (`VisionSettings.CameraFovHorizontalGraus`):

```
distância (cm) = (tamanho_real_cm × largura_imagem_px) / (2 × tamanho_aparente_px × tan(FOV/2))
```

Isso é inteiramente do lado da câmera/visão — não tem nada a ver com o sensor de
distância/ângulo do Arduino, descrito a seguir.

## 3. "Depois de identificar, como ela passa as coordenadas pro Arduino?" — ela não passa

Esse é o ponto mais importante para não confundir a arquitetura: **o YOLO não gera as
coordenadas enviadas ao Arduino, e a direção real do dado é o INVERSO do que a pergunta sugere.**

- O **Arduino tem seu próprio sensor de distância/ângulo** (fora do escopo deste documento — ver
  `COMUNICACAO_ARDUINO.md`) e é ele quem **envia** leituras (`SensorReading`: ângulo, distância,
  id do alvo) pela porta serial PARA o aplicativo, não o contrário.
- `TargetTrackingService.ProcessReading` converte essas leituras polares em X/Y cartesiano e
  mantém a lista de `Target` ativos no radar. **A posição de cada alvo vem do Arduino, nunca do
  YOLO.**
- O papel da câmera/YOLO nesse fluxo é só alimentar `Target.Confidence`: o evento
  `IOcorrenciaDeteccaoService.ConfiancaAtualizada` (disparado a partir do resultado do
  `AlvoVisualService`, por quadrante de câmera) é escutado por `MainViewModel`, que atualiza a
  confiança de todo alvo ativo naquele quadrante.
- Na hora de decidir o acionamento, `FireControlService.Authorize` verifica (entre outras
  regras) se `Target.Confidence` está acima do limiar configurado — **se a câmera/YOLO não
  confirmou o alvo com confiança suficiente, o disparo é bloqueado**, mesmo que a posição (vinda
  do Arduino) esteja perfeita.
- Se autorizado, o comando que volta para o Arduino via `SendCommandAsync` é só
  `FIRE;TOWER=<id>;TARGET=<id>` (`SerialProtocolParser.BuildFire`) — **dois números inteiros (qual
  torre, qual alvo), não coordenadas**. A torre física já sabe apontar para o alvo que lhe foi
  atribuído por outros meios (mecânica/firmware do próprio RadarTorres); o PC não manda X/Y pela
  serial.

Em resumo: **Arduino → PC = posição (ângulo/distância) do sensor físico. PC → Arduino = qual
torre deve disparar contra qual alvo (confirmação demonstrativa), uma vez que a câmera/YOLO
confirmou a identidade do alvo com confiança suficiente.** O YOLO participa da decisão
("autorizo ou não o disparo"), mas não da geolocalização do alvo nem do conteúdo do comando
enviado.

## 4. Por que o NMS-free importa

Detectores clássicos (YOLOv3 até YOLOv8/v9) preveem **várias caixas sobrepostas para o mesmo
objeto** e dependem de uma etapa separada de pós-processamento, o *Non-Maximum Suppression*
(NMS), para eliminar as duplicatas e manter só a melhor caixa por objeto. Isso tem três custos:

1. **Custo de implementação**: alguém precisa escrever (e manter) o algoritmo de NMS fora do
   modelo — exatamente o trabalho que o `tiny-yolov3-11.onnx` anterior evitava ao já trazer o NMS
   embutido no próprio grafo ONNX (saídas `yolonms_layer_1*`), uma solução específica daquele
   modelo do ONNX Model Zoo, não padrão em modelos YOLO mais novos exportados "crus".
2. **Custo de inferência**: NMS é um algoritmo O(n²) sobre as caixas candidatas por quadro —
   relevante aqui porque a detecção roda localmente, por CPU, em tempo real a cada quadro de
   câmera, sem GPU dedicada.
3. **Qualidade**: thresholds de NMS mal calibrados podem suprimir objetos legítimos muito próximos
   (dois alvos lado a lado) ou deixar duplicatas passar.

O **YOLO26** resolve isso na arquitetura do próprio modelo (cabeça de predição *one-to-one*, em
vez de *one-to-many* + NMS): cada objeto real já gera **uma única** predição, então as até 300
linhas da saída (`output0`, formato `[1,300,6]` = x1,y1,x2,y2,score,classe) já vêm deduplicadas.
Não existe etapa de NMS para implementar nem para custear em tempo de CPU — o código em
`OnnxYoloDetectionService.Detect` só filtra por confiança mínima, nada mais.

## 5. Por que o YOLO26n prevalece sobre as versões anteriores

O projeto usava `tiny-yolov3-11.onnx` (2018, ONNX Model Zoo) até esta migração. A troca para
YOLO26n (variante *nano* do YOLO26, Ultralytics, lançado em janeiro/2026) se justifica por:

| Critério | tiny-YOLOv3 (anterior) | YOLO26n (atual) |
|---|---|---|
| Ano / maturidade da arquitetura | 2018 | 2026 |
| NMS | Embutido no ONNX do Model Zoo (solução pontual daquele export) | Nativamente NMS-free (arquitetura one-to-one) |
| Precisão (mAP no COCO) | Baixa — modelo pequeno, desenhado para hardware limitado de 2018 | Bem maior nos mesmos tamanhos de modelo; ganha 1,6–2,8 pontos de AP sobre o YOLO11 nos benchmarks da Ultralytics |
| Velocidade em CPU | Referência de então | Até 43% mais rápido que o YOLO11 em CPU, segundo a Ultralytics |
| Ecossistema/manutenção | Descontinuado, só o `.onnx` estático do Model Zoo | Ativamente mantido (Ultralytics), exportação ONNX de primeira classe |
| Reentreinável nas classes do `PerfilAlvo` | Caro/pouco documentado | Fluxo de fine-tuning simples (`yolo train ...`), caso o projeto queira treinar nas classes próprias no futuro |

Antes de chegar ao YOLO26, outras opções mais recentes que o tiny-YOLOv3 foram consideradas:

- **YOLOv8** — o que o professor recomendou inicialmente ("pelo menos a v8"); estável e muito
  documentado, mas já não é mais o estado da arte da própria Ultralytics.
- **YOLOv9 / YOLOv10** — YOLOv10 (Tsinghua) foi pioneiro em NMS-free, mas hoje (com exportação
  `nms=True`/`nms=False` disponível também no YOLO11/YOLO26) essa vantagem deixou de ser
  exclusiva dele.
- **YOLO11** — sucessor direto do v8 pela própria Ultralytics; teria sido uma escolha sólida, mas
  o YOLO26 (mesma empresa, lançado depois) já o supera em precisão E velocidade de CPU, mantendo
  o mesmo fluxo de exportação.

Dado que o RadarTorres roda a detecção localmente, por CPU, em tempo real, dentro de um
aplicativo desktop sem GPU dedicada garantida, **velocidade em CPU + ausência de NMS manual +
melhor precisão no mesmo orçamento de computação** tornam o YOLO26n (variante nano, menor e mais
rápida da família) a escolha mais alinhada ao que o projeto precisa — não só "a versão mais
nova por ser mais nova".

## 6. Onde isso vive no código

- `src/RadarTorres.App/Services/OnnxYoloDetectionService.cs` — carrega `yolo26n.onnx`, monta o
  tensor de entrada `[1,3,640,640]`, lê a saída `output0 [1,300,6]` e filtra por confiança (sem
  NMS).
- `src/RadarTorres.App/ComputerVision/Models/YoloModel/yolo26n.onnx` — o modelo em si (export
  oficial Ultralytics, ~9,5 MB).
- `src/RadarTorres.App/Services/AlvoVisualService.cs` — decide, entre os candidatos do YOLO e da
  segmentação por cor, qual É o alvo do perfil ativo; estima a distância por geometria de câmera.
- `src/RadarTorres.App/Services/TargetTrackingService.cs` — ponto onde a posição (vinda do
  Arduino, não do YOLO) é processada.
- `src/RadarTorres.App/Services/FireControlService.cs` — ponto onde `Target.Confidence`
  (alimentada pelo YOLO/câmera) entra na autorização de disparo, e onde o comando
  `FIRE;TOWER=x;TARGET=y` é montado para voltar ao Arduino.
