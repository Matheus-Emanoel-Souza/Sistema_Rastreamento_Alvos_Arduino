interface DiagramProps {
  steps: string[]
  variant?: 'accent' | 'muted'
}

/** Cadeia vertical de etapas (Sensores → Serial → Processamento → ...), reutilizável. */
export default function Diagram({ steps, variant = 'accent' }: DiagramProps) {
  const color = variant === 'accent' ? 'border-accent/60 text-accent' : 'border-accent-2/60 text-accent-2'

  return (
    <div className="flex flex-col items-stretch gap-1">
      {steps.map((step, index) => (
        <div key={step} className="flex flex-col items-center">
          <div className={`w-full rounded-lg border bg-surface px-4 py-3 text-center font-mono text-sm ${color}`}>
            {step}
          </div>
          {index < steps.length - 1 && <span className="text-muted">↓</span>}
        </div>
      ))}
    </div>
  )
}
