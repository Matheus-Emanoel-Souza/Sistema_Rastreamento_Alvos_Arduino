// Espelha, em TypeScript, a lógica documentada em
// Docs/Tecnica/ALGORITMO_SELECAO_TORRE.md (CoordinateConverter, QuadrantHelper,
// DistanceCalculator e TowerSelectionService do app C#/WPF real).

export interface Point {
  x: number
  y: number
}

export interface Tower extends Point {
  id: number
  name: string
}

export type Quadrant = 'Q1' | 'Q2' | 'Q3' | 'Q4'

/** Torres de demonstração — mesmas posições de exemplo do appsettings.json no README. */
export const demoTowers: Tower[] = [
  { id: 1, name: 'Torre 1', x: 3, y: 3 },
  { id: 2, name: 'Torre 2', x: -3, y: 3 },
  { id: 3, name: 'Torre 3', x: -3, y: -3 },
  { id: 4, name: 'Torre 4', x: 3, y: -3 },
]

/** Converte leitura polar (ângulo em graus, distância) em coordenadas cartesianas. */
export function polarToCartesian(angleDeg: number, distance: number): Point {
  const rad = (angleDeg * Math.PI) / 180
  return {
    x: distance * Math.cos(rad),
    y: distance * Math.sin(rad),
  }
}

/** Quadrante a partir do sinal de X/Y — mesma regra do QuadrantHelper. */
export function quadrantOf(point: Point): Quadrant {
  if (point.x >= 0 && point.y >= 0) return 'Q1'
  if (point.x < 0 && point.y >= 0) return 'Q2'
  if (point.x < 0 && point.y < 0) return 'Q3'
  return 'Q4'
}

export function euclideanDistance(a: Point, b: Point): number {
  return Math.hypot(a.x - b.x, a.y - b.y)
}

export interface TowerSelectionResult {
  tower: Tower
  distance: number
  sameQuadrant: boolean
}

/**
 * Reproduz TowerSelectionService: prioriza torres do mesmo quadrante do alvo;
 * se nenhuma disponível estiver no quadrante, considera todas; escolhe a de
 * menor distância euclidiana.
 */
export function selectTower(target: Point, towers: Tower[]): TowerSelectionResult | null {
  if (towers.length === 0) return null

  const targetQuadrant = quadrantOf(target)
  const sameQuadrant = towers.filter((t) => quadrantOf(t) === targetQuadrant)
  const pool = sameQuadrant.length > 0 ? sameQuadrant : towers

  let best = pool[0]
  let bestDistance = euclideanDistance(target, best)

  for (const candidate of pool.slice(1)) {
    const d = euclideanDistance(target, candidate)
    if (d < bestDistance) {
      best = candidate
      bestDistance = d
    }
  }

  return { tower: best, distance: bestDistance, sameQuadrant: sameQuadrant.length > 0 }
}
