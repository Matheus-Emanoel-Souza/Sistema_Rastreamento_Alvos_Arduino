import type { Technology } from '../data/technologies'

export default function TechCard({ tech }: { tech: Technology }) {
  return (
    <div className="flex flex-col gap-2 rounded-xl border border-border bg-surface p-5 transition-colors hover:border-accent/50">
      <div className="flex items-center gap-3">
        <span className="text-2xl" aria-hidden>
          {tech.icon}
        </span>
        <h3 className="font-semibold text-foreground">{tech.name}</h3>
      </div>
      <p className="text-sm text-muted">{tech.description}</p>
      <p className="mt-auto pt-2 text-xs text-accent-2">
        <span className="text-muted">Onde é usado: </span>
        {tech.usage}
      </p>
    </div>
  )
}
