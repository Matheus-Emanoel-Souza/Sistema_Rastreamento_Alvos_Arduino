import { useEffect } from 'react'
import type { DocumentationLink } from '../data/documentation'
import { toRawUrl } from '../data/documentation'

interface PdfPreviewModalProps {
  item: DocumentationLink
  onClose: () => void
}

/** Lightbox de pré-visualização de PDF direto no site, sem sair pro GitHub. */
export default function PdfPreviewModal({ item, onClose }: PdfPreviewModalProps) {
  const rawUrl = toRawUrl(item.href)

  useEffect(() => {
    const handleKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', handleKey)
    document.body.style.overflow = 'hidden'
    return () => {
      window.removeEventListener('keydown', handleKey)
      document.body.style.overflow = ''
    }
  }, [onClose])

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={item.title}
      className="fixed inset-0 z-[100] flex flex-col gap-3 bg-background/95 p-4 backdrop-blur"
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose()
      }}
    >
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h3 className="font-semibold text-foreground">{item.title}</h3>
          <p className="text-sm text-muted">{item.description}</p>
        </div>
        <div className="flex items-center gap-2">
          <a
            href={item.href}
            target="_blank"
            rel="noreferrer"
            className="rounded-md border border-border px-3 py-1.5 text-sm text-foreground transition-colors hover:border-accent/50"
          >
            Abrir no GitHub
          </a>
          <button
            type="button"
            onClick={onClose}
            className="rounded-md border border-border px-3 py-1.5 text-sm text-foreground transition-colors hover:border-accent/50"
          >
            Fechar ✕
          </button>
        </div>
      </div>

      <object
        data={rawUrl}
        type="application/pdf"
        className="w-full flex-1 rounded-lg border border-border bg-surface"
      >
        <div className="flex h-full flex-col items-center justify-center gap-3 p-8 text-center text-muted">
          <p>Não foi possível exibir o PDF aqui.</p>
          <a href={rawUrl} target="_blank" rel="noreferrer" className="text-accent hover:underline">
            Abrir o PDF em uma nova aba
          </a>
        </div>
      </object>
    </div>
  )
}
