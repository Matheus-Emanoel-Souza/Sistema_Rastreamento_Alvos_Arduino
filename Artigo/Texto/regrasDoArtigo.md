Funcionalidade: regras de formatação obrigatórias do artigo do TCC, verificadas contra o estado atual de `Artigo.tex`.

# Regras do artigo — RadarTorres

Regras que `Artigo.tex` precisa respeitar antes da entrega. Cada regra tem o status real
verificado contra o `.tex`/`.pdf` atual, não a intenção.

## 1. Máximo de 20 páginas

**Status: ✅ cumprida.** `Artigo.pdf` compilado em 09/11/2026 tem **17 páginas** (eram 27).
A reescrita da revisão de 09/11 condensou o resumo (~170 palavras), resumiu os requisitos,
converteu as regras heurísticas em tabela e substituiu a narrativa experimental por uma seção
de Resultados e Discussão com a comparação pareada.

## 2. Imagens e tabelas padronizadas

**Status: ✅ cumprida.**

- Tabelas são todas tabelas LaTeX reais (`booktabs`, legenda acima), nenhuma é imagem. As antigas
  `Tabela1_Fitness.png` e `Tabela2_Ambiente.png` não são mais usadas.
- Figuras em português ("Figura"/"Tabela", via `babel`), com legenda abaixo e fonte "(dos autores)".
- Diagramas próprios (implantação e fluxo operacional) desenhados em TikZ, vetoriais.
- Figuras e tabelas usam posição `[H]` (pacote `float`) para ficarem dentro da seção a que
  pertencem — pedido do orientador para o modelo de dados.

## 3. Compilação

`pdflatex` → `bibtex` → `pdflatex` ×2, a partir desta pasta (as imagens vêm de `../../assets/`).
Requer os pacotes `abntex2cite`, `tikz`, `booktabs`, `tabularx`, `float`, `caption` e `babel`
(português); o MiKTeX instala sob demanda.

## Ver também

- [[Melhoria_de_artigo]] — checklist geral de revisão do artigo.
- [[ApoioDeApresentacao]] — material de apoio retirado do corpo do artigo.
