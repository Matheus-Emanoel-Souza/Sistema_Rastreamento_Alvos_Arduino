import PageSection from '../components/layout/PageSection'
import DocumentationCard from '../components/DocumentationCard'
import { documentationSections } from '../data/documentation'

export default function Documentacao() {
  return (
    <PageSection
      eyebrow="Central de documentos"
      title="Documentação"
      description="Todos os links abrem os arquivos reais no repositório — nada aqui é duplicado, só referenciado."
    >
      <div className="flex flex-col gap-10">
        {documentationSections.map((section) => (
          <div key={section.title}>
            <h3 className="mb-4 font-mono text-sm uppercase tracking-widest text-muted">
              {section.title}
            </h3>
            <div className="grid gap-3 sm:grid-cols-2">
              {section.items.map((item) => (
                <DocumentationCard key={item.href} item={item} />
              ))}
            </div>
          </div>
        ))}
      </div>
    </PageSection>
  )
}
