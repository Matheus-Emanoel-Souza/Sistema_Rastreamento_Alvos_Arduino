"""Verificacao geometrica do conjunto da torre do canhao.

Checa, sobre as malhas realmente geradas:
  1. estanqueidade e volume de cada peca;
  2. interferencia entre pecas (numero de enrolamento num grid 3D);
  3. folga do berco contra os montantes e a plataforma ao longo do curso
     de tilt, varrendo o berco em passos de 1 grau;
  4. coincidencia dos furos de interface.

Uso: python verifica.py
"""

import math
import sys

import meshlib as M
import torre_canhao as T


# ------------------------------------------------------------------
# numero de enrolamento: >= 1 significa dentro do solido, mesmo quando a
# peca e uma uniao de prismas que se sobrepoem
# ------------------------------------------------------------------

class Solido(object):
    def __init__(self, mesh, celula=4.0):
        self.celula = celula
        self.balde = {}
        self.bbox = M.bbox(mesh)
        for t in mesh:
            nz = ((t[1][0] - t[0][0]) * (t[2][1] - t[0][1])
                  - (t[1][1] - t[0][1]) * (t[2][0] - t[0][0]))
            if abs(nz) < 1e-12:
                continue                       # triangulo vertical: nao conta
            xs = [p[0] for p in t]
            ys = [p[1] for p in t]
            i0, i1 = int(math.floor(min(xs) / celula)), int(math.floor(max(xs) / celula))
            j0, j1 = int(math.floor(min(ys) / celula)), int(math.floor(max(ys) / celula))
            for i in range(i0, i1 + 1):
                for j in range(j0, j1 + 1):
                    self.balde.setdefault((i, j), []).append((t, 1 if nz > 0 else -1))

    def dentro(self, p):
        x0, y0, z0, x1, y1, z1 = self.bbox
        if not (x0 <= p[0] <= x1 and y0 <= p[1] <= y1 and z0 <= p[2] <= z1):
            return False
        chave = (int(math.floor(p[0] / self.celula)), int(math.floor(p[1] / self.celula)))
        w = 0
        for t, s in self.balde.get(chave, ()):
            a, b, c = t
            d = ((b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1]))
            if abs(d) < 1e-12:
                continue
            l1 = ((b[1] - c[1]) * (p[0] - c[0]) + (c[0] - b[0]) * (p[1] - c[1])) / d
            l2 = ((c[1] - a[1]) * (p[0] - c[0]) + (a[0] - c[0]) * (p[1] - c[1])) / d
            l3 = 1.0 - l1 - l2
            if l1 < 0 or l2 < 0 or l3 < 0:
                continue
            z = l1 * a[2] + l2 * b[2] + l3 * c[2]
            if z > p[2]:
                w += s
        return w >= 1


def interfere(sa, sb, passo=1.5):
    """Procura um ponto interno as duas pecas. Devolve (n_pontos, exemplo)."""
    ax0, ay0, az0, ax1, ay1, az1 = sa.bbox
    bx0, by0, bz0, bx1, by1, bz1 = sb.bbox
    x0, x1 = max(ax0, bx0), min(ax1, bx1)
    y0, y1 = max(ay0, by0), min(ay1, by1)
    z0, z1 = max(az0, bz0), min(az1, bz1)
    if x0 >= x1 or y0 >= y1 or z0 >= z1:
        return 0, None
    achados, exemplo = 0, None
    x = x0 + passo / 2.0
    while x < x1:
        y = y0 + passo / 2.0
        while y < y1:
            z = z0 + passo / 2.0
            while z < z1:
                p = (x, y, z)
                if sa.dentro(p) and sb.dentro(p):
                    achados += 1
                    if exemplo is None:
                        exemplo = p
                z += passo
            y += passo
        x += passo
    return achados, exemplo


