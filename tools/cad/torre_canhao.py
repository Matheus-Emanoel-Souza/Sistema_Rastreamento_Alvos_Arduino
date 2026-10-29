"""Gerador parametrico do conjunto acima do rolamento da torre do canhao.

Pecas geradas (revisao v2):
    01  montante do motor do tilt
    02  montante do apoio do pivo
    03  berco do tilt em U
    04  suporte do laser
    05  plataforma giratoria

Referencial de montagem: origem no centro da plataforma, Z=0 na face
superior do deck (onde os montantes se apoiam), Z para cima, eixo de tilt
paralelo a X, frente da camera em +Y.

Uso:
    python torre_canhao.py            # gera STLs, placa de impressao e viewer
    python torre_canhao.py --relatorio  # so imprime o relatorio dimensional
"""

import math
import os
import sys

import meshlib as M

# ===================================================================
# PARAMETROS
# ===================================================================

N = 64                      # segmentos por circunferencia completa

# --- componentes comerciais ---------------------------------------
ROL_OD, ROL_H = 60.0, 13.0  # rolamento axial 51108
SEDE_D = 60.4               # alojamento da arruela girante do rolamento
SEDE_PROF = 3.0

EIXO_D, EIXO_FLAT = 5.2, 3.2    # eixo D do 28BYJ-48 (com folga)
EIXO_SAI = 10.0                 # quanto o eixo sai alem da face de fixacao
MOT_FUROS = 35.0                # distancia entre os furos das orelhas
MOT_FURO_D = 4.2

M3_PASS, M3_ROSCA = 3.4, 2.9
M3_PORCA_CHAVE = 5.8            # entre faces da porca M3 + folga

CAM_L, CAM_P, CAM_H = 88.0, 55.0, 30.0   # webcam deitada no berco
LASER_D = 12.0
LASER_APERTO = 0.3              # interferencia radial da braadeira

# --- plataforma (05) ----------------------------------------------
PLAT_R = 64.0
PLAT_ESP = 6.0
NERVURA_H = 4.0                 # nervuras e aro no lado de baixo
NERVURA_L = 3.0
ARO_L = 3.0
CUBO_D = 16.0                   # cubo que recebe o eixo do motor do pan
CUBO_SAI = 12.0
FIO_R, FIO_L = 14.0, 7.0        # rasgo em arco para a fiacao

# --- montantes (01 e 02) ------------------------------------------
PL_ESP = 6.0                    # espessura da placa (era 4 mm)
PL_LARG = 30.0
PE_ESP = 5.0                    # espessura do pe
PE_X0 = 43.0                    # borda interna do pe
PE_Y = 16.0                     # meia largura do pe
PE_R = 62.0                     # borda externa acompanha o disco
H_EIXO = 50.5                   # altura do eixo de tilt sobre o deck
PL_H_MOTOR = 72.0
PL_H_PIVO = 62.0
GUSSET_ESP = 4.0
GUSSET_Y = (10.0, 14.0)         # faixa em Y de cada nervura
GUSSET_H = 34.0                 # altura junto a placa
GUSSET_X_LIVRE = 51.0           # a partir daqui pode subir: fora do berco

# --- berco (03) ---------------------------------------------------
BER_X = 50.0                    # meia largura externa
BER_PAR = 4.0                   # espessura das paredes laterais
BER_Y = 35.0                    # meia profundidade
BER_H = 48.0                    # altura da parede
BER_PISO = 5.0                  # espessura do piso
BER_CHANF_Y = 30.0              # inicio do chanfro inferior
BER_CHANF_Z = 8.0               # altura do chanfro
BER_FUNDO_Y = 30.0              # meia largura do piso plano
MUNHAO_D = 16.0                 # cubo do eixo de tilt
MUNHAO_X = 56.6                 # face externa do cubo
NARIZ_D = 9.8                   # nariz que faz de mancal na placa
COLAR_D = 24.0                  # colar de reforco do cubo
TRAS_ESP = 4.0                  # parede traseira
TRAS_H = 34.0
TILT_MAX = 25.0                 # curso de tilt considerado no envelope

# --- suporte do laser (04) ----------------------------------------
LS_L, LS_P, LS_BASE = 20.0, 30.0, 4.0
LS_Z_FURO = 15.0                # altura do furo do laser sobre a base
LS_ALT = 26.0                   # altura total do corpo da braadeira
LS_FENDA = 2.0                  # fenda da braadeira
LS_FUROS_Y = (5.0, 25.0)

# --- massas estimadas para o balanceamento ------------------------
DENS_PLA = 1.24e-3              # g/mm3
MASSA_CAM = 60.0                # g, webcam
MASSA_LASER = 12.0              # g, modulo laser

# --- derivados -----------------------------------------------------
PL_X0 = MUNHAO_X + 0.6          # face interna da placa (folga no cubo)
PL_X1 = PL_X0 + PL_ESP
NARIZ_X1 = PL_X1 - 1.5          # o nariz para pouco antes da face externa
MANCAL_D = NARIZ_D + 0.4        # furo da placa que recebe o nariz


# ===================================================================
# PERFIS AUXILIARES
# ===================================================================

