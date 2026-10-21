const REPO_URL = 'https://github.com/Matheus-Emanoel-Souza/Sistema_Rastreamento_Alvos_Arduino'

export default function Footer() {
  return (
    <footer className="border-t border-border py-8 text-center text-sm text-muted">
      <p>
        RadarTorres — Trabalho de Conclusão de Curso em Engenharia da Computação.{' '}
        <a href={REPO_URL} target="_blank" rel="noreferrer" className="text-accent hover:underline">
          Código-fonte no GitHub
        </a>
      </p>
    </footer>
  )
}
