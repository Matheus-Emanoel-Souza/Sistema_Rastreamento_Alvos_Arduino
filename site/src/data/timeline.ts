export interface TimelineEntry {
  title: string
  description: string
}

// Extraído de Docs/Projeto/CONTEXTO_PROJETO.md §2.4 (como o app evoluiu).
export const timeline: TimelineEntry[] = [
  {
    title: 'Base inicial',
    description: 'Aplicativo único com radar, comunicação serial, torres e simulação.',
  },
  {
    title: 'Instalador Windows',
    description: 'Setup.exe self-contained via Inno Setup, launcher avulso, tratamento global de erros.',
  },
  {
    title: 'Fundação multiusuário',
    description:
      'Login, 3 perfis, hash de senha, permissões, i18n (pt-BR/en-US), tema claro/escuro, painel principal com auditoria.',
  },
  {
    title: 'Configurações do Arduino',
    description:
      'Detecção do arduino-cli, compilação de sketch assíncrona/cancelável, monitor serial, primeiros testes automatizados (21 testes xUnit).',
  },
  {
    title: 'Painel personalizável',
    description: 'Cards arrastáveis/redimensionáveis, layout responsivo persistido por usuário.',
  },
  {
    title: 'Consolidação de branches',
    description: 'Sistema, TESTE e homologação mescladas em main, que passa a ser o branch canônico.',
  },
  {
    title: 'Zonas mortas',
    description: 'Áreas que bloqueiam seleção de torre/disparo, criadas por clique/arraste no radar.',
  },
  {
    title: 'Objetos detectados',
    description: 'Tela de tabela real com exportação/importação em CSV, XML e PDF.',
  },
  {
    title: 'Chamado de Ajuda',
    description: 'Formulário de abertura de ticket de suporte acessível pela barra superior.',
  },
  {
    title: 'Levantamento de engenharia',
    description: 'Diagramas de classes/pacotes, DER, requisitos funcionais e não funcionais, matriz de rastreabilidade.',
  },
  {
    title: 'Visão Computacional (em andamento)',
    description: 'Módulo isolado de captura + detecção YOLO/ONNX, branch tratamento-de-imagens, ainda não mesclado em main.',
  },
]
