Funcionalidade: regras de formatação obrigatórias do artigo do TCC, verificadas contra o estado atual de `Artigo.tex`.

# Regras do artigo — RadarTorres

Regras que `Artigo.tex` precisa respeitar antes da entrega. Cada regra tem o status real
verificado contra o `.tex`/`.pdf` atual, não a intenção.

## 1. Máximo de 20 páginas

**Status: ⚠️ violada.** `Artigo.pdf` compilado hoje (30/10/2026) tem **30 páginas** — 10 acima do
limite.

Candidatos a corte/condensação, do mais ao menos custoso:
- Seção "Análise experimental de hiperparâmetros" tem discussão extensa (3 parágrafos longos)
  sobre o efeito do endurecimento do ambiente; pode condensar sem perder a tabela nem a conclusão.
- Parágrafo "Impacto de acrescentar uma entrada" lista 4 impactos em detalhe (quase uma página) —
  candidato a resumir em uma lista mais compacta.
- Seções de investigação da hesitação no gatilho (certeza estática, penalização de recusa,
  comparação com `RedePura`) têm sobreposição narrativa; são a contribuição mais original do
  artigo, então condensar com cuidado, não cortar o achado.
- Verificar se alguma figura grande (DER, diagrama de pacotes) pode ir para apêndice ou ter o
  tamanho reduzido, já que ocupam página cheia hoje (ver regra 2).

## 2. Imagens padronizadas

**Status: ⚠️ violada.** `Artigo.tex` hoje mistura convenções de tamanho entre figuras
(`\includegraphics`):

| Figura | Convenção atual |
|---|---|
| `Fluxo_Operacional.png` | `height=0.82\textheight,width=\textwidth,keepaspectratio` |
| `Diagrama_de_Pacotes.png` | `width=\textwidth` |
| `DER_Usuarios_Modo_Preferencias.png` | `width=0.85\textwidth` |
| `DER_Cameras_Objetos_Detectados.png` | `width=\textwidth` |
| `DER_Tabelas_Treinamento.png` | `width=\textwidth` |

Padrão a adotar (pendente de confirmação do autor): todas as figuras de corpo (exceto o logotipo
da capa) usam `width=\textwidth` sozinho, sem `height`/`keepaspectratio` misto, para manter
legenda e proporção consistentes entre si. `Fluxo_Operacional.png` e `DER_Usuarios_Modo_Preferencias.png`
precisam ser ajustadas para esse padrão (ou o padrão escolhido precisa ser outro, formato de
largura fixa em cm, por exemplo).

## Ver também

- [[Melhoria_de_artigo]] — checklist geral de revisão do artigo.
- [[ApoioDeApresentacao]] — material de apoio retirado do corpo do artigo para manter o texto
  objetivo (relevante para a regra 1).