def main():
    pivo_z, _, _ = T.pivo_equilibrado()
    pivo_z = round(pivo_z, 1)
    pecas = T.conjunto(pivo_z)

    print('=' * 70)
    print('1) MALHAS')
    erro = 0
    for nome, m in pecas.items():
        ab, vol = M.watertight(m), M.volume(m)
        ok = ab == 0 and vol > 0
        erro += 0 if ok else 1
        print('   %-32s %s  vol=%8.0f mm3  arestas abertas=%d'
              % (nome, 'OK  ' if ok else 'FALHA', vol, ab))

    print('=' * 70)
    print('2) INTERFERENCIA ENTRE PECAS (grid 1,5 mm)')
    solidos = {n: Solido(m) for n, m in pecas.items()}
    nomes = list(pecas)
    for i in range(len(nomes)):
        for j in range(i + 1, len(nomes)):
            a, b = nomes[i], nomes[j]
            n, ex = interfere(solidos[a], solidos[b])
            if n:
                erro += 1
                print('   FALHA %s x %s: %d pontos comuns, ex. (%.1f, %.1f, %.1f)'
                      % (a[:2], b[:2], n, ex[0], ex[1], ex[2]))
            else:
                print('   OK    %s x %s' % (a[:24], b[:24]))

    print('=' * 70)
    print('3) FOLGA DO BERCO AO LONGO DO TILT')
    berco_local = T.peca_berco(pivo_z)
    fixos = [Solido(pecas[n]) for n in pecas if n.startswith(('01', '02', '05'))]
    pior = None
    for g in range(-int(T.TILT_MAX), int(T.TILT_MAX) + 1):
        giro = M.translate(M.rot_x(M.translate(berco_local, 0, 0, -pivo_z), g),
                           0, 0, T.H_EIXO)
        zmin = M.bbox(giro)[2]
        sg = Solido(giro)
        bate = 0
        for f in fixos:
            n, _ = interfere(sg, f, 2.0)
            bate += n
        if bate:
            erro += 1
            print('   FALHA em %+3d graus: %d pontos de colisao' % (g, bate))
        if pior is None or zmin < pior[1]:
            pior = (g, zmin)
    print('   cota mais baixa do berco: %.1f mm acima do deck (em %+d graus)'
          % (pior[1], pior[0]))
    print('   altura do pe do montante: %.1f mm' % T.PE_ESP)
    print('   topo da nervura sobre o berco: %.1f mm' % 14.0)

    print('=' * 70)
    print('4) INTERFACES')
    checks = [
        ('nariz do munhao x furo da placa',
         T.MANCAL_D - T.NARIZ_D, 0.2, 0.6),
        ('folga cubo do berco x face da placa',
         T.PL_X0 - T.MUNHAO_X, 0.4, 1.0),
        ('engate do eixo do 28BYJ-48 no soquete',
         T.NARIZ_X1 - (T.PL_X1 - T.EIXO_SAI), 6.0, 99.0),
        ('sobra do nariz ate a face externa da placa',
         T.PL_X1 - T.NARIZ_X1, 1.0, 3.0),
        ('parede do deck sob a porca M3',
         T.SEDE_PROF, 2.5, 99.0),
    ]
    for nome, valor, lo, hi in checks:
        ok = lo <= valor <= hi
        erro += 0 if ok else 1
        print('   %-42s %6.2f mm  [%.1f..%.1f] %s'
              % (nome, valor, lo, hi, 'OK' if ok else 'FORA'))

    furos_plat = set((round(l * x, 2), round(l * y, 2))
                     for l in (1, -1) for x, y in T.furos_pe())
    furos_mont = set((round(x, 2), round(y, 2)) for x, y in T.furos_pe())
    furos_mont |= set((round(-x, 2), round(-y, 2)) for x, y in T.furos_pe())
    ok = furos_plat == furos_mont
    erro += 0 if ok else 1
    print('   %-42s %s' % ('furos do deck x furos dos pes', 'OK' if ok else 'FORA'))

    print('=' * 70)
    print('RESULTADO: %s' % ('tudo certo' if erro == 0 else '%d problema(s)' % erro))
    return 1 if erro else 0


if __name__ == '__main__':
    sys.exit(main())
