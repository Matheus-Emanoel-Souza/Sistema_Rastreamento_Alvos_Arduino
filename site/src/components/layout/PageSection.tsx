import type { ReactNode } from 'react'
import { motion } from 'framer-motion'

interface PageSectionProps {
  title: string
  eyebrow?: string
  description?: string
  children: ReactNode
  id?: string
}

/** Wrapper de seção padrão das páginas internas: título + descrição + animação de entrada. */
export default function PageSection({ title, eyebrow, description, children, id }: PageSectionProps) {
  return (
    <motion.section
      id={id}
      initial={{ opacity: 0, y: 16 }}
      whileInView={{ opacity: 1, y: 0 }}
      viewport={{ once: true, margin: '-80px' }}
      transition={{ duration: 0.45, ease: 'easeOut' }}
      className="mx-auto max-w-6xl px-4 py-14"
    >
      <div className="mb-8">
        {eyebrow && (
          <p className="mb-2 font-mono text-xs uppercase tracking-widest text-accent">{eyebrow}</p>
        )}
        <h2 className="text-2xl font-semibold text-foreground sm:text-3xl">{title}</h2>
        {description && <p className="mt-3 max-w-3xl text-muted">{description}</p>}
      </div>
      {children}
    </motion.section>
  )
}
