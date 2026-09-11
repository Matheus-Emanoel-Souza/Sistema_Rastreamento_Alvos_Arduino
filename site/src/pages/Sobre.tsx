import PageSection from '../components/layout/PageSection'

export default function Sobre() {
  return (
    <PageSection eyebrow="Sobre o projeto" title="RadarTorres">
      <div className="grid gap-8 lg:grid-cols-[1fr_auto]">
        <div className="max-w-2xl text-muted">
          <p>
            Projeto desenvolvido como Trabalho de Conclusão de Curso em Engenharia da
            Computação, integrando hardware (Arduino, sensores, câmeras), software desktop
            (C#/WPF) e, em desenvolvimento, visão computacional com inteligência artificial
            (YOLO/ONNX).
          </p>
          <p className="mt-4">
            Desenvolvido por{' '}
            <strong className="text-foreground">Gabriel Vasconcellos de Morais</strong> e{' '}
            <strong className="text-foreground">Matheus Emanoel Souza</strong>.
          </p>
        </div>

        <div className="flex flex-col gap-6">
          <div className="flex flex-col gap-3 rounded-xl border border-border bg-surface p-5">
            <h3 className="font-mono text-xs uppercase tracking-widest text-muted">
              Matheus Emanoel Souza
            </h3>
            <a
              href="https://github.com/Matheus-Emanoel-Souza"
              target="_blank"
              rel="noreferrer"
              className="text-accent hover:underline"
            >
              GitHub
            </a>
            <a
              href="https://www.linkedin.com/in/matheus-emanoel-821241184/"
              target="_blank"
              rel="noreferrer"
              className="text-accent hover:underline"
            >
              LinkedIn
            </a>
            <a href="#" className="text-muted line-through" aria-disabled>
              Currículo (adicionar link)
            </a>
          </div>

          <div className="flex flex-col gap-3 rounded-xl border border-border bg-surface p-5">
            <h3 className="font-mono text-xs uppercase tracking-widest text-muted">
              Gabriel Vasconcellos de Morais
            </h3>
            <a
              href="https://www.linkedin.com/in/gabriel-vasconcellos-de-morais/"
              target="_blank"
              rel="noreferrer"
              className="text-accent hover:underline"
            >
              LinkedIn
            </a>
          </div>
        </div>
      </div>
    </PageSection>
  )
}
