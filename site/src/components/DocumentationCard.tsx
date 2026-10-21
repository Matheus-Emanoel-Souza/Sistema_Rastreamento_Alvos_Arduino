import type { DocumentationLink } from '../data/documentation'

const kindIcon: Record<DocumentationLink['kind'], string> = {
  md: '📄',
  pdf: '📕',
  folder: '📁',
}

interface DocumentationCardProps {
  item: DocumentationLink
  onPreview?: (item: DocumentationLink) => void
}

export default function DocumentationCard({ item, onPreview }: DocumentationCardProps) {
  const body = (
    <>
      <span className="text-xl" aria-hidden>
        {kindIcon[item.kind]}
      </span>
      <span>
        <span className="block font-medium text-foreground">{item.title}</span>
        <span className="text-sm text-muted">{item.description}</span>
      </span>
    </>
  )

  if (item.kind === 'pdf' && onPreview) {
    return (
      <button
        type="button"
        onClick={() => onPreview(item)}
        className="flex items-start gap-3 rounded-lg border border-border bg-surface p-4 text-left transition-colors hover:border-accent/50"
      >
        {body}
        <span className="ml-auto shrink-0 self-center font-mono text-xs text-accent">
          Pré-visualizar →
        </span>
      </button>
    )
  }

  return (
    <a
      href={item.href}
      target="_blank"
      rel="noreferrer"
      className="flex items-start gap-3 rounded-lg border border-border bg-surface p-4 transition-colors hover:border-accent/50"
    >
      {body}
    </a>
  )
}
