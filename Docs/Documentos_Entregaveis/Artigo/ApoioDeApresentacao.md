Funcionalidade: material de apoio técnico aprofundado para a defesa do TCC — conteúdo detalhado demais para o corpo do artigo, mas útil para responder perguntas da banca.

# Apoio de apresentação — RadarTorres

Este documento guarda o detalhamento técnico que foi retirado do corpo do `Artigo.tex` para
manter o artigo objetivo, mas que é útil ter à mão na apresentação/defesa.

## Tipos de mutação em algoritmos genéticos e por que o RadarTorres usa gaussiana

Mutação é o operador do algoritmo genético que introduz variação nova na população, perturbando genes
de um filho já formado por *crossover*. Os tipos mais comuns, com genomas de números reais (como o do
RadarTorres, um vetor de 82 pesos/vieses de ponto flutuante):

- **Mutação gaussiana (a usada no RadarTorres)** — cada gene tem uma probabilidade fixa (`TaxaMutacao`,
  8% no RadarTorres) de receber um ruído somado ao seu valor atual, amostrado de uma distribuição normal
  com média 0 e desvio padrão fixo (`ForcaMutacao`, 0,5 no RadarTorres). Implementada em
  `AlgoritmoGenetico.cs` via transformação de Box-Muller (`AmostraGaussiana()`). Efeito prático: a
  maioria das perturbações é pequena (perto da média), com perturbações grandes raras — a cauda da
  normal.
- **Mutação uniforme** — substitui o gene por um valor sorteado uniformemente dentro de um intervalo
  fixo (ex.: $[-1, 1]$), descartando o valor anterior por completo. Mais agressiva: não preserva nada do
  que o gene já tinha aprendido.
- **Mutação não-uniforme / polinomial** — como a gaussiana, soma ruído ao valor atual, mas com a
  magnitude do ruído diminuindo ao longo das gerações (ou perto dos limites do espaço de busca) — início
  do treino explora mais, final refina mais.
- **Mutação de Cauchy** — como a gaussiana, mas amostrando de uma distribuição de Cauchy em vez de
  normal: cauda muito mais pesada, ou seja, saltos grandes ocasionais são bem mais prováveis. Usada
  quando se quer escapar de ótimos locais com mais frequência, à custa de estabilidade.
- **Mutação bit-flip** — só se aplica a genomas binários (0/1), não a genomas de ponto flutuante como o
  do RadarTorres: inverte o bit com uma certa probabilidade.

### Por que gaussiana foi a escolha do RadarTorres

1. **Preserva o que já foi aprendido.** Como a mutação soma ruído ao valor existente (em vez de
   substituí-lo), a maioria das mutações é um ajuste fino, não um reset do gene — importante porque o
   genoma já carrega comportamento útil vindo das gerações anteriores e do *crossover*.
2. **Desvio padrão controla diretamente o quão "forte" a busca é**, um único hiperparâmetro
   (`ForcaMutacao`) fácil de calibrar experimentalmente.
3. **É a escolha padrão da literatura de algoritmos genéticos com representação de ponto flutuante** —
   não houve comparação experimental formal contra Cauchy/polinomial dentro deste projeto; a escolha foi
   de engenharia (robustez e simplicidade de implementação), não resultado de um experimento A/B. Se
   a banca perguntar "por que gaussiana e não outra", a resposta honesta é essa: é a convenção mais
   usada e mais simples de implementar/calibrar corretamente, não uma escolha validada empiricamente
   contra as alternativas no contexto específico do RadarTorres.
4. **Outras mutações são tecnicamente possíveis** de adotar no `AlgoritmoGenetico.cs` sem mudar o resto
   da arquitetura (o genoma continua sendo um vetor de `double`) — troca de operador, não reescrita do
   algoritmo.

## Biblioteca de neuroevolução em avaliação

Nenhuma biblioteca de neuroevolução ou de aprendizado de máquina está referenciada hoje no projeto para
o algoritmo genético (`RadarTorres.App.csproj` só traz `Microsoft.ML.OnnxRuntime`, usado exclusivamente
para rodar o modelo de visão computacional YOLO26n — não tem relação com o treinamento da rede do
controlador). O algoritmo genético e a rede neural feedforward continuam implementados do zero em
`AlgoritmoGenetico.cs`/`RedeNeural.cs`. Caso uma biblioteca de neuroevolução venha a ser adotada,
atualizar esta seção com nome, versão e o que mudaria na implementação atual.
