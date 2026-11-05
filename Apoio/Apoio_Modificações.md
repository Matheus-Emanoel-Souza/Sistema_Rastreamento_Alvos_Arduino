Funcionalidade: lista as próximas modificações planejadas para o projeto.

# Próximas Modificações

Anotações de pontos a revisar/implementar em futuras sessões. Atualizar conforme itens forem resolvidos ou surgirem novos.

## Pendente

  - [ ] Usar a rede neural por código fonte ou por biblioteca ? 

- [ ] Remover Claude Code dos contribuidores do repositório

- [ ] Testar a detecção por câmera na máquina real após a migração para o modelo YOLO26n
  (`OnnxYoloDetectionService`/`ComputerVision/Models/YoloModel/yolo26n.onnx`) — confirmar que a
  detecção continua reconhecendo os perfis de alvo cadastrados (cor/formato/fotos) com o novo
  modelo (entrada 640×640, saída NMS-free `output0 [1,300,6]`), e remedir o custo de inferência
  por quadro citado em `AlvoVisualService.ObterCandidatos` (estava em ~480 ms com o tiny-YOLOv3
  anterior, em 416×416; não remedido desde a troca). Ver [[DETECCAO_VISAO_YOLO]].

- [ ] Montagem física do protótipo (hardware real: servos, sensor de distância, torres).
- [ ] Ajustar medidas do motor 28BYJ-48 no modelo 3D do copo de rolamento
  (`06_copo_rolamento_motor.stl`) — os furos de fixação não coincidiram com os furos do motor na
  peça impressa; conferir distância entre furos e diâmetro, corrigir e reimprimir.
- [ ] Testar com mais câmeras — validar detecção, estimativa de distância e calibração dos
  limiares com webcams de modelos, resoluções e campos de visão diferentes.
- [ ] Revisar o custo da própria instrumentação de desempenho (gravação do `.log` por execução e
  acumulação de estatísticas por rodada de detecção) — confirmar se o monitoramento está afetando
  o desempenho do software.
- [ ] Revisar a eficiência das opções salvas em "Configurações" (tamanho/modo de abertura da
  janela, idioma, tema, câmera automática, contorno da detecção, alerta sonoro, relatório de
  desempenho), persistidas em `%LocalAppData%\RadarTorres\configuracoes-app.json`.

## Documentos com flag `alteração_pendente`

Marcados com `Status: 🚩 alteração_pendente` no topo do arquivo — ainda vão ser alterados pelo
autor.

- [[Diagrama_de_Pacotes]]
- [[UML_RadarTorres]] — Seção 5 (Diagrama de Classes).
- [[Modelo_Banco_de_Dados]] — inclui o DER.
- [[Casos_de_Uso]]


