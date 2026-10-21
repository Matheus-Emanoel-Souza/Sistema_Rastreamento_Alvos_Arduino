import { useEffect, useState } from 'react'
import type { DocumentationLink } from '../data/documentation'
import { toRawUrl } from '../data/documentation'

interface PdfPreviewModalProps {
  item: DocumentationLink
  onClose: () => void
}

type LoadState = 'loading' | 'ready' | 'error'

/**
 * Lightbox de pré-visualização de PDF direto no site.
 *
 * O raw.githubusercontent.com serve PDFs com `Content-Disposition: attachment`,
 * o que faz o navegador baixar o arquivo em vez de exibi-lo quando ele é usado
 * direto como `src`/`data` de <iframe>/<object>. Para exibir de verdade sem sair
 * do site, buscamos os bytes via `fetch` (esse header não afeta fetch) e criamos
 * um Blob local — um `blob:` URL não carrega Content-Disposition nenhum, então o
 * visualizador nativo de PDF do navegador renderiza normalmente.
 */
export default function PdfPreviewModal({ item, onClose }: PdfPreviewModalProps) {
  const rawUrl = toRawUrl(item.href)
  const [state, setState] = useState<LoadState>('loading')
  const [blobUrl, setBlobUrl] = useState<string | null>(null)

  useEffect(() => {
    let objectUrl: string | null = null
    let cancelled = false

    fetch(rawUrl)
      .then((res) => {
        if (!res.ok) throw new Error(`HTTP ${res.status}`)
        return res.blob()
      })
      .then((blob) => {
        if (cancelled) return
        const pdfBlob = blob.type === 'application/pdf' ? blob : new Blob([blob], { type: 'application/pdf' })
        objectUrl = URL.createObjectURL(pdfBlob)
        setBlobUrl(objectUrl)
        setState('ready')
      })
      .catch(() => {
        if (!cancelled) setState('error')
      })

    return () => {
      cancelled = true
      if (objectUrl) URL.revokeObjectURL(objectUrl)
    }
  }, [rawUrl])

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

      <div className="relative w-full flex-1 overflow-hidden rounded-lg border border-border bg-surface">
        {state === 'loading' && (
          <div className="flex h-full flex-col items-center justify-center gap-3 text-muted">
            <span className="h-8 w-8 animate-spin rounded-full border-2 border-border border-t-accent" />
            <p>Carregando PDF…</p>
          </div>
        )}

        {state === 'error' && (
          <div className="flex h-full flex-col items-center justify-center gap-3 p-8 text-center text-muted">
            <p>Não foi possível carregar o PDF aqui.</p>
            <a href={item.href} target="_blank" rel="noreferrer" className="text-accent hover:underline">
              Abrir no GitHub em vez disso
            </a>
          </div>
        )}

        {state === 'ready' && blobUrl && (
          <object data={blobUrl} type="application/pdf" className="h-full w-full">
            <div className="flex h-full flex-col items-center justify-center gap-3 p-8 text-center text-muted">
              <p>Seu navegador não suporta visualização de PDF embutida.</p>
              <a href={blobUrl} target="_blank" rel="noreferrer" className="text-accent hover:underline">
                Abrir o PDF em uma nova aba
              </a>
            </div>
          </object>
        )}
      </div>
    </div>
  )
}
