export interface DocumentationLink {
  title: string
  description: string
  href: string
  kind: 'md' | 'pdf' | 'folder'
}

export interface DocumentationSection {
  title: string
  items: DocumentationLink[]
}

const REPO_BLOB = 'https://github.com/Matheus-Emanoel-Souza/Sistema_Rastreamento_Alvos_Arduino/blob/main'
const REPO_TREE = 'https://github.com/Matheus-Emanoel-Souza/Sistema_Rastreamento_Alvos_Arduino/tree/main'

/** Converte um link "blob" do GitHub (página HTML) no arquivo bruto, embutível em <object>/<iframe>. */
export function toRawUrl(blobUrl: string): string {
  return blobUrl
    .replace('https://github.com/', 'https://raw.githubusercontent.com/')
    .replace('/blob/', '/')
}

// Todos os links apontam para os arquivos reais já existentes no repositório —
// nada é duplicado dentro do site, só referenciado.
export const documentationSections: DocumentationSection[] = [
  {
    title: 'Arquitetura',
    items: [
      {
        title: 'Decisões arquiteturais',
        description: 'Racional das escolhas de arquitetura (WPF, MVVM, camadas).',
        href: `${REPO_BLOB}/Docs/Tecnica/ARQUITETURA.md`,
        kind: 'md',
      },
      {
        title: 'Documentação técnica',
        description: 'Referência de cada classe/serviço, limitações e bugs conhecidos.',
        href: `${REPO_BLOB}/Docs/Tecnica/DOCUMENTACAO_TECNICA.md`,
        kind: 'md',
      },
    ],
  },
  {
    title: 'Requisitos',
    items: [
      {
        title: 'Requisitos do sistema (PDF entregável)',
        description: 'Requisitos funcionais e não funcionais consolidados.',
        href: `${REPO_BLOB}/Docs/Documentos_Entregaveis/Requisitos_de_Sistema/Requisitos_RadarTorres.pdf`,
        kind: 'pdf',
      },
      {
        title: 'Pasta Requisitos_de_Sistema',
        description: 'Fonte LaTeX, matriz de rastreabilidade e conteúdo completo.',
        href: `${REPO_TREE}/Docs/Documentos_Entregaveis/Requisitos_de_Sistema`,
        kind: 'folder',
      },
    ],
  },
  {
    title: 'UML',
    items: [
      {
        title: 'Casos de uso (PDF entregável)',
        description: 'Documento de casos de uso com diagrama.',
        href: `${REPO_BLOB}/Docs/Documentos_Entregaveis/UML/Documento_Casos_de_Uso_RadarTorres.pdf`,
        kind: 'pdf',
      },
      {
        title: 'Pasta UML',
        description: 'Diagramas de casos de uso, pacotes, implantação e decisões arquiteturais.',
        href: `${REPO_TREE}/Docs/Documentos_Entregaveis/UML`,
        kind: 'folder',
      },
      {
        title: 'Diagrama de Classes',
        description: 'Diagrama de classes do sistema (PlantUML + imagem).',
        href: `${REPO_TREE}/Docs/Documentos_Entregaveis/Diagrama_de_Classes`,
        kind: 'folder',
      },
    ],
  },
  {
    title: 'Banco de Dados',
    items: [
      {
        title: 'Modelo de Banco de Dados (PDF entregável)',
        description: 'Modelo lógico do banco de dados do sistema.',
        href: `${REPO_BLOB}/Docs/Documentos_Entregaveis/Banco_de_Dados/Modelo_Banco_Dados_RadarTorres.pdf`,
        kind: 'pdf',
      },
      {
        title: 'Modelo de dados (técnico)',
        description: 'Schema das tabelas CSV, relacionamentos e plano de migração para SQL.',
        href: `${REPO_BLOB}/Docs/Tecnica/MODELO_DADOS.md`,
        kind: 'md',
      },
    ],
  },
  {
    title: 'Artigo',
    items: [
      {
        title: 'Artigo do TCC (PDF)',
        description: 'Artigo acadêmico do Trabalho de Conclusão de Curso.',
        href: `${REPO_BLOB}/Docs/Documentos_Entregaveis/Artigo/Artigo.pdf`,
        kind: 'pdf',
      },
    ],
  },
  {
    title: 'Diagramas',
    items: [
      {
        title: 'Comunicação com o Arduino',
        description: 'Protocolo serial completo (ANGLE/DIST) entre app e firmware.',
        href: `${REPO_BLOB}/Docs/Tecnica/COMUNICACAO_ARDUINO.md`,
        kind: 'md',
      },
      {
        title: 'Algoritmo de seleção de torre',
        description: 'Matemática do radar e da seleção automática de torres.',
        href: `${REPO_BLOB}/Docs/Tecnica/ALGORITMO_SELECAO_TORRE.md`,
        kind: 'md',
      },
    ],
  },
  {
    title: 'Documentação Técnica',
    items: [
      {
        title: 'Visão Computacional',
        description: 'Módulo de tratamento de imagens e detecção com YOLO/ONNX.',
        href: `${REPO_BLOB}/Docs/Tecnica/VISAO_COMPUTACIONAL.md`,
        kind: 'md',
      },
      {
        title: 'Contexto do projeto',
        description: 'Resumo do projeto pensado para retomada rápida por qualquer pessoa/IA.',
        href: `${REPO_BLOB}/Docs/Projeto/CONTEXTO_PROJETO.md`,
        kind: 'md',
      },
      {
        title: 'Fundação multiusuário (Etapa 1)',
        description: 'Arquitetura de login, perfis, permissões e navegação.',
        href: `${REPO_BLOB}/Docs/Projeto/ETAPA1_FUNDACAO.md`,
        kind: 'md',
      },
    ],
  },
]
