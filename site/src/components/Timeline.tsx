import type { TimelineEntry } from '../data/timeline'

export default function Timeline({ entries }: { entries: TimelineEntry[] }) {
  return (
    <ol className="relative border-l border-border pl-6">
      {entries.map((entry, index) => (
        <li key={entry.title} className="mb-8 last:mb-0">
          <span className="absolute -left-[7px] mt-1.5 h-3 w-3 rounded-full border border-background bg-accent" />
          <p className="font-mono text-xs text-muted">
            {String(index + 1).padStart(2, '0')}
          </p>
          <h3 className="font-semibold text-foreground">{entry.title}</h3>
          <p className="mt-1 text-sm text-muted">{entry.description}</p>
        </li>
      ))}
    </ol>
  )
}