def _hexagono(chave, cx, cy):
    """Bolso sextavado para porca M3 (chave = distancia entre faces)."""
    r = chave / 2.0 / math.cos(math.pi / 6)
    return [(cx + r * math.cos(math.pi / 6 + i * math.pi / 3),
             cy + r * math.sin(math.pi / 6 + i * math.pi / 3)) for i in range(6)]


def _pe_contorno():
    """Contorno do pe do montante: reto por dentro, arco acompanhando o disco."""
    pts = [(PE_X0, -PE_Y), (PE_X0, PE_Y)]
    a_lim = math.asin(PE_Y / PE_R)
    passos = 16
    for i in range(passos + 1):               # arco de +a_lim ate -a_lim
        a = a_lim - 2 * a_lim * i / passos
        pts.append((PE_R * math.cos(a), PE_R * math.sin(a)))
    return M._limpa(pts)


def furos_pe():
    """Posicoes (x, y) dos tres parafusos de cada montante."""
    return [(PE_X0 + 3.5, 0.0), (57.0, 12.0), (57.0, -12.0)]


# ===================================================================
# 05 - PLATAFORMA GIRATORIA
# ===================================================================

def peca_plataforma():
    m = []
    furos_xy = []
    for lado in (1, -1):
        for x, y in furos_pe():
            furos_xy.append((lado * x, lado * y))

    # rasgo em arco para a fiacao (passa por dentro do furo do rolamento)
    rasgo = M.arc_slot(FIO_R, math.radians(95), math.radians(265), FIO_L, 24)

    # anel inferior: aloja o rolamento e guarda as porcas M3
    bolsos = [_hexagono(M3_PORCA_CHAVE, x, y) for x, y in furos_xy]
    m += M.prism(M.circle(PLAT_R, N), -PLAT_ESP, -SEDE_PROF,
                 [M.circle(SEDE_D / 2.0, N)] + bolsos)

    # disco superior: deck onde os montantes se apoiam
    passagens = [M.circle(M3_PASS / 2.0, 20, x, y) for x, y in furos_xy]
    m += M.prism(M.circle(PLAT_R, N), -SEDE_PROF, 0.0, passagens + [rasgo])

    # cubo do eixo do motor do pan (soquete em D)
    m += M.prism(M.circle(CUBO_D / 2.0, 40), -PLAT_ESP - CUBO_SAI, -SEDE_PROF,
                 [M.dshape(EIXO_D / 2.0, EIXO_FLAT, 40)])

    # aro perimetral e nervuras radiais, ambos no lado de baixo
    z0, z1 = -PLAT_ESP - NERVURA_H, -PLAT_ESP
    m += M.prism(M.circle(PLAT_R, N), z0, z1,
                 [M.circle(PLAT_R - ARO_L, N)])
    for g in (40, 70, 110, 140, 220, 250, 290, 320):
        a = math.radians(g)
        ca, sa = math.cos(a), math.sin(a)
        h = NERVURA_L / 2.0
        r0, r1 = SEDE_D / 2.0 + 5.0, PLAT_R - ARO_L
        quad = [(r0 * ca + h * sa, r0 * sa - h * ca),
                (r1 * ca + h * sa, r1 * sa - h * ca),
                (r1 * ca - h * sa, r1 * sa + h * ca),
                (r0 * ca - h * sa, r0 * sa + h * ca)]
        m += M.prism(quad, z0, z1)
    return m


# ===================================================================
# 01 / 02 - MONTANTES
# ===================================================================

def peca_montante(tipo):
    """tipo: 'motor' (recebe o 28BYJ-48) ou 'pivo' (apoio livre)."""
    altura = PL_H_MOTOR if tipo == 'motor' else PL_H_PIVO
    m = []

    # pe apoiado no deck, com tres parafusos M3
    m += M.prism(_pe_contorno(), 0.0, PE_ESP,
                 [M.circle(M3_PASS / 2.0, 20, x, y) for x, y in furos_pe()])

    # placa vertical; o furo central e o mancal do nariz do berco
    furos = [M.circle(MANCAL_D / 2.0, 36, 0.0, H_EIXO)]
    if tipo == 'motor':
        for dz in (-MOT_FUROS / 2.0, MOT_FUROS / 2.0):
            furos.append(M.circle(MOT_FURO_D / 2.0, 20, 0.0, H_EIXO + dz))
    m += M.prism_x(M.rrect(-PL_LARG / 2.0, 0.0, PL_LARG / 2.0, altura, 8.0),
                   PL_X0, PL_X1, furos)

    # nervuras de raiz: sobem rente a placa e descem antes do berco
    perfil = [(PE_X0, PE_ESP), (PL_X0, PE_ESP), (PL_X0, GUSSET_H),
              (GUSSET_X_LIVRE, GUSSET_H), (GUSSET_X_LIVRE, 14.0)]
    for sinal in (1, -1):
        y0, y1 = sorted((sinal * GUSSET_Y[0], sinal * GUSSET_Y[1]))
        m += M.prism_y(perfil, y0, y1)
    return m


# ===================================================================
# 03 - BERCO DO TILT
# ===================================================================

def _perfil_parede():
    """Secao da parede lateral no plano YZ, com os cantos inferiores
    chanfrados para reduzir o envelope varrido pelo tilt."""
    return [(-BER_CHANF_Y, 0.0), (BER_CHANF_Y, 0.0),
            (BER_Y, BER_CHANF_Z), (BER_Y, BER_H),
            (-BER_Y, BER_H), (-BER_Y, BER_CHANF_Z)]


