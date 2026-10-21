import { HashRouter, Route, Routes, useLocation } from 'react-router-dom'
import { useEffect } from 'react'
import Navbar from './components/layout/Navbar'
import Footer from './components/layout/Footer'
import Home from './pages/Home'
import Projeto from './pages/Projeto'
import Arquitetura from './pages/Arquitetura'
import Tecnologias from './pages/Tecnologias'
import VisaoComputacional from './pages/VisaoComputacional'
import Documentacao from './pages/Documentacao'
import Testes from './pages/Testes'
import Galeria from './pages/Galeria'
import Sobre from './pages/Sobre'

/** Volta ao topo a cada troca de rota (HashRouter não faz isso sozinho). */
function ScrollToTop() {
  const { pathname, hash } = useLocation()
  useEffect(() => {
    if (!hash || hash === '#simulador') return
    window.scrollTo({ top: 0 })
  }, [pathname, hash])
  return null
}

export default function App() {
  return (
    <HashRouter>
      <ScrollToTop />
      <div className="flex min-h-screen flex-col">
        <Navbar />
        <main className="flex-1">
          <Routes>
            <Route path="/" element={<Home />} />
            <Route path="/projeto" element={<Projeto />} />
            <Route path="/arquitetura" element={<Arquitetura />} />
            <Route path="/tecnologias" element={<Tecnologias />} />
            <Route path="/visao-computacional" element={<VisaoComputacional />} />
            <Route path="/documentacao" element={<Documentacao />} />
            <Route path="/testes" element={<Testes />} />
            <Route path="/galeria" element={<Galeria />} />
            <Route path="/sobre" element={<Sobre />} />
          </Routes>
        </main>
        <Footer />
      </div>
    </HashRouter>
  )
}
