import PageSection from '../components/layout/PageSection'
import Diagram from '../components/Diagram'

const futureFields = [
  { label: 'Objeto identificado', status: 'disponível' },
  { label: 'Confiança', status: 'disponível' },
  { label: 'Bounding box', status: 'disponível' },
  { label: 'Posição', status: 'em desenvolvimento' },
  { label: 'Profundidade', status: 'em desenvolvimento' },
  { label: 'Velocidade', status: 'em desenvolvimento' },
]

export default function VisaoComputacional() {
  return (
    <>
      <PageSection
        eyebrow="Novo módulo · branch tratamento-de-imagens"
        title="Visão Computacional"
        description="Recebe frames de até 4 câmeras e identifica objetos com um modelo YOLO, devolvendo um resultado estruturado — sem nenhuma lógica além disso nesta primeira versão."
      >
        <div className="mx-auto max-w-sm">
          <Diagram steps={['Imagem da câmera', 'Tratamento', 'YOLO', 'Detecção', 'Resultado']} />
        </div>
      </PageSection>

      <PageSection
        eyebrow="Resultado estruturado"
        title="O que cada detecção vai trazer"
        description="Campos do DetectedObject / DetectionResult. Alguns já funcionam na primeira versão do módulo; outros são evolução planejada e ainda não devem ser tratados como prontos."
      >
        <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {futureFields.map((field) => (
            <li
              key={field.label}
              className="flex items-center justify-between rounded-lg border border-border bg-surface p-4"
            >
              <span className="text-foreground">{field.label}</span>
              <span
                className={`rounded-full px-2.5 py-1 text-xs font-medium ${
                  field.status === 'disponível'
                    ? 'bg-accent-2/15 text-accent-2'
                    : 'bg-warning/15 text-warning'
                }`}
              >
                {field.status}
              </span>
            </li>
          ))}
        </ul>
      </PageSection>

      <PageSection
        eyebrow="Escopo desta primeira versão"
        title="O que está deliberadamente fora"
      >
        <p className="max-w-3xl text-muted">
          Rastreamento de objetos, cálculo de velocidade, profundidade, triangulação,
          reconhecimento avançado, integração com Arduino/torres/acionamento e persistência em
          banco de dados ficam fora do escopo por ora. O módulo é isolado da lógica principal do
          RadarTorres — nenhum serviço existente (comunicação serial, rastreamento de alvos,
          seleção de torre, acionamento) foi alterado ou é referenciado por ele.
        </p>
      </PageSection>
    </>
  )
}
