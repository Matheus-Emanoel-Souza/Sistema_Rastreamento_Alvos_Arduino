"""Biblioteca minima de malhas para os modelos 3D do RadarTorres.

Sem dependencias externas: usa so a biblioteca padrao do Python 3.
Todas as pecas sao prismaticas, entao o primitivo central e a extrusao de
uma regiao 2D (contorno + furos) entre duas cotas Z.

Convencao: milimetros, Z para cima, orientacao de montagem.
"""

import math
import struct

# ---------------------------------------------------------------- perfis 2D


def circle(r, n=64, cx=0.0, cy=0.0, start=0.0):
    """Poligono circular no sentido anti-horario."""
    return [(cx + r * math.cos(start + 2 * math.pi * i / n),
             cy + r * math.sin(start + 2 * math.pi * i / n)) for i in range(n)]


def rect(x0, y0, x1, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


def rrect(x0, y0, x1, y1, r, n=8):
    """Retangulo de cantos arredondados (raio r)."""
    r = min(r, (x1 - x0) / 2.0, (y1 - y0) / 2.0)
    if r <= 1e-9:
        return rect(x0, y0, x1, y1)
    pts = []
    cantos = [(x1 - r, y0 + r, -math.pi / 2), (x1 - r, y1 - r, 0.0),
              (x0 + r, y1 - r, math.pi / 2), (x0 + r, y0 + r, math.pi)]
    for cx, cy, a0 in cantos:
        for i in range(n + 1):
            a = a0 + (math.pi / 2) * i / n
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return _limpa(pts)


def dshape(r, flat, n=48, cx=0.0, cy=0.0):
    """Secao do eixo D do 28BYJ-48: circulo de raio r cortado por dois planos
    paralelos distantes `flat` entre si."""
    h = flat / 2.0
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        x, y = r * math.cos(a), r * math.sin(a)
        y = max(-h, min(h, y))
        pts.append((cx + x, cy + y))
    return _limpa(pts)


def stadium(x0, x1, y, r, n=24):
    """Rasgo oblongo (dois semicirculos ligados por retas), eixo em X."""
    pts = []
    for i in range(n + 1):
        a = -math.pi / 2 + math.pi * i / n
        pts.append((x1 + r * math.cos(a), y + r * math.sin(a)))
    for i in range(n + 1):
        a = math.pi / 2 + math.pi * i / n
        pts.append((x0 + r * math.cos(a), y + r * math.sin(a)))
    return _limpa(pts)


def arc_slot(rad, a0, a1, w, n=20, cx=0.0, cy=0.0):
    """Rasgo em arco de largura w centrado no raio `rad`, de a0 a a1 (rad)."""
    ri, ro = rad - w / 2.0, rad + w / 2.0
    pts = []
    for i in range(n + 1):
        a = a0 + (a1 - a0) * i / n
        pts.append((cx + ro * math.cos(a), cy + ro * math.sin(a)))
    for i in range(n + 1):
        a = a1 + (a0 - a1) * i / n
        pts.append((cx + ri * math.cos(a), cy + ri * math.sin(a)))
    return _limpa(pts)


def _limpa(pts, tol=1e-7):
    """Remove vertices repetidos consecutivos (inclusive o fecho do anel)."""
    out = []
    for p in pts:
        if not out or abs(p[0] - out[-1][0]) > tol or abs(p[1] - out[-1][1]) > tol:
            out.append(p)
    while len(out) > 2 and abs(out[0][0] - out[-1][0]) < tol and abs(out[0][1] - out[-1][1]) < tol:
        out.pop()
    return out


def signed_area(poly):
    s = 0.0
    n = len(poly)
    for i in range(n):
        x0, y0 = poly[i]
        x1, y1 = poly[(i + 1) % n]
        s += x0 * y1 - x1 * y0
    return s / 2.0


def ccw(poly):
    return poly if signed_area(poly) > 0 else poly[::-1]


def cw(poly):
    return poly if signed_area(poly) < 0 else poly[::-1]


# ------------------------------------------------------- triangulacao 2D


def _orient(p, q, r):
    v = (q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0])
    if abs(v) < 1e-12:
        return 0
    return 1 if v > 0 else -1


def _seg_cruza(a, b, c, d):
    """Cruzamento proprio (ignora toques em extremidade)."""
    o1, o2 = _orient(a, b, c), _orient(a, b, d)
    o3, o4 = _orient(c, d, a), _orient(c, d, b)
    return 0 not in (o1, o2, o3, o4) and o1 != o2 and o3 != o4


def _visivel(aneis, p, hp):
    for anel in aneis:
        n = len(anel)
        for i in range(n):
            a, b = anel[i], anel[(i + 1) % n]
            if _seg_cruza(p, hp, a, b):
                return False
    return True


