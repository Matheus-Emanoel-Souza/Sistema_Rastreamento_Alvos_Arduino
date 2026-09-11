export type TestStatus = 'Concluído' | 'Em desenvolvimento' | 'Planejado'

export interface SoftwareTest {
  test: string
  status: TestStatus
}

// Status real, tirado de Docs/Projeto/CONTEXTO_PROJETO.md §3 (estado atual do projeto).
export const softwareTests: SoftwareTest[] = [
  { test: 'Comunicação com Arduino (serial)', status: 'Concluído' },
  { test: 'Modo de simulação (sem hardware)', status: 'Concluído' },
  { test: 'Interface WPF (radar, painéis, temas)', status: 'Concluído' },
  { test: 'Multiusuário (login, perfis, permissões)', status: 'Concluído' },
  { test: 'Testes automatizados (xUnit — 21/21)', status: 'Concluído' },
  { test: 'Captura de câmeras', status: 'Em desenvolvimento' },
  { test: 'Detecção com YOLO/ONNX', status: 'Em desenvolvimento' },
]

export interface FutureMetric {
  metric: string
  description: string
}

export const futureMetrics: FutureMetric[] = [
  { metric: 'FPS', description: 'Quadros processados por segundo em cada pipeline de câmera.' },
  {
    metric: 'Tempo de processamento',
    description: 'Latência entre captura do frame e resultado de detecção.',
  },
  {
    metric: 'Quantidade de câmeras',
    description: 'Escalabilidade do sistema com até 4 câmeras simultâneas.',
  },
  {
    metric: 'Precisão da detecção',
    description: 'Acurácia do modelo YOLO nas classes de objeto relevantes.',
  },
]
