# Projeto Físico — Índice

Documentação do hardware do RadarTorres: modelos de impressão 3D, peças montadas e eletrônica.

## Impressao_3D/

Modelos `.stl` (placas de impressão, montantes, berços, suportes — incluindo revisões `v2`) e
visualizações `.html` das torres montadas.

## Pecas/

- [`ESPECIFICACOES_MECANICAS.md`](Pecas/ESPECIFICACOES_MECANICAS.md) — especificações mecânicas
  das peças (furos, encaixes, medidas de motor).
- `Maquete_Conceitual_RadarTorres.html` — maquete conceitual navegável.
- Imagens de referência: visão explodida da montagem, desenho técnico frontal, detalhes de torre.

## Eletronica/

- [`Diagrama_Eletrico.md`](Eletronica/Diagrama_Eletrico.md) — componentes e pinagem real do
  Arduino MEGA 2560 (fonte mais atual sobre contagem de sensores/motores/torres).
- `Diagrama_Eletrico_RadarTorres.puml` — fonte do diagrama elétrico.
- Datasheets/imagens de referência dos motores (28BYJ-48, SG90).

## Ver também

- [`Software/Qualidade/REGISTRO_GERAL.md`](../Software/Qualidade/REGISTRO_GERAL.md) — índice
  geral de toda a documentação do projeto.
- [`Software/Arquitetura/ARQUITETURA_SISTEMA.md`](../Software/Arquitetura/ARQUITETURA_SISTEMA.md)
  — seção 7, diagrama de arquitetura geral (PC + Arduino + sensores + câmeras + torres).
