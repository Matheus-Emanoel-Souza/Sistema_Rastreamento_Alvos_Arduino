import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import RadarSimulation from '../components/radar/RadarSimulation'
import PageSection from '../components/layout/PageSection'

const REPO_URL = 'https://github.com/Matheus-Emanoel-Souza/Sistema_Rastreamento_Alvos_Arduino'

export default function Home() {
  return (
    <>
      <section className="relative overflow-hidden border-b border-border">
        <div className="pointer-events-none absolute inset-0 opacity-40">
          <div className="radar-sweep absolute left-1/2 top-1/2 h-[600px] w-[600px] -translate-x-1/2 -translate-y-1/2 rounded-full border border-accent/10" />
          <div className="absolute left-1/2 top-1/2 h-[400px] w-[400px] -translate-x-1/2 -translate-y-1/2 rounded-full border border-accent/10" />
          <div className="absolute left-1/2 top-1/2 h-[200px] w-[200px] -translate-x-1/2 -translate-y-1/2 rounded-full border border-accent/10" />
        </div>

        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.5 }}
          className="relative mx-auto flex max-w-4xl flex-col items-center px-4 py-24 text-center"
        >
          <p className="mb-4 font-mono text-xs uppercase tracking-widest text-accent">
            TCC · Engenharia da Computação
          </p>
          <h1 className="text-4xl font-bold sm:text-6xl">
            Radar<span className="text-accent">Torres</span>
          </h1>
          <p className="mt-4 text-xl text-muted">
            Sistema inteligente de detecção, rastreamento e seleção de torres.
          </p>
          <p className="mt-6 max-w-2xl text-muted">
            Aplicativo desktop desenvolvido em Engenharia da Computação, integrando
            <strong className="text-foreground"> Arduino</strong>,{' '}
            <strong className="text-foreground">sensores</strong>,{' '}
            <strong className="text-foreground">comunicação serial</strong>,{' '}
            <strong className="text-foreground">processamento em tempo real</strong>,{' '}
            <strong className="text-foreground">visão computacional</strong> e{' '}
            <strong className="text-foreground">inteligência artificial</strong> para localizar
            alvos e selecionar automaticamente a torre demonstrativa mais adequada.
          </p>

          <div className="mt-8 flex flex-wrap justify-center gap-3">
            <a
              href="#simulador"
              className="rounded-lg bg-accent px-5 py-2.5 font-medium text-background transition-opacity hover:opacity-90"
            >
              Ver funcionamento
            </a>
            <Link
              to="/documentacao"
              className="rounded-lg border border-border px-5 py-2.5 font-medium text-foreground transition-colors hover:border-accent/50"
            >
              Documentação
            </Link>
            <a
              href={REPO_URL}
              target="_blank"
              rel="noreferrer"
              className="rounded-lg border border-border px-5 py-2.5 font-medium text-foreground transition-colors hover:border-accent/50"
            >
              Código fonte
            </a>
          </div>
        </motion.div>
      </section>

      <PageSection
        id="simulador"
        eyebrow="Demonstração interativa"
        title="Veja o radar em ação"
        description="Arraste o alvo ou ajuste ângulo/distância: o simulador reproduz o mesmo cálculo de quadrante e seleção de torre do TowerSelectionService real (documentado em Docs/Tecnica/ALGORITMO_SELECAO_TORRE.md)."
      >
        <RadarSimulation />
      </PageSection>
    </>
  )
}