def _perfil_saia(sinal):
    """Saia chanfrada do piso, de BER_FUNDO_Y ate BER_Y."""
    y0, y1 = sinal * BER_FUNDO_Y, sinal * BER_Y
    return [(y0, 0.0), (y1, BER_CHANF_Z),
            (y1, BER_CHANF_Z + BER_PISO), (y0, BER_PISO)]


def peca_berco(pivo_z):
    """Berco com o eixo de tilt a `pivo_z` acima da face inferior do piso."""
    m = []
    xi = BER_X - BER_PAR                      # face interna das paredes

    # paredes laterais
    for sinal in (1, -1):
        x0, x1 = sorted((sinal * xi, sinal * BER_X))
        m += M.prism_x(_perfil_parede(), x0, x1)

    # piso plano + saias chanfradas
    m += M.prism(M.rect(-xi, -BER_FUNDO_Y, xi, BER_FUNDO_Y), 0.0, BER_PISO,
                 [M.circle(M3_PASS / 2.0, 20, sx * 10.0, sy * 18.0)
                  for sx in (1, -1) for sy in (1, -1)]
                 + [M.stadium(-4.0, 4.0, sy * 26.0, 1.6, 12) for sy in (1, -1)])
    for sinal in (1, -1):
        m += M.prism_x(_perfil_saia(sinal), -xi, xi)

    # parede traseira: amarra as duas laterais e leva o trilho do contrapeso
    m += M.prism_y(M.rect(-xi, BER_CHANF_Z, xi, TRAS_H), -BER_Y, -BER_Y + TRAS_ESP,
                   [M.circle(M3_PASS / 2.0, 20, dx, 24.0) for dx in (-14.0, 0.0, 14.0)])

    # munhoes: cubo + colar de reforco + nariz que faz de mancal na placa
    for sinal in (1, -1):
        if sinal > 0:
            furo = M.circle(M3_ROSCA / 2.0, 20, 0.0, pivo_z)
        else:
            furo = M.dshape(EIXO_D / 2.0, EIXO_FLAT, 40, 0.0, pivo_z)
        for d, xa, xb in ((COLAR_D, BER_X, BER_X + 3.0),
                          (MUNHAO_D, BER_X, MUNHAO_X),
                          (NARIZ_D, MUNHAO_X, NARIZ_X1)):
            x0, x1 = sorted((sinal * xa, sinal * xb))
            m += M.prism_x(M.circle(d / 2.0, 40, 0.0, pivo_z), x0, x1, [furo])

    # engrossamento da parede esquerda para receber o suporte do laser
    bx0, bx1 = -BER_X - 2.0, -xi + 2.0
    m += M.prism_y([(-BER_X, TRAS_H), (-xi, TRAS_H), (bx1, TRAS_H + 8.0),
                    (bx1, BER_H - LS_BASE), (bx0, BER_H - LS_BASE),
                    (bx0, TRAS_H + 8.0)],
                   LS_FUROS_Y[0] - 7.0, LS_FUROS_Y[1] + 7.0)
    m += M.prism(M.rect(bx0, LS_FUROS_Y[0] - 7.0, bx1, LS_FUROS_Y[1] + 7.0),
                 BER_H - LS_BASE, BER_H,
                 [M.circle(M3_ROSCA / 2.0, 20, (bx0 + bx1) / 2.0, y)
                  for y in LS_FUROS_Y])
    return m


# ===================================================================
# 04 - SUPORTE DO LASER
# ===================================================================

def _perfil_bracadeira():
    """Secao da braadeira no plano XZ: bloco com rasgo em fechadura."""
    r = (LASER_D - LASER_APERTO) / 2.0
    cx, cz = LS_L / 2.0, LS_BASE + LS_Z_FURO
    meia = LS_FENDA / 2.0
    dz = math.sqrt(max(r * r - meia * meia, 1e-6))
    a_dir = math.atan2(dz, meia)              # saida direita da fenda
    pts = [(0.0, LS_BASE), (LS_L, LS_BASE), (LS_L, LS_ALT + LS_BASE),
           (cx + meia, LS_ALT + LS_BASE), (cx + meia, cz + dz)]
    passos = 56
    a_fim = a_dir - 2 * math.pi + (math.pi - 2 * a_dir)
    for i in range(passos + 1):               # contorna o furo por baixo
        a = a_dir + (a_fim - a_dir) * i / passos
        pts.append((cx + r * math.cos(a), cz + r * math.sin(a)))
    pts += [(cx - meia, LS_ALT + LS_BASE), (0.0, LS_ALT + LS_BASE)]
    return M._limpa(pts)


def peca_laser():
    m = M.prism(M.rect(0.0, 0.0, LS_L, LS_P), 0.0, LS_BASE,
                [M.circle(M3_PASS / 2.0, 20, LS_L / 2.0, y) for y in LS_FUROS_Y])
    m += M.prism_y(_perfil_bracadeira(), 7.0, LS_P - 7.0)
    return m


# ===================================================================
# BALANCEAMENTO
# ===================================================================

