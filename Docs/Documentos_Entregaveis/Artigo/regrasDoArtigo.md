Funcionalidade: regras de formatação obrigatórias do artigo do TCC, verificadas contra o estado atual de `Artigo.tex`.

# Regras do artigo — RadarTorres

Regras que `Artigo.tex` precisa respeitar antes da entrega. Cada regra tem o status real
verificado contra o `.tex`/`.pdf` atual, não a intenção.

## 1. Máximo de 20 páginas

**Status: ⚠️ violada, mas melhorando.** `Artigo.pdf` compilado hoje (30/10/2026) tem **27 páginas**
— 7 acima do limite (era 30 antes da rodada de cortes de hoje).

Cortes já aplicados:
- [x] Seção "Análise experimental de hiperparâmetros" -- 4 parágrafos longos condensados em 1.
- [x] Parágrafo "Impacto de acrescentar uma entrada" -- 4 impactos resumidos em lista enxuta.
- [x] "Modelo de dados" -- 3 figuras de página cheia reduzidas a 1; prosa de relacionamento FK a FK
  e o histórico do modelo CSV antigo removidos (já cobertos em `Modelo_Banco_de_Dados.md`).

Ainda pendente (candidatos restantes):
- [ ] Seções de investigação da hesitação no gatilho (certeza estática, penalização de recusa,
  comparação com `RedePura`) têm sobreposição narrativa; são a contribuição mais original do
  artigo, então condensar com cuidado, não cortar o achado.
- [ ] Conclusão não menciona a investigação de hesitação/recusa de disparo (mais rica que o achado
  da certeza estática que ela cita) -- atualizar, não só cortar.
- [ ] Verificar se o diagrama de pacotes pode ir para apêndice ou ter o tamanho reduzido.

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
