import PageSection from '../components/layout/PageSection'
import TechCard from '../components/TechCard'
import { techCategories, technologies } from '../data/technologies'

export default function Tecnologias() {
  return (
    <PageSection
      eyebrow="Stack"
      title="Tecnologias utilizadas"
      description="Linguagens, frameworks, inteligência artificial e hardware envolvidos no projeto — e onde cada um é usado."
    >
      <div className="flex flex-col gap-10">
        {techCategories.map((category) => (
          <div key={category}>
            <h3 className="mb-4 font-mono text-sm uppercase tracking-widest text-muted">
              {category}
            </h3>
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {technologies
                .filter((tech) => tech.category === category)
                .map((tech) => (
                  <TechCard key={tech.name} tech={tech} />
                ))}
            </div>
          </div>
        ))}
      </div>
    </PageSection>
  )
}
