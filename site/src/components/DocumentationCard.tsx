import type { DocumentationLink } from '../data/documentation'

const kindIcon: Record<DocumentationLink['kind'], string> = {
  md: '📄',
  pdf: '📕',
  folder: '📁',
}

export default function DocumentationCard({ item }: { item: DocumentationLink }) {
  return (
    <a
      href={item.href}
      target="_blank"
      rel="noreferrer"
      className="flex items-start gap-3 rounded-lg border border-border bg-surface p-4 transition-colors hover:border-accent/50"
    >
      <span className="text-xl" aria-hidden>
        {kindIcon[item.kind]}
      </span>
      <span>
        <span className="block font-medium text-foreground">{item.title}</span>
        <span className="text-sm text-muted">{item.description}</span>
      </span>
    </a>
  )
}