def centro_massa(mesh):
    """Centroide do solido (mm) por decomposicao em tetraedros."""
    vt = 0.0
    cx = cy = cz = 0.0
    for a, b, c in mesh:
        v = (a[0] * (b[1] * c[2] - c[1] * b[2])
             - a[1] * (b[0] * c[2] - c[0] * b[2])
             + a[2] * (b[0] * c[1] - c[0] * b[1])) / 6.0
        vt += v
        cx += v * (a[0] + b[0] + c[0]) / 4.0
        cy += v * (a[1] + b[1] + c[1]) / 4.0
        cz += v * (a[2] + b[2] + c[2]) / 4.0
    if abs(vt) < 1e-9:
        return 0.0, 0.0, 0.0, 0.0
    return cx / vt, cy / vt, cz / vt, vt


def pivo_equilibrado():
    """Altura do eixo de tilt que equilibra berco + webcam + laser."""
    berco = peca_berco(20.0)                  # a altura do munhao nao muda a massa
    _, _, zb, vol = centro_massa(berco)
    mb = vol * DENS_PLA
    mc = MASSA_CAM
    zc = BER_PISO + CAM_H / 2.0
    ml = MASSA_LASER
    zl = BER_H + LS_BASE + LS_Z_FURO
    z = (mb * zb + mc * zc + ml * zl) / (mb + mc + ml)
    return z, mb, mc + ml


def envelope_tilt(pivo_z):
    """Menor cota (relativa ao pivo) varrida pelo berco em +-TILT_MAX."""
    pontos = list(_perfil_parede()) + [(y, z) for y, z in _perfil_saia(1)]
    pontos += [(-y, z) for y, z in _perfil_saia(1)]
    pontos += [(-BER_Y, TRAS_H), (BER_Y, TRAS_H)]
    pior = 0.0
    for y, z in pontos:
        dz = z - pivo_z
        r = math.hypot(y, dz)
        if r < 1e-9:
            continue
        t0 = math.atan2(dz, y)
        for i in range(81):
            a = math.radians(-TILT_MAX + 2 * TILT_MAX * i / 80.0)
            pior = min(pior, r * math.sin(t0 + a))
    return pior


# ===================================================================
# MONTAGEM E SAIDA
# ===================================================================

def pad_laser_x():
    """Centro em X do pad que recebe o suporte do laser."""
    return ((-BER_X - 2.0) + (-(BER_X - BER_PAR) + 2.0)) / 2.0


def conjunto(pivo_z):
    """Todas as pecas no referencial de montagem."""
    z_berco = H_EIXO - pivo_z
    berco = M.translate(peca_berco(pivo_z), 0.0, 0.0, z_berco)
    # o suporte do laser e desenhado com os furos em (LS_L/2, LS_FUROS_Y);
    # basta alinhar esses furos com os do pad da parede esquerda.
    laser = M.translate(peca_laser(), pad_laser_x() - LS_L / 2.0, 0.0,
                        z_berco + BER_H)
    return {
        '01_montante_motor_tilt_v2': M.mirror_x(peca_montante('motor')),
        '02_montante_apoio_pivo_v2': peca_montante('pivo'),
        '03_berco_tilt_U_v2': berco,
        '04_suporte_laser_v2': laser,
        '05_plataforma_giratoria_v2': peca_plataforma(),
    }


def _assenta(mesh):
    x0, y0, z0, _, _, _ = M.bbox(mesh)
    return M.translate(mesh, -x0, -y0, -z0)


def orienta_impressao(nome, mesh):
    """Coloca cada peca na posicao de impressao (sem suportes)."""
    if nome.startswith('05'):                 # deck na mesa, cubo para cima
        mesh = M.rot_x(mesh, 180.0)
    elif nome.startswith(('01', '02')):       # placa deitada na mesa
        mesh = M.rot_z(M.rot_y(mesh, -90.0), 90.0)
    return _assenta(mesh)


MESA = 200.0

# A plataforma (128 mm) e o berco (70 mm de profundidade) nao convivem na
# mesma mesa de 200 mm, entao o conjunto sai em duas bandejas.
PLACAS = {
    'PLACA_torre_canhao_v2_A_200x200': {
        '05_plataforma_giratoria_v2': (6.0, 6.0),
        '01_montante_motor_tilt_v2': (140.0, 6.0),
        '02_montante_apoio_pivo_v2': (140.0, 84.0),
        '04_suporte_laser_v2': (140.0, 152.0),
    },
    'PLACA_torre_canhao_v2_B_200x200': {
        '03_berco_tilt_U_v2': (6.0, 6.0),
    },
}


def placa_impressao(pecas, postos):
    """Arranjo de uma bandeja de impressao."""
    placa = []
    ocupacao = []
    for nome, (px, py) in postos.items():
        p = orienta_impressao(nome, pecas[nome])
        placa += M.translate(p, px, py, 0.0)
        _, _, _, x1, y1, z1 = M.bbox(p)
        ocupacao.append((nome, px, py, px + x1, py + y1, z1))
    return placa, ocupacao


VIEWER_PECAS = [
    ('05_plataforma_giratoria_v2', 'Plataforma giratoria (05)', '#1aa179', 'base'),
    ('01_montante_motor_tilt_v2', 'Montante do motor do tilt (01)', '#7b4fcf', 'base'),
    ('02_montante_apoio_pivo_v2', 'Montante do pivo (02)', '#d6529f', 'base'),
    ('03_berco_tilt_U_v2', 'Berco do tilt em U (03)', '#e07b24', 'tilt'),
    ('04_suporte_laser_v2', 'Suporte do laser (04)', '#d6b400', 'tilt'),
]


