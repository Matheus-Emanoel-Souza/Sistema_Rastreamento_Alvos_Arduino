import { useCallback, useEffect, useRef, useState } from 'react'
import {
  demoTowers,
  polarToCartesian,
  quadrantOf,
  selectTower,
  type Tower,
} from './radarMath'

const CANVAS_SIZE = 440
const MAX_DISTANCE = 5
const PADDING = 36
const SCALE = (CANVAS_SIZE / 2 - PADDING) / MAX_DISTANCE
const CENTER = CANVAS_SIZE / 2

function toScreen(x: number, y: number) {
  return { sx: CENTER + x * SCALE, sy: CENTER - y * SCALE }
}

function toWorld(sx: number, sy: number) {
  return { x: (sx - CENTER) / SCALE, y: -(sy - CENTER) / SCALE }
}

/**
 * Simulador do radar — reproduz o desenho e o algoritmo de seleção de torre
 * do RadarControl / TowerSelectionService reais (ver radarMath.ts), não é
 * uma animação apenas decorativa: arrastar o alvo (ou mexer nos sliders)
 * recalcula quadrante, distância e a torre escolhida em tempo real.
 */
export default function RadarSimulation() {
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const [angleDeg, setAngleDeg] = useState(35)
  const [distance, setDistance] = useState(3.2)
  const [dragging, setDragging] = useState(false)
  const [towers] = useState<Tower[]>(demoTowers)
  const sweepRef = useRef(0)
  const [sweepTick, setSweepTick] = useState(0)

  const target = polarToCartesian(angleDeg, distance)
  const quadrant = quadrantOf(target)
  const selection = selectTower(target, towers)

  const updateFromScreenPoint = useCallback((sx: number, sy: number) => {
    const world = toWorld(sx, sy)
    const rawDistance = Math.hypot(world.x, world.y)
    const clamped = Math.min(rawDistance, MAX_DISTANCE)
    const angle = (Math.atan2(world.y, world.x) * 180) / Math.PI
    setDistance(Number(clamped.toFixed(2)))
    setAngleDeg(Number((angle < 0 ? angle + 360 : angle).toFixed(1)))
  }, [])

  const pointFromEvent = (
    canvas: HTMLCanvasElement,
    clientX: number,
    clientY: number,
  ) => {
    const rect = canvas.getBoundingClientRect()
    const scaleX = CANVAS_SIZE / rect.width
    const scaleY = CANVAS_SIZE / rect.height
    return { sx: (clientX - rect.left) * scaleX, sy: (clientY - rect.top) * scaleY }
  }

  // animação contínua do sweep do radar (varredura) — desenhada a ~30fps
  useEffect(() => {
    let raf = 0
    let last = performance.now()
    const loop = (now: number) => {
      const dt = now - last
      last = now
      sweepRef.current = (sweepRef.current + dt * 0.05) % 360
      setSweepTick((n) => (n + 1) % 1000000)
      raf = requestAnimationFrame(loop)
    }
    raf = requestAnimationFrame(loop)
    return () => cancelAnimationFrame(raf)
  }, [])

  // desenho do canvas — roda a cada render (estado do alvo ou sweep mudou)
  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return
    const ctx = canvas.getContext('2d')
    if (!ctx) return

    ctx.clearRect(0, 0, CANVAS_SIZE, CANVAS_SIZE)

    // anéis de distância
    ctx.strokeStyle = 'rgba(34, 211, 238, 0.18)'
    ctx.lineWidth = 1
    for (let r = 1; r <= MAX_DISTANCE; r++) {
      ctx.beginPath()
      ctx.arc(CENTER, CENTER, r * SCALE, 0, Math.PI * 2)
      ctx.stroke()
    }

    // eixos dos quadrantes
    ctx.strokeStyle = 'rgba(148, 163, 184, 0.35)'
    ctx.beginPath()
    ctx.moveTo(PADDING / 2, CENTER)
    ctx.lineTo(CANVAS_SIZE - PADDING / 2, CENTER)
    ctx.moveTo(CENTER, PADDING / 2)
    ctx.lineTo(CENTER, CANVAS_SIZE - PADDING / 2)
    ctx.stroke()

    // rótulos de quadrante
    ctx.fillStyle = 'rgba(148, 163, 184, 0.55)'
    ctx.font = '12px ui-monospace, monospace'
    ctx.fillText('Q1', CENTER + MAX_DISTANCE * SCALE - 22, CENTER - 8)
    ctx.fillText('Q2', CENTER - MAX_DISTANCE * SCALE + 6, CENTER - 8)
    ctx.fillText('Q3', CENTER - MAX_DISTANCE * SCALE + 6, CENTER + 18)
    ctx.fillText('Q4', CENTER + MAX_DISTANCE * SCALE - 22, CENTER + 18)

    // sweep (varredura) rotativo, sutil
    const sweepRad = (sweepRef.current * Math.PI) / 180
    const gradient = ctx.createConicGradient
      ? ctx.createConicGradient(sweepRad, CENTER, CENTER)
      : null
    if (gradient) {
      gradient.addColorStop(0, 'rgba(74, 222, 128, 0.22)')
      gradient.addColorStop(0.06, 'rgba(74, 222, 128, 0)')
      gradient.addColorStop(1, 'rgba(74, 222, 128, 0)')
      ctx.fillStyle = gradient
      ctx.beginPath()
      ctx.arc(CENTER, CENTER, MAX_DISTANCE * SCALE, 0, Math.PI * 2)
      ctx.fill()
    }

    // base central
    ctx.fillStyle = '#e5e7eb'
    ctx.beginPath()
    ctx.arc(CENTER, CENTER, 5, 0, Math.PI * 2)
    ctx.fill()

    // torres (quadrado, destaca a selecionada)
    towers.forEach((tower) => {
      const { sx, sy } = toScreen(tower.x, tower.y)
      const isSelected = selection?.tower.id === tower.id
      const size = isSelected ? 16 : 12
      ctx.fillStyle = isSelected ? '#4ade80' : '#22d3ee'
      ctx.fillRect(sx - size / 2, sy - size / 2, size, size)
      ctx.strokeStyle = 'rgba(5, 7, 13, 0.8)'
      ctx.strokeRect(sx - size / 2, sy - size / 2, size, size)
      ctx.fillStyle = 'rgba(229, 231, 235, 0.85)'
      ctx.font = '11px ui-monospace, monospace'
      ctx.fillText(tower.name, sx + size / 2 + 4, sy + 4)
    })

    // linha até a torre selecionada
    if (selection) {
      const { sx: tsx, sy: tsy } = toScreen(selection.tower.x, selection.tower.y)
      const { sx: xsx, sy: xsy } = toScreen(target.x, target.y)
      ctx.strokeStyle = 'rgba(74, 222, 128, 0.7)'
      ctx.setLineDash([5, 4])
      ctx.beginPath()
      ctx.moveTo(xsx, xsy)
      ctx.lineTo(tsx, tsy)
      ctx.stroke()
      ctx.setLineDash([])
    }

    // alvo
    const { sx: axs, sy: ays } = toScreen(target.x, target.y)
    ctx.fillStyle = '#fbbf24'
    ctx.beginPath()
    ctx.arc(axs, ays, 7, 0, Math.PI * 2)
    ctx.fill()
    ctx.strokeStyle = 'rgba(5, 7, 13, 0.8)'
    ctx.stroke()
  }, [target.x, target.y, towers, selection, sweepTick])

  return (
    <div className="grid gap-6 md:grid-cols-[auto_1fr]">
      <div className="mx-auto">
        <canvas
          ref={canvasRef}
          width={CANVAS_SIZE}
          height={CANVAS_SIZE}
          className="max-w-full touch-none rounded-full border border-border bg-surface"
          onPointerDown={(e) => {
            const canvas = canvasRef.current
            if (!canvas) return
            setDragging(true)
            const { sx, sy } = pointFromEvent(canvas, e.clientX, e.clientY)
            updateFromScreenPoint(sx, sy)
          }}
          onPointerMove={(e) => {
            if (!dragging) return
            const canvas = canvasRef.current
            if (!canvas) return
            const { sx, sy } = pointFromEvent(canvas, e.clientX, e.clientY)
            updateFromScreenPoint(sx, sy)
          }}
          onPointerUp={() => setDragging(false)}
          onPointerLeave={() => setDragging(false)}
        />
        <p className="mt-2 text-center text-xs text-muted">
          Arraste o alvo (ponto amarelo) ou use os controles ao lado.
        </p>
      </div>

      <div className="flex flex-col justify-center gap-5 rounded-xl border border-border bg-surface p-5">
        <div>
          <label className="mb-1 flex justify-between text-sm text-muted">
            <span>Ângulo</span>
            <span className="font-mono text-foreground">{angleDeg.toFixed(1)}°</span>
          </label>
          <input
            type="range"
            min={0}
            max={359}
            step={0.5}
            value={angleDeg}
            onChange={(e) => setAngleDeg(Number(e.target.value))}
            className="w-full accent-accent"
          />
        </div>

        <div>
          <label className="mb-1 flex justify-between text-sm text-muted">
            <span>Distância</span>
            <span className="font-mono text-foreground">{distance.toFixed(2)} m</span>
          </label>
          <input
            type="range"
            min={0}
            max={MAX_DISTANCE}
            step={0.05}
            value={distance}
            onChange={(e) => setDistance(Number(e.target.value))}
            className="w-full accent-accent"
          />
        </div>

        <dl className="grid grid-cols-2 gap-x-4 gap-y-2 border-t border-border pt-4 font-mono text-sm">
          <dt className="text-muted">Posição (X, Y)</dt>
          <dd>{target.x.toFixed(2)}, {target.y.toFixed(2)}</dd>

          <dt className="text-muted">Quadrante</dt>
          <dd className="text-accent">{quadrant}</dd>

          <dt className="text-muted">Torre selecionada</dt>
          <dd className="text-accent-2">{selection?.tower.name ?? '—'}</dd>

          <dt className="text-muted">Distância até a torre</dt>
          <dd>{selection ? selection.distance.toFixed(2) : '—'} m</dd>

          <dt className="text-muted">Critério</dt>
          <dd>{selection?.sameQuadrant ? 'mesmo quadrante' : 'todas disponíveis'}</dd>
        </dl>
      </div>
    </div>
  )
}
