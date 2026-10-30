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

## Documentos com flag `alteração_pendente`

Marcados com `Status: 🚩 alteração_pendente` no topo do arquivo — ainda vão ser alterados pelo
autor.

- [[Diagrama_de_Pacotes]]
- [[UML_RadarTorres]] — Seção 5 (Diagrama de Classes).
- [[Modelo_Banco_de_Dados]] — inclui o DER.
- [[Casos_de_Uso]]