def escreve_viewer(pecas, pivo_z, caminho):
    """Gera um visualizador 3D autocontido (geometria embutida em base64)."""
    import base64
    import io

    blocos = []
    for nome, rotulo, cor, grupo in VIEWER_PECAS:
        buf = io.BytesIO()
        mesh = pecas[nome]
        buf.write(b'v2'.ljust(80, b' '))
        buf.write(__import__('struct').pack('<I', len(mesh)))
        for a, b, c in mesh:
            buf.write(__import__('struct').pack(
                '<12fH', 0.0, 0.0, 0.0, a[0], a[1], a[2], b[0], b[1], b[2],
                c[0], c[1], c[2], 0))
        blocos.append('  {nome:%s, rotulo:%s, cor:%s, grupo:%s, massa:%.1f, stl:"%s"}'
                      % (_js(nome), _js(rotulo), _js(cor), _js(grupo),
                         M.volume(mesh) * DENS_PLA,
                         base64.b64encode(buf.getvalue()).decode('ascii')))

    cotas = [
        ('Eixo de tilt sobre o deck', '%.1f mm' % H_EIXO),
        ('Pivo sobre o piso do berco', '%.1f mm (era 18,0)' % pivo_z),
        ('Curso de tilt verificado', '+-%.0f graus' % TILT_MAX),
        ('Cota mais baixa do berco', '%.1f mm sobre o deck'
         % (H_EIXO + envelope_tilt(pivo_z))),
        ('Plataforma', 'diam. %.0f x %.0f mm' % (2 * PLAT_R, PLAT_ESP)),
        ('Rolamento', 'axial 51108 (sede diam. %.1f)' % SEDE_D),
        ('Placa dos montantes', '%.0f mm (era 4,0)' % PL_ESP),
        ('Mancal do berco na placa', 'nariz diam. %.1f em furo %.1f'
         % (NARIZ_D, MANCAL_D)),
        ('Engate no eixo do 28BYJ-48', '%.1f mm' % (NARIZ_X1 - (PL_X1 - EIXO_SAI))),
    ]
    linhas_cotas = '\n'.join(
        '    <tr><th>%s</th><td>%s</td></tr>' % (k, v) for k, v in cotas)

    html = _VIEWER_HTML.replace('@@PECAS@@', ',\n'.join(blocos))
    html = html.replace('@@COTAS@@', linhas_cotas)
    html = html.replace('@@HEIXO@@', '%.4f' % H_EIXO)
    html = html.replace('@@TILTMAX@@', '%.1f' % TILT_MAX)
    html = html.replace('@@PLATR@@', '%.2f' % PLAT_R)
    html = html.replace('@@PLATESP@@', '%.2f' % PLAT_ESP)
    html = html.replace('@@CUBOSAI@@', '%.2f' % CUBO_SAI)
    html = html.replace('@@PIVOZ@@', '%.2f' % pivo_z)
    html = html.replace('@@CAM@@', '%.1f,%.1f,%.1f' % (CAM_L, CAM_P, CAM_H))
    html = html.replace('@@BERPISO@@', '%.2f' % BER_PISO)
    html = html.replace('@@BERH@@', '%.2f' % BER_H)
    html = html.replace('@@BERX@@', '%.2f' % BER_X)
    html = html.replace('@@PLX1@@', '%.2f' % PL_X1)
    html = html.replace('@@LASERX@@', '%.2f' % pad_laser_x())
    html = html.replace('@@LASERZ@@', '%.2f' % (BER_H + LS_BASE + LS_Z_FURO))
    html = html.replace('@@LASERD@@', '%.2f' % LASER_D)
    with open(caminho, 'w', encoding='utf-8') as f:
        f.write(html)


def _js(s):
    return '"%s"' % s.replace('\\', '\\\\').replace('"', '\\"')


