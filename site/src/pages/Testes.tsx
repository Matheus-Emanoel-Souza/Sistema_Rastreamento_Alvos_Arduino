import PageSection from '../components/layout/PageSection'
import { futureMetrics, softwareTests, type TestStatus } from '../data/tests'

const statusClass: Record<TestStatus, string> = {
  Concluído: 'bg-accent-2/15 text-accent-2',
  'Em desenvolvimento': 'bg-warning/15 text-warning',
  Planejado: 'bg-accent/15 text-accent',
}

export default function Testes() {
  return (
    <>
      <PageSection eyebrow="Qualidade" title="Testes de software">
        <div className="overflow-hidden rounded-xl border border-border">
          <table className="w-full text-left text-sm">
            <thead className="bg-surface-2 text-muted">
              <tr>
                <th className="px-4 py-3 font-medium">Teste</th>
                <th className="px-4 py-3 font-medium">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {softwareTests.map((row) => (
                <tr key={row.test} className="bg-surface">
                  <td className="px-4 py-3 text-foreground">{row.test}</td>
                  <td className="px-4 py-3">
                    <span className={`rounded-full px-2.5 py-1 text-xs font-medium ${statusClass[row.status]}`}>
                      {row.status}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </PageSection>

      <PageSection
        eyebrow="Roadmap de testes"
        title="Testes futuros"
        description="Métricas planejadas para o módulo de Visão Computacional, ainda não coletadas."
      >
        <ul className="grid gap-3 sm:grid-cols-2">
          {futureMetrics.map((metric) => (
            <li key={metric.metric} className="rounded-lg border border-border bg-surface p-4">
              <p className="font-medium text-foreground">{metric.metric}</p>
              <p className="mt-1 text-sm text-muted">{metric.description}</p>
            </li>
          ))}
        </ul>
      </PageSection>
    </>
  )
}