def _no_triangulo(p, a, b, c):
    """Contencao inclusiva, independente da orientacao do triangulo."""
    d1, d2, d3 = _orient(a, b, p), _orient(b, c, p), _orient(c, a, p)
    neg = d1 < 0 or d2 < 0 or d3 < 0
    pos = d1 > 0 or d2 > 0 or d3 > 0
    return not (neg and pos)


def _bridge(outline, hole):
    """Liga um furo ao contorno por uma ponte de ida e volta, devolvendo um
    unico poligono simples.

    Metodo classico (Eberly): parte do vertice do furo mais a direita,
    lanca um raio em +X, acha a aresta do contorno mais proxima e usa o
    vertice dela de maior X; se algum vertice reflexo cair dentro do
    triangulo formado, usa o de menor angulo. Isso garante uma ponte que
    nao cruza nada, inclusive quando ha furos colineares.
    """
    hi = max(range(len(hole)), key=lambda i: hole[i][0])
    mp = hole[hi]
    n = len(outline)

    melhor_x, melhor_e = None, None
    for i in range(n):
        a, b = outline[i], outline[(i + 1) % n]
        if abs(a[1] - b[1]) < 1e-12:
            continue
        if (a[1] - mp[1]) * (b[1] - mp[1]) > 0:
            continue
        t = (mp[1] - a[1]) / (b[1] - a[1])
        x = a[0] + t * (b[0] - a[0])
        if x < mp[0] - 1e-9:
            continue
        if melhor_x is None or x < melhor_x:
            melhor_x, melhor_e = x, i

    if melhor_e is None:
        alvo = min(range(n), key=lambda i: (outline[i][0] - mp[0]) ** 2
                   + (outline[i][1] - mp[1]) ** 2)
    else:
        ip = (melhor_x, mp[1])
        a, b = outline[melhor_e], outline[(melhor_e + 1) % n]
        pi = melhor_e if a[0] >= b[0] else (melhor_e + 1) % n
        pp = outline[pi]
        alvo, melhor_chave = None, None
        for j in range(n):
            if j == pi:
                continue
            r = outline[j]
            if _orient(outline[j - 1], r, outline[(j + 1) % n]) > 0:
                continue                       # convexo estrito: nao atrapalha
            # colineares entram na disputa: sao os vertices das pontes ja
            # existentes, que o teste estrito deixaria passar batido.
            if not _no_triangulo(r, mp, ip, pp):
                continue
            dx, dy = r[0] - mp[0], r[1] - mp[1]
            chave = (abs(math.atan2(dy, dx)), dx * dx + dy * dy)
            if melhor_chave is None or chave < melhor_chave:
                melhor_chave, alvo = chave, j

        if alvo is None:
            # Nenhum vertice reflexo atrapalha: a ponte vai direto ao ponto
            # onde o raio corta a aresta. Inserir esse ponto como vertice
            # novo evita que varias pontes se acumulem no mesmo canto, que e
            # o que trava o ear clipping.
            if abs(a[0] - ip[0]) < 1e-9 and abs(a[1] - ip[1]) < 1e-9:
                alvo = melhor_e
            elif abs(b[0] - ip[0]) < 1e-9 and abs(b[1] - ip[1]) < 1e-9:
                alvo = (melhor_e + 1) % n
            else:
                outline = outline[:melhor_e + 1] + [ip] + outline[melhor_e + 1:]
                alvo = melhor_e + 1

    furo = hole[hi:] + hole[:hi]
    return outline[:alvo + 1] + furo + [furo[0]] + outline[alvo:]


def _dentro(p, a, b, c):
    return (_orient(a, b, p) > 0 and _orient(b, c, p) > 0 and _orient(c, a, p) > 0)


def triangulate(outline, holes=()):
    """Ear clipping sobre contorno CCW com furos CW. Devolve triangulos 2D."""
    poly = list(ccw(list(outline)))
    furos = sorted((list(cw(list(h))) for h in holes),
                   key=lambda h: -max(p[0] for p in h))
    for h in furos:
        poly = _bridge(poly, h)

    tris = []
    idx = list(range(len(poly)))
    guarda = 0
    limite = 40 * len(poly) + 1000
    while len(idx) > 3 and guarda < limite:
        guarda += 1
        orelha = False
        for k in range(len(idx)):
            i0, i1, i2 = idx[k - 1], idx[k], idx[(k + 1) % len(idx)]
            a, b, c = poly[i0], poly[i1], poly[i2]
            if _orient(a, b, c) <= 0:          # reflexo ou degenerado
                continue
            if any(_dentro(poly[j], a, b, c) for j in idx if j not in (i0, i1, i2)):
                continue
            tris.append((a, b, c))
            idx.pop(k)
            orelha = True
            break
        if not orelha:
            # Sem orelha propria: descarta um vertice degenerado (colinear ou
            # repetido), o caso tipico dos vertices criados pelas pontes.
            for k in range(len(idx)):
                i0, i1, i2 = idx[k - 1], idx[k], idx[(k + 1) % len(idx)]
                if _orient(poly[i0], poly[i1], poly[i2]) == 0:
                    idx.pop(k)
                    orelha = True
                    break
        if not orelha:                          # poligono travado
            raise RuntimeError('triangulacao travada com %d vertices' % len(idx))
    if len(idx) == 3:
        tris.append((poly[idx[0]], poly[idx[1]], poly[idx[2]]))
    return tris


