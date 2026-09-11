export type TechCategory = 'Linguagens' | 'Frameworks' | 'Inteligência Artificial' | 'Hardware'

export interface Technology {
  name: string
  category: TechCategory
  icon: string
  description: string
  usage: string
}

export const technologies: Technology[] = [
  // Linguagens
  {
    name: 'C#',
    category: 'Linguagens',
    icon: '🟣',
    description: 'Linguagem principal do aplicativo desktop, C# 13 sobre .NET 9.',
    usage: 'Toda a aplicação RadarTorres.App: ViewModels, Services, Models.',
  },
  {
    name: 'C/C++ (Arduino)',
    category: 'Linguagens',
    icon: '🔧',
    description: 'Firmware embarcado que lê os sensores e fala com o app via serial.',
    usage: 'Arduino/ArduinoSimulation.ino — protocolo texto simples (ANGLE/DIST).',
  },
  {
    name: 'HTML / CSS / JavaScript',
    category: 'Linguagens',
    icon: '🌐',
    description: 'Base da camada web usada neste site de documentação.',
    usage: 'Este site (React + TypeScript), hospedado no GitHub Pages.',
  },

  // Frameworks
  {
    name: '.NET 9',
    category: 'Frameworks',
    icon: '⚙️',
    description: 'Runtime e SDK usados para compilar e publicar o aplicativo.',
    usage: 'Build self-contained do instalador (Setup.exe), sem dependências externas.',
  },
  {
    name: 'WPF',
    category: 'Frameworks',
    icon: '🖼️',
    description: 'Windows Presentation Foundation — interface gráfica com data-binding real.',
    usage: 'Radar circular, painéis, telas de Monitoramento e Configurações do Arduino.',
  },

  // Inteligência Artificial
  {
    name: 'YOLO',
    category: 'Inteligência Artificial',
    icon: '🎯',
    description: 'Modelo de detecção de objetos em tempo real.',
    usage: 'Módulo de Visão Computacional (branch tratamento-de-imagens) — identifica objetos nas câmeras.',
  },
  {
    name: 'ONNX Runtime',
    category: 'Inteligência Artificial',
    icon: '🧠',
    description: 'Motor de inferência que executa o modelo YOLO exportado em ONNX.',
    usage: 'YoloOnnxObjectDetector — uma sessão de inferência por câmera.',
  },
  {
    name: 'OpenCV (OpenCvSharp)',
    category: 'Inteligência Artificial',
    icon: '📷',
    description: 'Captura e pré-processamento de frames de vídeo.',
    usage: 'WebcamCaptureService — captura via webcam USB antes da inferência.',
  },

  // Hardware
  {
    name: 'Arduino',
    category: 'Hardware',
    icon: '🔌',
    description: 'Placa microcontroladora responsável pelos sensores de detecção.',
    usage: 'Envia leituras de ângulo/distância via USB serial para o aplicativo.',
  },
  {
    name: 'Sensores',
    category: 'Hardware',
    icon: '📡',
    description: 'Sensores de distância/proximidade ao redor da base.',
    usage: 'Geram as leituras convertidas em posição cartesiana no radar.',
  },
  {
    name: 'Câmeras',
    category: 'Hardware',
    icon: '🎥',
    description: 'Até 4 câmeras, uma por pipeline de visão computacional independente.',
    usage: 'Fonte de frames para o módulo de Visão Computacional (em desenvolvimento).',
  },
]

export const techCategories: TechCategory[] = [
  'Linguagens',
  'Frameworks',
  'Inteligência Artificial',
  'Hardware',
]
