# Tecnica/

Referência técnica interna: arquitetura, protocolo de comunicação com o Arduino, modelo de
dados, algoritmo de seleção de torre.

- [[ARQUITETURA]] — decisões arquiteturais e diagramas (camadas, concorrência, fluxos de dados).
- [[DOCUMENTACAO_TECNICA]] — referência classe a classe/serviço a serviço, limitações e bugs
  conhecidos.
- [[COMUNICACAO_ARDUINO]] — protocolo serial PC ↔ Arduino completo.
- [[ALGORITMO_SELECAO_TORRE]] — fundamentação matemática do radar e da seleção de torres.
- [[CONTROLADOR_NEUROEVOLUTIVO]] — rede neural + algoritmo genético que refina a seleção de
  torre e a decisão de disparo, e sua persistência de treinamento.
- [[DETECCAO_VISAO_YOLO]] — papel do modelo YOLO na detecção por câmera: o que ele faz e não
  faz, como a confiança chega até a decisão de disparo, e por que o YOLO26n foi escolhido.
- [[MODELO_DADOS]] — schema das tabelas SQLite, relacionamentos.

Ver também: [[Índice]] (raiz do vault), [[Guia_da_Documentacao|Docs]].