def main():
    destino = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                           '..', '..', 'Docs', 'Projecao 3D')
    destino = os.path.normpath(destino)
    if not os.path.isdir(destino):            # pasta real tem acento
        alt = os.path.join(os.path.dirname(destino), 'Projeção 3D')
        if os.path.isdir(alt):
            destino = alt

    pivo_z, massa_berco, massa_acess = pivo_equilibrado()
    pivo_z = round(pivo_z, 1)
    folga = envelope_tilt(pivo_z)

    print('=' * 68)
    print('TORRE DO CANHAO - conjunto acima do rolamento (revisao v2)')
    print('=' * 68)
    print('Eixo de tilt .................. %.1f mm acima do deck' % H_EIXO)
    print('Pivo no berco ................. %.1f mm acima do piso (era 18,0)' % pivo_z)
    print('Massa berco impresso .......... %.0f g' % massa_berco)
    print('Massa webcam + laser .......... %.0f g' % massa_acess)
    print('Envelope do tilt (+-%.0f graus) . %.1f mm acima do deck'
          % (TILT_MAX, H_EIXO + folga))
    print('Folga ate a nervura do montante %.1f mm' % (H_EIXO + folga - 14.0))

    pecas = conjunto(pivo_z)
    print('-' * 68)
    print('%-34s %7s %8s %9s' % ('peca', 'tri', 'massa g', 'fechada'))
    for nome, mesh in pecas.items():
        vol = M.volume(mesh)
        abertas = M.watertight(mesh)
        print('%-34s %7d %8.1f %9s'
              % (nome, len(mesh), vol * DENS_PLA,
                 'sim' if abertas == 0 else '%d arestas' % abertas))
        M.write_stl(mesh, os.path.join(destino, nome + '.stl'),
                    nome.encode('ascii'))

    escreve_viewer(pecas, pivo_z, os.path.join(destino, 'torre_canhao_v2.html'))

    for nome_placa, postos in PLACAS.items():
        placa, ocupacao = placa_impressao(pecas, postos)
        M.write_stl(placa, os.path.join(destino, nome_placa + '.stl'),
                    nome_placa.encode('ascii'))
        print('-' * 68)
        print('%s (mesa %.0f x %.0f mm):' % (nome_placa, MESA, MESA))
        for nome, x0, y0, x1, y1, z1 in ocupacao:
            cabe = x1 <= MESA and y1 <= MESA
            print('  %-34s X %5.1f..%5.1f  Y %5.1f..%5.1f  h %5.1f%s'
                  % (nome, x0, x1, y0, y1, z1, '' if cabe else '  <-- NAO CABE'))
    print('-' * 68)
    print('Pasta de saida: %s' % destino)
    return pecas, pivo_z


