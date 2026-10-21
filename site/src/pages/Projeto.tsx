import PageSection from '../components/layout/PageSection'
import Timeline from '../components/Timeline'
import { timeline } from '../data/timeline'

export default function Projeto() {
  return (
    <>
      <PageSection
        eyebrow="Visão geral"
        title="O que é o RadarTorres"
        description="Aplicativo desktop em C#/WPF que se comunica via porta serial (USB) com um Arduino responsável por sensores de detecção de alvos ao redor de uma base."
      >
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="rounded-xl border border-border bg-surface p-5">
            <h3 className="font-semibold text-foreground">Como funciona</h3>
            <p className="mt-2 text-sm text-muted">
              O software localiza os alvos em um radar gráfico, determina automaticamente o
              quadrante de cada um, seleciona a torre demonstrativa mais próxima/adequada e pode
              acionar um indicador demonstrativo — laser de baixa potência, LED ou simulação em
              software, nunca armamento real.
            </p>
          </div>
          <div className="rounded-xl border border-border bg-surface p-5">
            <h3 className="font-semibold text-foreground">Funciona sem hardware</h3>
            <p className="mt-2 text-sm text-muted">
              Um modo de simulação embutido gera alvos fictícios e permite validar o sistema de
              ponta a ponta mesmo sem nenhum Arduino conectado — essencial para desenvolvimento
              contínuo e para as demonstrações do TCC.
            </p>
          </div>
        </div>
      </PageSection>

      <PageSection eyebrow="Linha do tempo" title="Como o projeto evoluiu">
        <Timeline entries={timeline} />
      </PageSection>

      <PageSection eyebrow="Roadmap" title="O que falta / próximos passos">
        <ul className="grid gap-3 sm:grid-cols-2">
          {[
            'Fechar a Etapa 1: telas de Ações realizadas, Histórico de modos, Usuários e gestão de Chamados de Ajuda.',
            'Etapa 2: indicadores gráficos avançados, filtros avançados, exportação CSV, notificações.',
            'Etapa 3 (Qualidade): testes automatizados mais amplos, revisão de segurança, desempenho.',
            'Migração de persistência: CSV → banco relacional (SQLite/EF Core).',
            'Upload/gravação de firmware pelo Arduino CLI (hoje só compila).',
            'Consolidar o módulo de Visão Computacional (branch tratamento-de-imagens) em main.',
          ].map((item) => (
            <li key={item} className="rounded-lg border border-border bg-surface p-4 text-sm text-muted">
              {item}
            </li>
          ))}
        </ul>
      </PageSection>
    </>
  )
}
