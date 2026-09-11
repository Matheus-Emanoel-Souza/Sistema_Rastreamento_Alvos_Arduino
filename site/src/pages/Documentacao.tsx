import { useState } from 'react'
import PageSection from '../components/layout/PageSection'
import DocumentationCard from '../components/DocumentationCard'
import PdfPreviewModal from '../components/PdfPreviewModal'
import { documentationSections, type DocumentationLink } from '../data/documentation'

export default function Documentacao() {
  const [preview, setPreview] = useState<DocumentationLink | null>(null)

  return (
    <PageSection
      eyebrow="Central de documentos"
      title="Documentação"
      description="Todos os links abrem os arquivos reais no repositório — nada aqui é duplicado, só referenciado. PDFs podem ser pré-visualizados sem sair do site."
    >
      <div className="flex flex-col gap-10">
        {documentationSections.map((section) => (
          <div key={section.title}>
            <h3 className="mb-4 font-mono text-sm uppercase tracking-widest text-muted">
              {section.title}
            </h3>
            <div className="grid gap-3 sm:grid-cols-2">
              {section.items.map((item) => (
                <DocumentationCard
                  key={item.href}
                  item={item}
                  onPreview={item.kind === 'pdf' ? setPreview : undefined}
                />
              ))}
            </div>
          </div>
        ))}
      </div>

      {preview && <PdfPreviewModal item={preview} onClose={() => setPreview(null)} />}
    </PageSection>
  )
}
