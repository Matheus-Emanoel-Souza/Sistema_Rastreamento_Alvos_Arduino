# Especificações Mecânicas — Estrutura Física do RadarTorres

Este documento reúne as especificações técnicas, a lista de componentes e as notas
de montagem da estrutura mecânica impressa em 3D (torre/canhão), modelada nos
arquivos `.stl` e `.html` desta mesma pasta (`Docs/Projeção 3D/`).

## 1. Elementos de fixação (padrão do projeto)

Todos os pontos de fixação parafusada da estrutura mecânica devem seguir o
padrão abaixo. Qualquer peça nova ou revisão de peça existente precisa ser
furada/projetada para esse padrão, salvo exceção justificada e registrada
neste documento.

| Elemento  | Norma    | Medida |
|-----------|----------|--------|
| Parafusos | DIN 965  | M3     |
| Porcas    | DIN 934  | M3     |

- **DIN 965**: parafuso cabeça escareada (chata) fenda cruzada (Phillips/Pozidriv).
- **DIN 934**: porca sextavada padrão.

## 2. Lista de componentes (peças impressas)

| Arquivo | Peça | Elementos de fixação |
|---|---|---|
| `01_montante_motor_tilt.stl` | Montante do motor de tilt | Parafusos DIN 965 M3 / Porcas DIN 934 M3 |
| `02_montante_apoio_pivo.stl` | Montante de apoio do pivô | Parafusos DIN 965 M3 / Porcas DIN 934 M3 |
| `03_berco_tilt_U.stl` | Berço do tilt em U | Parafusos DIN 965 M3 / Porcas DIN 934 M3 |
| `05_Plataforma_giratória.stl` | Plataforma giratória | Parafusos DIN 965 M3 / Porcas DIN 934 M3 |
| `06_Montante do motor do tilt .stl` | Montante do motor do tilt (revisão) | Parafusos DIN 965 M3 / Porcas DIN 934 M3 |
| `07_Montante_do_pivô(Com rolamento).stl` | Montante do pivô, com rolamento | Parafusos DIN 965 M3 / Porcas DIN 934 M3 |
| `09_Berço do tilt em U(Para Rolamento).stl` | Berço do tilt em U, para rolamento | Parafusos DIN 965 M3 / Porcas DIN 934 M3 |

A referência visual da montagem completa está em `montagem_explodida.png`, e os
modelos navegáveis de conjunto em `torre_canhao_v2.html` e
`torre_central_com_rolamento_e_fiacao.html`.

## 3. Notas de montagem

- Usar sempre parafuso DIN 965 M3 combinado com porca DIN 934 M3 nos pontos de
  junção entre peças impressas (montantes, berços, plataforma).
- Furos de passagem nas peças devem ser dimensionados para parafuso M3
  (diâmetro de folga), e os encaixes de porca (quando embutidos na peça)
  devem ser dimensionados para porca sextavada M3 (DIN 934).
- Ao revisar ou criar uma nova peça, manter a mesma padronização de fixação
  descrita na seção 1, para permitir intercambialidade de parafusaria entre
  as peças do conjunto.
