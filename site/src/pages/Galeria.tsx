import PageSection from '../components/layout/PageSection'

const slots = [
  { title: 'Prints do aplicativo', folder: 'site/src/assets/images' },
  { title: 'Vídeos de demonstração', folder: 'site/src/assets/videos' },
  { title: 'Fotos do hardware', folder: 'site/src/assets/images' },
  { title: 'Diagramas', folder: 'site/src/assets/diagrams' },
]

export default function Galeria() {
  return (
    <PageSection
      eyebrow="Mídia"
      title="Galeria"
      description="Espaço preparado para prints, vídeos, fotos do hardware e diagramas. As pastas já existem no repositório — basta adicionar os arquivos e referenciá-los aqui."
    >
      <div className="grid gap-4 sm:grid-cols-2">
        {slots.map((slot) => (
          <div
            key={slot.title}
            className="flex flex-col items-center justify-center gap-2 rounded-xl border border-dashed border-border bg-surface p-10 text-center"
          >
            <span className="text-3xl" aria-hidden>
              🖼️
            </span>
            <p className="font-medium text-foreground">{slot.title}</p>
            <p className="font-mono text-xs text-muted">{slot.folder}/</p>
            <p className="text-xs text-muted">Ainda sem mídia adicionada.</p>
          </div>
        ))}
      </div>
    </PageSection>
  )
}
