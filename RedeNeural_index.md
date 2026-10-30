Funcionalidade: índice temático dos documentos sobre o controlador híbrido (heurística + rede neural evoluída) e a detecção por visão computacional.

# RedeNeural — Índice

Controlador de decisão (seleção de torre e disparo) e o módulo de percepção que alimenta a
decisão com confiança de identificação.

- [[CONTROLADOR_NEUROEVOLUTIVO]] — rede neural *feedforward* + algoritmo genético que refina a
  seleção de torre e a decisão de disparo; arquitetura, treinamento e persistência.
- [[ALGORITMO_SELECAO_TORRE]] — fundamentação matemática da heurística de seleção de torre que o
  controlador híbrido refina.
- [[DETECCAO_VISAO_YOLO]] — papel do modelo YOLO26n na detecção por câmera: o que ele faz e não
  faz, como a confiança chega até a decisão de disparo, e por que essa versão foi escolhida.
- [[Como_a_IA_funciona_no_RadarTorres]] — a mesma arquitetura explicada em linguagem acessível,
  para quem não é da área técnica.
- [[ApoioDeApresentacao]] — detalhamento de apoio à defesa do TCC: tipos de mutação em algoritmos
  genéticos e por que a gaussiana foi escolhida; biblioteca de neuroevolução em avaliação.

Ver também: [[Estrutura_index]] (arquitetura de software onde este controlador se encaixa,
protocolo serial que leva a decisão até o Arduino) e [[Artigo_index]] (decisões pendentes sobre
mudanças nesta lógica, registradas em `Melhoria_de_artigo`).
