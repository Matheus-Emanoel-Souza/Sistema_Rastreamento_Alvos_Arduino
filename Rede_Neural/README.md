# Rede Neural — Índice

Documentação do controlador neuroevolutivo (heurística + rede neural evoluída por NEAT/SharpNEAT)
e dos algoritmos de visão/decisão usados pelo RadarTorres.

## Arquitetura/

- [`CONTROLADOR_NEUROEVOLUTIVO.md`](Arquitetura/CONTROLADOR_NEUROEVOLUTIVO.md) — arquitetura da
  rede, treinamento, persistência e o anexo em linguagem acessível (como a IA funciona, proposta
  de ajuste de firmware, rede embutida no Arduino).
- `Explicacao_IA_Neuroevolutiva.tex`/`.pdf` — versão formal (LaTeX) do mesmo tema, para entrega
  acadêmica.

## Algoritmos/

- [`ALGORITMO_SELECAO_TORRE.md`](Algoritmos/ALGORITMO_SELECAO_TORRE.md) — heurística matemática de
  seleção de torre em produção (base que o controlador neuroevolutivo refina).
- [`DETECCAO_VISAO_YOLO.md`](Algoritmos/DETECCAO_VISAO_YOLO.md) — detecção por câmera (YOLO26n) e
  classificação de alvo por cor/formato/tamanho.

## Treinamento/

Reservada para documentação futura específica de experimentos/execução de treinamento (hoje o
conteúdo de treinamento vive dentro de `CONTROLADOR_NEUROEVOLUTIVO.md`, seção 5).

## Ver também

- [`Software/Qualidade/REGISTRO_GERAL.md`](../Software/Qualidade/REGISTRO_GERAL.md) — índice geral
  de toda a documentação do projeto.
- [`Software/Arquitetura/ARQUITETURA_SISTEMA.md`](../Software/Arquitetura/ARQUITETURA_SISTEMA.md)
  — como o controlador se encaixa nas camadas do sistema.
