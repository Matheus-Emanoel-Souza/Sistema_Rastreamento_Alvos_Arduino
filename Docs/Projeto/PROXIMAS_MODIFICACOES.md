Funcionalidade: lista as próximas modificações planejadas para o projeto.

# Próximas Modificações

Anotações de pontos a revisar/implementar em futuras sessões. Atualizar conforme itens forem resolvidos ou surgirem novos.

## Pendente

- [ ] Remover Claude Code dos contribuidores do repositório
- [ ] Usar app Wokwi para ligações elétricas e gerar imagem do circuito
- [ ] Testar a detecção por câmera na máquina real após a migração para o modelo YOLO26n
  (`OnnxYoloDetectionService`/`ComputerVision/Models/YoloModel/yolo26n.onnx`) — confirmar que a
  detecção continua reconhecendo os perfis de alvo cadastrados (cor/formato/fotos) com o novo
  modelo (entrada 640×640, saída NMS-free `output0 [1,300,6]`), e remedir o custo de inferência
  por quadro citado em `AlvoVisualService.ObterCandidatos` (estava em ~480 ms com o tiny-YOLOv3
  anterior, em 416×416; não remedido desde a troca). Ver [[DETECCAO_VISAO_YOLO]].

## Em avaliação

- [ ]

## Decisões

- IDE escolhida: Arduino IDE 2.x. Interface moderna, autocomplete, debugger integrado, board manager melhor. Alternativa avaliada: PlatformIO (VS Code) — melhor pra projeto grande/versionamento de libs, mas curva de aprendizado maior; não necessário por enquanto.

## Feito

- [x] Instalador Arduino IDE (`instalar_arduino_ide.bat`) — winget com fallback download direto.
- [x] Modelo de Banco de Dados documentado (coluna "Persistência" já removida, DER com as 11
  tabelas reais) — ver [[Modelo_Banco_de_Dados]].
- [x] Diagrama de implantação e diagrama de classes criados — ver [[Diagrama_de_Implantacao]] e
  [[UML_RadarTorres]] (Seção 5, inclui a discussão sobre máquina de estados).
- [x] Diagrama dos requisitos por usuário/ação — ver [[Casos_de_Uso]] e
  [[Matriz_de_Rastreabilidade]].

## Referência — ferramentas de diagrama UML

Consolidado de `Notas_de_Apoio.md` (mesclado aqui em 2026-10-26, pois os itens acima já estavam
resolvidos e o arquivo só tinha essa lista de ferramentas como conteúdo não-tarefa):

- draw.io / diagrams.net — gratuita, offline, formas UML prontas. Escolha padrão usada no projeto
  (ex.: `Diagrama_de_Classes_RadarTorres.drawio`).
- StarUML — ferramenta UML dedicada, bom suporte a diagrama de classes, exporta código.
- Visual Paradigm — suíte UML completa, mais pesada; existe edição community gratuita.
- Lucidchart — baseado em navegador, bons templates UML, exige conta.
- PlantUML — baseado em texto (sem drag-drop), mas rápido e versionável junto do código — usado
  em vários diagramas desta pasta (`.puml`).

## Ver também

- [[Diagrama_Eletrico]] — ligações elétricas mencionadas no item "Usar app Wokwi".
- [[COMUNICACAO_ARDUINO]] — contexto da configuração de ambiente Arduino.