# ------------------------------------------------------------ solidos 3D


def _refina(anel, pontos, tol=1e-9):
    """Insere em `anel` os pontos de `pontos` que caiam no interior de uma
    aresta. As pontes da triangulacao criam vertices novos sobre os aneis;
    sem este refinamento a tampa e a parede lateral ficariam com juncoes em
    T e a malha nao fecharia."""
    out = []
    n = len(anel)
    for i in range(n):
        a, b = anel[i], anel[(i + 1) % n]
        out.append(a)
        dx, dy = b[0] - a[0], b[1] - a[1]
        comp = dx * dx + dy * dy
        if comp < tol:
            continue
        meio = []
        for p in pontos:
            t = ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / comp
            if t <= 1e-9 or t >= 1.0 - 1e-9:
                continue
            ex, ey = a[0] + t * dx - p[0], a[1] + t * dy - p[1]
            if ex * ex + ey * ey <= tol:
                meio.append((t, p))
        meio.sort(key=lambda it: it[0])
        out.extend(p for _, p in meio)
    return out


def prism(outline, z0, z1, holes=()):
    """Extrusao de uma regiao 2D entre z0 e z1. Devolve lista de triangulos."""
    out = ccw(list(outline))
    hs = [cw(list(h)) for h in holes]
    tapa = triangulate(out, hs)
    if hs:
        pontos = {p for t in tapa for p in t}
        out = _refina(out, pontos)
        hs = [_refina(h, pontos) for h in hs]
    m = []
    for a, b, c in tapa:                        # topo (normal +Z)
        m.append(((a[0], a[1], z1), (b[0], b[1], z1), (c[0], c[1], z1)))
    for a, b, c in tapa:                        # base (normal -Z)
        m.append(((a[0], a[1], z0), (c[0], c[1], z0), (b[0], b[1], z0)))
    # Os furos ja vem com sentido invertido (CW), entao o mesmo winding do
    # contorno externo produz normais apontando para dentro do furo.
    for anel in [out] + hs:
        n = len(anel)
        for i in range(n):
            p, q = anel[i], anel[(i + 1) % n]
            m.append(((p[0], p[1], z0), (q[0], q[1], z0), (q[0], q[1], z1)))
            m.append(((p[0], p[1], z0), (q[0], q[1], z1), (p[0], p[1], z1)))
    return m


def flip(mesh):
    """Inverte as normais da malha."""
    return [(t[0], t[2], t[1]) for t in mesh]


def _orienta(mesh):
    """Garante volume positivo (normais para fora)."""
    return mesh if volume(mesh) >= 0 else flip(mesh)


def prism_x(profile_yz, x0, x1, holes=()):
    """Extrusao ao longo de X de um perfil dado no plano YZ.
    A permutacao (y,z,x) -> (x,y,z) e ciclica, logo preserva a orientacao."""
    m = prism(profile_yz, x0, x1, holes)
    return [tuple((p[2], p[0], p[1]) for p in t) for t in m]


def prism_y(profile_xz, y0, y1, holes=()):
    """Extrusao ao longo de Y de um perfil dado no plano XZ.
    A troca (x,z) -> (x,z) com Y como altura inverte a mao, por isso o
    winding de cada triangulo tambem e invertido."""
    m = prism(profile_xz, y0, y1, holes)
    out = []
    for t in m:
        a, b, c = ((p[0], p[2], p[1]) for p in t)
        out.append((a, c, b))
    return out


def box(x0, y0, z0, x1, y1, z1):
    return prism(rect(x0, y0, x1, y1), z0, z1)


def cyl(r, z0, z1, n=64, cx=0.0, cy=0.0, holes=()):
    return prism(circle(r, n, cx, cy), z0, z1, holes)


def cone(r0, r1, z0, z1, n=64, cx=0.0, cy=0.0):
    """Tronco de cone macico (usado em chanfros de primeira camada)."""
    a = circle(r0, n, cx, cy)
    b = circle(r1, n, cx, cy)
    m = []
    for i in range(n):
        p0, p1 = a[i], a[(i + 1) % n]
        q0, q1 = b[i], b[(i + 1) % n]
        m.append(((p0[0], p0[1], z0), (p1[0], p1[1], z0), (q1[0], q1[1], z1)))
        m.append(((p0[0], p0[1], z0), (q1[0], q1[1], z1), (q0[0], q0[1], z1)))
    c0, c1 = (cx, cy, z0), (cx, cy, z1)
    for i in range(n):
        p0, p1 = a[i], a[(i + 1) % n]
        q0, q1 = b[i], b[(i + 1) % n]
        m.append((c0, (p1[0], p1[1], z0), (p0[0], p0[1], z0)))
        m.append((c1, (q0[0], q0[1], z1), (q1[0], q1[1], z1)))
    return m


