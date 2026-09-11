import { useState } from 'react'
import { NavLink } from 'react-router-dom'

const links = [
  { to: '/', label: 'Home' },
  { to: '/projeto', label: 'Projeto' },
  { to: '/arquitetura', label: 'Arquitetura' },
  { to: '/tecnologias', label: 'Tecnologias' },
  { to: '/visao-computacional', label: 'Visão Computacional' },
  { to: '/documentacao', label: 'Documentação' },
  { to: '/testes', label: 'Testes' },
  { to: '/galeria', label: 'Galeria' },
  { to: '/sobre', label: 'Sobre' },
]

export default function Navbar() {
  const [open, setOpen] = useState(false)

  const linkClass = ({ isActive }: { isActive: boolean }) =>
    `rounded-md px-3 py-2 text-sm transition-colors ${
      isActive ? 'bg-surface-2 text-accent' : 'text-muted hover:text-foreground'
    }`

  return (
    <header className="sticky top-0 z-50 border-b border-border bg-background/90 backdrop-blur">
      <nav className="mx-auto flex max-w-6xl items-center justify-between px-4 py-3">
        <NavLink to="/" className="flex items-center gap-2 text-lg font-semibold">
          <span className="text-accent">📡</span>
          RadarTorres
        </NavLink>

        <ul className="hidden flex-wrap items-center gap-1 lg:flex">
          {links.map((link) => (
            <li key={link.to}>
              <NavLink to={link.to} className={linkClass} end={link.to === '/'}>
                {link.label}
              </NavLink>
            </li>
          ))}
        </ul>

        <button
          type="button"
          onClick={() => setOpen((v) => !v)}
          className="rounded-md border border-border px-3 py-2 text-sm text-foreground lg:hidden"
          aria-expanded={open}
          aria-label="Abrir menu"
        >
          {open ? '✕' : '☰'}
        </button>
      </nav>

      {open && (
        <ul className="flex flex-col gap-1 border-t border-border px-4 py-3 lg:hidden">
          {links.map((link) => (
            <li key={link.to}>
              <NavLink
                to={link.to}
                className={linkClass}
                end={link.to === '/'}
                onClick={() => setOpen(false)}
              >
                {link.label}
              </NavLink>
            </li>
          ))}
        </ul>
      )}
    </header>
  )
}
