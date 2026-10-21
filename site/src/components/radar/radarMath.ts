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

/**
 * Vizinhança geométrica entre quadrantes — mesma regra de QuadrantHelper.AreAdjacent.
 * Q1-Q2 e Q3-Q4 são vizinhos no eixo Y; Q1-Q4 e Q2-Q3 no eixo X; Q1-Q3 e Q2-Q4 são
 * diagonais (NÃO vizinhos).
 */
export function areAdjacent(a: Quadrant, b: Quadrant): boolean {
  if (a === b) return false
  const adjacentPairs: Record<Quadrant, Quadrant[]> = {
    Q1: ['Q2', 'Q4'],
    Q2: ['Q1', 'Q3'],
    Q3: ['Q2', 'Q4'],
    Q4: ['Q1', 'Q3'],
  }
  return adjacentPairs[a].includes(b)
}

export function euclideanDistance(a: Point, b: Point): number {
  return Math.hypot(a.x - b.x, a.y - b.y)
}

export type SelectionLayer = 'quadrante-proprio' | 'adjacente' | 'cobertura' | 'sobrecarregada'

export interface TowerSelectionResult {
  tower: Tower
  distance: number
  layer: SelectionLayer
}

/**
 * Reproduz TowerSelectionService.SelectTowerFor (versão em camadas, com cooperação
 * entre torres adjacentes): prioriza a torre do próprio quadrante do alvo; se estiver
 * comprometida/indisponível, tenta uma torre de quadrante adjacente cujo próprio
 * quadrante não tenha alvo concorrente; por fim, qualquer torre livre restante.
 * `committedTowerIds` e `quadrantsWithOtherTargets` descrevem o estado dos OUTROS
 * alvos simultâneos (vazio = comportamento de alvo único, como no demo atual).
 */
export function selectTowerAmong(
  target: Point,
  towers: Tower[],
  committedTowerIds: ReadonlySet<number> = new Set(),
  quadrantsWithOtherTargets: ReadonlySet<Quadrant> = new Set(),
): TowerSelectionResult | null {
  const free = towers.filter((t) => !committedTowerIds.has(t.id))
  if (free.length === 0) return null

  const targetQuadrant = quadrantOf(target)

  const home = free.filter((t) => quadrantOf(t) === targetQuadrant)
  const adjacent = home.length > 0
    ? []
    : free.filter((t) => areAdjacent(quadrantOf(t), targetQuadrant) && !quadrantsWithOtherTargets.has(quadrantOf(t)))
  const coverage = home.length === 0 && adjacent.length === 0 ? free : []

  const [pool, layer]: [Tower[], SelectionLayer] =
    home.length > 0
      ? [home, 'quadrante-proprio']
      : adjacent.length > 0
        ? [adjacent, 'adjacente']
        : coverage.length > 0
          ? [coverage, 'cobertura']
          : [towers, 'sobrecarregada']

  let best = pool[0]
  let bestDistance = euclideanDistance(target, best)

  for (const candidate of pool.slice(1)) {
    const d = euclideanDistance(target, candidate)
    if (d < bestDistance) {
      best = candidate
      bestDistance = d
    }
  }

  return { tower: best, distance: bestDistance, layer }
}

/** Atalho de alvo único (sem outros alvos concorrentes) — usado pelo demo visual atual. */
export function selectTower(target: Point, towers: Tower[]): TowerSelectionResult | null {
  return selectTowerAmong(target, towers)
}