_VIEWER_HTML = r"""<!doctype html>
<html lang="pt-br"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Torre do canhao - conjunto acima do rolamento (v2)</title>
<style>
  :root{--bg:#10161c;--pan:#19222b;--bd:#2b3946;--tx:#e8eef3;--mu:#92a4b4;--ac:#2ec4a0}
  *{box-sizing:border-box}
  body{margin:0;font:14px/1.5 "Segoe UI",system-ui,sans-serif;background:var(--bg);color:var(--tx);overflow:hidden}
  #app{display:flex;height:100vh}
  #lona{flex:1;min-width:0}
  aside{width:330px;flex:none;background:var(--pan);border-left:1px solid var(--bd);overflow-y:auto;padding:18px}
  h1{font-size:16px;margin:0 0 4px}
  h2{font-size:12px;text-transform:uppercase;letter-spacing:.08em;color:var(--mu);margin:22px 0 8px;font-weight:600}
  p.sub{margin:0 0 6px;color:var(--mu);font-size:12px}
  .item{display:flex;align-items:center;gap:9px;padding:5px 0;cursor:pointer}
  .item input{accent-color:var(--ac);cursor:pointer}
  .sw{width:13px;height:13px;border-radius:3px;flex:none}
  .item span.n{flex:1;font-size:13px}
  .item span.g{color:var(--mu);font-size:11px;font-variant-numeric:tabular-nums}
  label.ctl{display:block;margin:12px 0 2px;color:var(--mu);font-size:12px}
  label.ctl b{color:var(--tx);float:right;font-variant-numeric:tabular-nums}
  input[type=range]{width:100%;accent-color:var(--ac)}
  table{width:100%;border-collapse:collapse;font-size:12px}
  th{text-align:left;font-weight:400;color:var(--mu);padding:3px 0;vertical-align:top}
  td{text-align:right;padding:3px 0;font-variant-numeric:tabular-nums}
  .dica{color:var(--mu);font-size:11px;margin-top:16px;border-top:1px solid var(--bd);padding-top:10px}
</style></head><body>
<div id="app"><canvas id="lona"></canvas><aside>
  <h1>Torre do canhao &mdash; revisao v2</h1>
  <p class="sub">Conjunto acima do rolamento axial 51108.</p>

  <h2>Pecas impressas</h2>
  <div id="lista"></div>

  <h2>Movimento</h2>
  <label class="ctl">Tilt <b id="vTilt">0&deg;</b></label>
  <input type="range" id="tilt" min="-@@TILTMAX@@" max="@@TILTMAX@@" value="0" step="1">
  <label class="ctl">Pan <b id="vPan">0&deg;</b></label>
  <input type="range" id="pan" min="-180" max="180" value="0" step="1">
  <label class="ctl">Vista explodida <b id="vExp">0</b></label>
  <input type="range" id="exp" min="0" max="100" value="0" step="1">

  <label class="item"><input type="checkbox" id="ar"><span class="sw" style="background:#5b6b7a"></span><span class="n">Mostrar arestas</span></label>

  <h2>Componentes comerciais</h2>
  <div id="ghosts"></div>

  <h2>Cotas principais</h2>
  <table>@@COTAS@@</table>

  <p class="dica">Arraste para girar, roda do mouse para aproximar,
  botao direito para deslocar.</p>
</aside></div>
<script src="https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js"></script>
<script>
var PECAS=[
@@PECAS@@
];
var HEIXO=@@HEIXO@@, PIVOZ=@@PIVOZ@@, PLATR=@@PLATR@@, PLATESP=@@PLATESP@@;
var CUBOSAI=@@CUBOSAI@@, BERPISO=@@BERPISO@@, BERH=@@BERH@@, BERX=@@BERX@@;
var PLX1=@@PLX1@@, LASERX=@@LASERX@@, LASERZ=@@LASERZ@@, LASERD=@@LASERD@@;
var CAM=[@@CAM@@], ZBERCO=HEIXO-PIVOZ;

function stlGeom(b64){
  var bin=atob(b64), buf=new ArrayBuffer(bin.length), u=new Uint8Array(buf), i;
  for(i=0;i<bin.length;i++) u[i]=bin.charCodeAt(i);
  var dv=new DataView(buf), n=dv.getUint32(80,true), pos=new Float32Array(n*9), o=84, k=0;
  for(i=0;i<n;i++){
    o+=12;
    for(var j=0;j<9;j++){ pos[k++]=dv.getFloat32(o,true); o+=4; }
    o+=2;
  }
  var g=new THREE.BufferGeometry();
  g.setAttribute('position',new THREE.BufferAttribute(pos,3));
  g.computeVertexNormals();
  return g;
}

var cena=new THREE.Scene();
cena.background=new THREE.Color(0x10161c);
var lona=document.getElementById('lona');
var rend=new THREE.WebGLRenderer({canvas:lona,antialias:true});
var cam=new THREE.PerspectiveCamera(42,1,1,2000);
cam.up.set(0,0,1);
cena.add(new THREE.AmbientLight(0xffffff,0.55));
var l1=new THREE.DirectionalLight(0xffffff,0.75); l1.position.set(160,-200,260); cena.add(l1);
var l2=new THREE.DirectionalLight(0xffffff,0.35); l2.position.set(-180,160,60); cena.add(l2);

var raiz=new THREE.Group(); cena.add(raiz);        // tudo
var fixo=new THREE.Group(); raiz.add(fixo);        // abaixo do rolamento
var base=new THREE.Group(); raiz.add(base);        // gira no pan
var tilt=new THREE.Group(); tilt.position.set(0,0,HEIXO); base.add(tilt);
// grupo interno compensa a origem, para que as pecas continuem com as
// coordenadas do modelo e o tilt gire em torno do eixo real
var tiltIn=new THREE.Group(); tiltIn.position.set(0,0,-HEIXO); tilt.add(tiltIn);

var arestas=[];                                     // criadas sob demanda
function malha(geo,cor,op){
  var mat=new THREE.MeshPhongMaterial({color:new THREE.Color(cor),
    flatShading:true,shininess:12,transparent:op<1,opacity:op});
  var m=new THREE.Mesh(geo,mat);
  m.userData.geo=geo;
  return m;
}
function mostraArestas(lig){
  if(lig && !arestas.length){
    // EdgesGeometry e caro: so e calculado na primeira vez que se pede
    [].concat(objetos.map(function(o){return o.obj;}),
              ghosts.map(function(g){return g.obj;})).forEach(function(m){
      var e=new THREE.LineSegments(new THREE.EdgesGeometry(m.userData.geo,30),
          new THREE.LineBasicMaterial({color:0x0b1116,transparent:true,opacity:.4}));
      m.add(e); arestas.push(e);
    });
  }
  arestas.forEach(function(e){e.visible=lig;});
}
function cil(r,h,cor,op){
  var g=new THREE.CylinderGeometry(r,r,h,40);
  g.rotateX(Math.PI/2);                             // eixo do cilindro em Z
  return malha(g,cor,op);
}

// ---- pecas impressas -------------------------------------------------
var objetos=[];
PECAS.forEach(function(p){
  var m=malha(stlGeom(p.stl),p.cor,1);
  (p.grupo==='tilt'?tiltIn:base).add(m);
  var d=[0,0,1];
  if(p.nome.indexOf('01')===0) d=[-1,0,0];
  else if(p.nome.indexOf('02')===0) d=[1,0,0];
  else if(p.nome.indexOf('03')===0) d=[0,0,1];
  else if(p.nome.indexOf('04')===0) d=[-0.6,0,1.4];
  else d=[0,0,-0.4];
  objetos.push({obj:m,dir:d,def:p});
});

// ---- componentes comerciais (referencia) -----------------------------
var ghosts=[];
function ghost(rotulo,obj,dir){ ghosts.push({rotulo:rotulo,obj:obj,dir:dir||[0,0,0]}); }

var zRolTopo=-PLATESP+3, zRolBase=zRolTopo-13;      // 51108: 13 mm de altura
var rol=cil(30,13,0xd9a441,1); rol.position.z=(zRolTopo+zRolBase)/2;
fixo.add(rol); ghost('Rolamento axial 51108',rol,[0,0,-1]);

var copo=cil(35,34,0x2f7fb5,0.45); copo.position.z=zRolBase-17;
fixo.add(copo); ghost('Copo do rolamento (06)',copo,[0,0,-1.6]);

var mp=cil(14,19,0x3a4653,1); mp.position.z=zRolBase-10;
fixo.add(mp); ghost('Motor do pan (28BYJ-48)',mp,[0,0,-2.4]);

var eixoPan=cil(2.5,CUBOSAI+6,0xb9c6d2,1);
eixoPan.position.z=-PLATESP-CUBOSAI/2;
fixo.add(eixoPan); ghost('Eixo do pan',eixoPan,[0,0,-2]);

var gm=new THREE.CylinderGeometry(14,14,19,40); gm.rotateZ(Math.PI/2);
var mt=malha(gm,0x6b4a3a,1);
mt.position.set(-PLX1-9.5,0,HEIXO);
base.add(mt); ghost('Motor do tilt (28BYJ-48)',mt,[-1.8,0,0]);

var web=malha(new THREE.BoxGeometry(CAM[0],CAM[1],CAM[2]),0x1b1f24,1);
web.position.set(0,0,ZBERCO+BERPISO+CAM[2]/2);
tiltIn.add(web); ghost('Webcam '+CAM[0]+'x'+CAM[1]+'x'+CAM[2]+' mm',web,[0,0,1.6]);

var gl=new THREE.CylinderGeometry(LASERD/2,LASERD/2,40,32);
var las=malha(gl,0xd63b2f,1);
las.position.set(LASERX,0,ZBERCO+LASERZ);
tiltIn.add(las); ghost('Laser diam. '+LASERD+' mm',las,[0,0,1.2]);

// ---- interface -------------------------------------------------------
function linha(cor,nome,extra,alvo,cb){
  var d=document.createElement('label'); d.className='item';
  d.innerHTML='<input type="checkbox" checked>'
    +'<span class="sw" style="background:'+cor+'"></span>'
    +'<span class="n">'+nome+'</span><span class="g">'+extra+'</span>';
  d.querySelector('input').addEventListener('change',function(e){cb(e.target.checked);});
  alvo.appendChild(d);
}
var lista=document.getElementById('lista');
objetos.forEach(function(o){
  linha(o.def.cor,o.def.rotulo,o.def.massa.toFixed(1)+' g',lista,function(v){o.obj.visible=v;});
});
var gdiv=document.getElementById('ghosts');
ghosts.forEach(function(g){
  var c='#'+g.obj.material.color.getHexString();
  linha(c,g.rotulo,'',gdiv,function(v){g.obj.visible=v;});
});

var sTilt=document.getElementById('tilt'), sPan=document.getElementById('pan'),
    sExp=document.getElementById('exp');
function aplica(){
  var a=parseFloat(sTilt.value), p=parseFloat(sPan.value), e=parseFloat(sExp.value)/100;
  document.getElementById('vTilt').textContent=a+'°';
  document.getElementById('vPan').textContent=p+'°';
  document.getElementById('vExp').textContent=e.toFixed(2);
  tilt.rotation.x=a*Math.PI/180;
  base.rotation.z=p*Math.PI/180;
  objetos.concat(ghosts).forEach(function(o){
    if(o.base===undefined) o.base=o.obj.position.clone();
    o.obj.position.set(o.base.x+o.dir[0]*55*e, o.base.y+o.dir[1]*55*e,
                       o.base.z+o.dir[2]*55*e);
  });
}
[sTilt,sPan,sExp].forEach(function(s){s.addEventListener('input',aplica);});
document.getElementById('ar').addEventListener('change',function(e){mostraArestas(e.target.checked);});

// ---- orbita simples --------------------------------------------------
var az=-0.9, el=0.42, dist=330, alvo=new THREE.Vector3(0,0,25);
function camPos(){
  cam.position.set(alvo.x+dist*Math.cos(el)*Math.cos(az),
                   alvo.y+dist*Math.cos(el)*Math.sin(az),
                   alvo.z+dist*Math.sin(el));
  cam.lookAt(alvo);
}
var arr=null;
lona.addEventListener('contextmenu',function(e){e.preventDefault();});
lona.addEventListener('pointerdown',function(e){arr={x:e.clientX,y:e.clientY,b:e.button};lona.setPointerCapture(e.pointerId);});
lona.addEventListener('pointerup',function(e){arr=null;});
lona.addEventListener('pointermove',function(e){
  if(!arr) return;
  var dx=e.clientX-arr.x, dy=e.clientY-arr.y; arr.x=e.clientX; arr.y=e.clientY;
  if(arr.b===2){
    var f=dist*0.0016;
    alvo.x+= dx*f*Math.sin(az)-dy*f*Math.cos(az)*Math.sin(el);
    alvo.y+=-dx*f*Math.cos(az)-dy*f*Math.sin(az)*Math.sin(el);
    alvo.z+= dy*f*Math.cos(el);
  }else{
    az-=dx*0.008; el=Math.max(-1.45,Math.min(1.45,el+dy*0.006));
  }
  camPos();
});
lona.addEventListener('wheel',function(e){
  e.preventDefault();
  dist=Math.max(90,Math.min(900,dist*(1+(e.deltaY>0?0.1:-0.1))));
  camPos();
},{passive:false});

function redim(){
  var w=lona.clientWidth, h=lona.clientHeight;
  rend.setSize(w,h,false); rend.setPixelRatio(Math.min(2,devicePixelRatio));
  cam.aspect=w/h; cam.updateProjectionMatrix();
}
window.addEventListener('resize',function(){redim();camPos();});
redim(); camPos(); aplica();
(function laco(){ requestAnimationFrame(laco); rend.render(cena,cam); })();
</script></body></html>
"""


if __name__ == '__main__':
    if '--relatorio' in sys.argv:
        pz, mb, ma = pivo_equilibrado()
        print('pivo equilibrado = %.2f mm | berco %.0f g | acessorios %.0f g'
              % (pz, mb, ma))
        print('envelope = %.2f mm acima do deck' % (H_EIXO + envelope_tilt(round(pz, 1))))
    else:
        main()
