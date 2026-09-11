import PageSection from '../components/layout/PageSection'
import Diagram from '../components/Diagram'

export default function Arquitetura() {
  return (
    <>
      <PageSection
        eyebrow="Arquitetura atual"
        title="Do sensor à interface"
        description="Separação clássica de responsabilidades (inspirada em MVVM), com toda a lógica de negócio isolada da interface gráfica — detalhado em Docs/Tecnica/ARQUITETURA.md."
      >
        <div className="mx-auto max-w-md">
          <Diagram
            steps={[
              'Sensores / Arduino',
              'Comunicação Serial',
              'RadarTorres (Services)',
              'Processamento (Target/Tower/FireControl)',
              'Interface WPF',
            ]}
          />
        </div>

        <div className="mt-8 grid gap-4 sm:grid-cols-3">
          <div className="rounded-lg border border-border bg-surface p-4">
            <h3 className="font-semibold text-foreground">Views</h3>
            <p className="mt-1 text-sm text-muted">
              Só XAML e pequenos encaminhamentos de eventos de UI. Nenhuma regra de negócio.
            </p>
          </div>
          <div className="rounded-lg border border-border bg-surface p-4">
            <h3 className="font-semibold text-foreground">ViewModels</h3>
            <p className="mt-1 text-sm text-muted">
              Orquestram os serviços e expõem propriedades/comandos simples para binding.
            </p>
          </div>
          <div className="rounded-lg border border-border bg-surface p-4">
            <h3 className="font-semibold text-foreground">Services</h3>
            <p className="mt-1 text-sm text-muted">
              100% das regras: parsing do protocolo serial, rastreamento, seleção de torre,
              acionamento e simulação. Nenhum referencia WPF diretamente.
            </p>
          </div>
        </div>
      </PageSection>

      <PageSection
        eyebrow="Arquitetura futura"
        title="Módulo de Visão Computacional"
        description="Pipeline isolado (branch tratamento-de-imagens), preparado para até 4 câmeras — ver Docs/Tecnica/VISAO_COMPUTACIONAL.md."
      >
        <div className="mx-auto max-w-md">
          <Diagram
            variant="muted"
            steps={[
              'Câmeras',
              'Tratamento de imagens',
              'YOLO (ONNX Runtime)',
              'Detecção de objetos',
              'Posição, distância e movimento',
            ]}
          />
        </div>
      </PageSection>
    </>
  )
}