def wedge(p0, p1, p2, esp):
    """Prisma triangular de espessura `esp` em Y, triangulo no plano XZ.
    p* sao pares (x, z); o solido vai de y=0 a y=esp."""
    tri = [p0, p1, p2]
    if signed_area(tri) < 0:
        tri = tri[::-1]
    (ax, az), (bx, bz), (cx_, cz) = tri
    A0, B0, C0 = (ax, 0.0, az), (bx, 0.0, bz), (cx_, 0.0, cz)
    A1, B1, C1 = (ax, esp, az), (bx, esp, bz), (cx_, esp, cz)
    m = [(A0, C0, B0), (A1, B1, C1)]
    for u0, v0, u1, v1 in [(A0, B0, A1, B1), (B0, C0, B1, C1), (C0, A0, C1, A1)]:
        m.append((u0, v0, v1))
        m.append((u0, v1, u1))
    return _orienta(m)


# ------------------------------------------------------ transformacoes


def translate(mesh, dx=0.0, dy=0.0, dz=0.0):
    return [tuple((p[0] + dx, p[1] + dy, p[2] + dz) for p in t) for t in mesh]


def _rot(mesh, eixo, ang):
    c, s = math.cos(ang), math.sin(ang)

    def f(p):
        x, y, z = p
        if eixo == 'x':
            return (x, y * c - z * s, y * s + z * c)
        if eixo == 'y':
            return (x * c + z * s, y, -x * s + z * c)
        return (x * c - y * s, x * s + y * c, z)
    return [tuple(f(p) for p in t) for t in mesh]


def rot_x(mesh, graus):
    return _rot(mesh, 'x', math.radians(graus))


def rot_y(mesh, graus):
    return _rot(mesh, 'y', math.radians(graus))


def rot_z(mesh, graus):
    return _rot(mesh, 'z', math.radians(graus))


def mirror_x(mesh):
    """Espelha em X invertendo a ordem dos vertices para manter as normais."""
    return [((-t[0][0], t[0][1], t[0][2]),
             (-t[2][0], t[2][1], t[2][2]),
             (-t[1][0], t[1][1], t[1][2])) for t in mesh]


def bbox(mesh):
    xs = [p[0] for t in mesh for p in t]
    ys = [p[1] for t in mesh for p in t]
    zs = [p[2] for t in mesh for p in t]
    return (min(xs), min(ys), min(zs), max(xs), max(ys), max(zs))


def volume(mesh):
    """Volume assinado (mm3) pelo teorema da divergencia."""
    v = 0.0
    for a, b, c in mesh:
        v += (a[0] * (b[1] * c[2] - c[1] * b[2])
              - a[1] * (b[0] * c[2] - c[0] * b[2])
              + a[2] * (b[0] * c[1] - c[0] * b[1])) / 6.0
    return v


def watertight(mesh, tol=4):
    """Conta arestas sem par (malha fechada => 0). Usa arredondamento `tol`."""
    from collections import Counter
    c = Counter()
    for t in mesh:
        for i in range(3):
            a = tuple(round(v, tol) for v in t[i])
            b = tuple(round(v, tol) for v in t[(i + 1) % 3])
            c[(a, b)] += 1
    abertas = 0
    for (a, b), n in c.items():
        if c.get((b, a), 0) != n:
            abertas += 1
    return abertas


# ---------------------------------------------------------------- STL


def write_stl(mesh, caminho, cabecalho=b'RadarTorres'):
    with open(caminho, 'wb') as f:
        f.write(cabecalho.ljust(80, b' ')[:80])
        f.write(struct.pack('<I', len(mesh)))
        for a, b, c in mesh:
            ux, uy, uz = b[0] - a[0], b[1] - a[1], b[2] - a[2]
            vx, vy, vz = c[0] - a[0], c[1] - a[1], c[2] - a[2]
            nx = uy * vz - uz * vy
            ny = uz * vx - ux * vz
            nz = ux * vy - uy * vx
            m = math.sqrt(nx * nx + ny * ny + nz * nz)
            if m > 1e-12:
                nx, ny, nz = nx / m, ny / m, nz / m
            else:
                nx = ny = nz = 0.0
            f.write(struct.pack('<12fH', nx, ny, nz,
                                a[0], a[1], a[2], b[0], b[1], b[2],
                                c[0], c[1], c[2], 0))
