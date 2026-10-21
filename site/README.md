# Site RadarTorres

Site oficial do projeto (documentação técnica, apresentação do TCC e portfólio),
publicado via **GitHub Pages**. Stack: React 19 + TypeScript + Tailwind CSS v4 + Vite.

## Desenvolvimento

```bash
cd site
npm install
npm run dev       # http://localhost:5173
```

## Build de produção

```bash
npm run build      # gera site/dist
npm run preview    # serve o build gerado, localmente
```

## Publicar uma nova versão

Não é manual: basta dar `git push` de qualquer alteração dentro de `site/` para a branch
`main`. O workflow `.github/workflows/deploy.yml` builda e publica automaticamente no
GitHub Pages a cada push.

**Configuração única, feita pelo GitHub (uma vez só):** em *Settings → Pages* do
repositório, mudar "Source" para **GitHub Actions**.

URL publicada: `https://matheus-emanoel-souza.github.io/Sistema_Rastreamento_Alvos_Arduino/`

## Estrutura

```
src/
├── components/   TechCard, Timeline, Diagram, DocumentationCard, radar/RadarSimulation
├── pages/        as 9 páginas do site
├── data/         conteúdo estruturado (tecnologias, documentação, testes, timeline)
├── styles/       (reservado — hoje o tema vive em src/index.css)
└── assets/       images/ videos/ diagrams/ — pasta da Galeria, ainda vazia
```

## Convenções

- Roteamento com `HashRouter` (react-router-dom) — funciona em qualquer subpath do GitHub
  Pages sem configuração extra (sem risco de 404 em link direto ou refresh).
- A página **Documentação** só linka para os arquivos reais em `Docs/` na raiz do
  repositório (via URL do GitHub) — nada é duplicado dentro de `site/`.
- Pasta chamada `site/`, não `docs/`, de propósito: uma pasta `docs/` colidiria
  case-insensitive com `Docs/` (já existente na raiz do repo) no Windows.
