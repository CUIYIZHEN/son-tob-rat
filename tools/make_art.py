"""손톱 먹은 쥐 그림 만들기. 실행하면 Assets/Resources/Art/*.png 를 다시 그린다.
사용법: python3 tools/make_art.py   (Pillow 필요)
낮 색으로만 그리고, 밤에는 게임이 색을 입혀 어둡게 쓴다."""
import math, os, random
from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'Resources', 'Art')
os.makedirs(OUT, exist_ok=True)
S = 4  # 겹쳐 그린 뒤 줄여서 부드럽게

INK = (48, 36, 32, 255)
SKIN = (240, 205, 170, 255)

def canvas(w, h):
    im = Image.new('RGBA', (w * S, h * S), (0, 0, 0, 0))
    return im, ImageDraw.Draw(im)

def sc(pts):
    return [(x * S, y * S) for x, y in pts]

def save(im, name, w, h):
    im = im.resize((w, h), Image.LANCZOS)
    im.save(os.path.join(OUT, name))

def poly(d, pts, fill, outline=INK, width=1.2):
    d.polygon(sc(pts), fill=fill)
    if outline:
        d.line(sc(pts + [pts[0]]), fill=outline, width=int(width * S), joint='curve')

def ell(d, box, fill, outline=INK, width=1.2):
    x0, y0, x1, y1 = box
    d.ellipse([x0 * S, y0 * S, x1 * S, y1 * S], fill=fill, outline=outline, width=int(width * S) if outline else 0)

def line(d, pts, fill=INK, width=1.5):
    d.line(sc(pts), fill=fill, width=int(width * S), joint='curve')

def rect(d, box, fill, outline=INK, width=1.2):
    x0, y0, x1, y1 = box
    d.rectangle([x0 * S, y0 * S, x1 * S, y1 * S], fill=fill, outline=outline, width=int(width * S) if outline else 0)

def shade(c, k):
    return (max(0, min(255, int(c[0] * k))), max(0, min(255, int(c[1] * k))), max(0, min(255, int(c[2] * k))), 255)

DIAMOND = [(32, 0), (64, 16), (32, 32), (0, 16)]

def diamond_mask(w=64, h=32):
    m = Image.new('L', (w * S, h * S), 0)
    ImageDraw.Draw(m).polygon(sc([(w / 2, 0), (w, h / 2), (w / 2, h), (0, h / 2)]), fill=255)
    return m

def clip_diamond(im):
    m = diamond_mask(im.width // S, im.height // S)
    out = Image.new('RGBA', im.size, (0, 0, 0, 0))
    out.paste(im, (0, 0), m)
    return out

# ───────────── 바닥 칸 (64 × 32)

def ground(name, seed, base=(222, 205, 165, 255)):
    rnd = random.Random(seed)
    im, d = canvas(64, 32)
    d.polygon(sc(DIAMOND), fill=base)
    for _ in range(46):
        x, y = rnd.uniform(8, 56), rnd.uniform(4, 28)
        k = rnd.uniform(0.86, 1.08)
        r = rnd.uniform(0.5, 1.3)
        ell(d, (x - r, y - r * 0.6, x + r, y + r * 0.6), shade(base, k), None)
    for _ in range(5):                      # 작은 돌멩이
        x, y = rnd.uniform(14, 50), rnd.uniform(8, 24)
        ell(d, (x - 2, y - 1, x + 2, y + 1), (190, 178, 150, 255), (150, 138, 112, 255), 0.5)
    for _ in range(4):                      # 풀 한 포기
        x, y = rnd.uniform(14, 50), rnd.uniform(8, 24)
        line(d, [(x, y), (x - 1, y - 2.5)], (130, 150, 80, 255), 0.7)
        line(d, [(x, y), (x + 1, y - 2.8)], (120, 145, 75, 255), 0.7)
    im = clip_diamond(im)
    d = ImageDraw.Draw(im)
    d.line(sc(DIAMOND + [DIAMOND[0]]), fill=shade(base, 0.72), width=int(1.1 * S), joint='curve')
    save(im, name, 64, 32)

def blocked(name, seed):
    """막힌 칸의 바닥: 길과 다르게 풀숲과 빗금."""
    rnd = random.Random(seed)
    base = (156, 168, 118, 255)
    im, d = canvas(64, 32)
    d.polygon(sc(DIAMOND), fill=base)
    for i in range(-8, 24):
        x = i * 6
        d.line(sc([(x, 32), (x + 32, 0)]), fill=shade(base, 0.9), width=int(1.2 * S))
    for _ in range(20):
        x, y = rnd.uniform(10, 54), rnd.uniform(6, 27)
        line(d, [(x, y), (x - 1.2, y - 3.5)], (96, 128, 70, 255), 0.9)
        line(d, [(x, y), (x + 0.2, y - 4)], (84, 116, 62, 255), 0.9)
        line(d, [(x, y), (x + 1.4, y - 3.2)], (110, 140, 76, 255), 0.9)
    im = clip_diamond(im)
    d = ImageDraw.Draw(im)
    d.line(sc(DIAMOND + [DIAMOND[0]]), fill=shade(base, 0.7), width=int(1.1 * S), joint='curve')
    save(im, name, 64, 32)

def wall_block(name, arch=False):
    """막힌 칸: 칸을 꽉 채우는 낮은 돌 블록(높이 16, 칸 높이 32를 넘지 않음). arch면 담 아래 반원 구멍(개구멍)."""
    im, d = canvas(64, 48)
    roof = (104, 106, 120, 255)
    se_col = (168, 138, 112, 255)
    sw_col = (194, 164, 134, 255)
    top = [(32, 0), (64, 16), (32, 32), (0, 16)]
    se = [(32, 32), (64, 16), (64, 32), (32, 48)]
    sw = [(0, 16), (32, 32), (32, 48), (0, 32)]
    poly(d, se, se_col, INK, 1.2)
    poly(d, sw, sw_col, INK, 1.2)
    poly(d, top, roof, INK, 1.3)
    # 기와 무늬(위)
    for k in range(1, 5):
        t = k / 5.0
        line(d, [(32 - 32 * t, 16 * t), (64 - 32 * t, 16 + 16 * t - 16)], shade(roof, 0.78), 0.8)
    for k in range(1, 5):
        t = k / 5.0
        line(d, [(32 + 32 * t, 16 * t), (0 + 32 * t, 16 + 16 * t)], shade(roof, 0.88), 0.7)
    # 흙담 돌 무늬(옆)
    rnd = random.Random(11)
    for _ in range(7):
        x = rnd.uniform(36, 61); ytop = 32 - (x - 32) / 2
        y = rnd.uniform(ytop + 3, ytop + 13)
        ell(d, (x - 2.2, y - 1.4, x + 2.2, y + 1.4), shade(se_col, rnd.uniform(0.85, 1.1)), shade(se_col, 0.7), 0.5)
    for _ in range(7):
        x = rnd.uniform(3, 28); ytop = 16 + x / 2
        y = rnd.uniform(ytop + 3, ytop + 13)
        ell(d, (x - 2.2, y - 1.4, x + 2.2, y + 1.4), shade(sw_col, rnd.uniform(0.85, 1.1)), shade(sw_col, 0.7), 0.5)
    if arch:
        r = 9.0
        for face in ('se', 'sw'):
            pts_out, pts_in = [], []
            for i in range(0, 21):
                t = -r + (2 * r) * i / 20.0
                v = math.sqrt(max(0.0, r * r - t * t)) * 0.95
                if face == 'se':
                    bx, by = 48 + t, 40 - t / 2.0
                else:
                    bx, by = 16 + t, 40 + t / 2.0
                pts_out.append((bx, by - v))
                pts_in.append((bx, by - v * 0.78))
            base = pts_out[0], pts_out[-1]
            poly(d, [base[0]] + pts_out[1:-1] + [base[1]], (28, 20, 16, 255), INK, 1.3)
            ins = [(x, y + (0 if i in (0, 20) else 0)) for i, (x, y) in enumerate(pts_in)]
            d.polygon(sc([pts_in[0]] + pts_in[1:-1] + [pts_in[-1]]), fill=(12, 8, 6, 255))
    save(im, name, 64, 48)

# ───────────── 물건

def hole(name):
    im, d = canvas(48, 28)
    ell(d, (4, 4, 44, 26), (150, 118, 86, 255), INK, 1.4)             # 흙무더기
    ell(d, (10, 7, 38, 23), (30, 20, 16, 255), INK, 1.2)              # 구멍
    ell(d, (14, 11, 34, 21), (12, 8, 6, 255), None)
    for (x, y) in [(6, 15), (42, 14), (14, 5), (34, 5)]:
        ell(d, (x - 2.5, y - 1.5, x + 2.5, y + 1.5), (176, 150, 120, 255), INK, 0.7)
    save(im, name, 48, 28)

def nail(name):
    im, d = canvas(32, 32)
    for r, a in [(15, 40), (12, 70), (9, 110)]:
        ell(d, (16 - r, 16 - r * 0.7, 16 + r, 16 + r * 0.7), (255, 238, 150, a), None)
    # 손톱 조각: 반달꼴
    d.pieslice([6 * S, 9 * S, 26 * S, 29 * S], 200, 340, fill=(255, 248, 226, 255), outline=INK, width=int(1.1 * S))
    line(d, [(10, 14), (22, 14)], (210, 190, 170, 255), 0.7)
    save(im, name, 32, 32)

def well(name):
    im, d = canvas(48, 60)
    # 기둥과 지붕
    rect(d, (8, 12, 11, 40), (120, 88, 64, 255), INK, 1.0)
    rect(d, (37, 12, 40, 40), (120, 88, 64, 255), INK, 1.0)
    poly(d, [(2, 14), (24, 2), (46, 14), (40, 16), (24, 8), (8, 16)], (92, 92, 104, 255), INK, 1.1)
    line(d, [(24, 10), (24, 26)], INK, 1.0)
    rect(d, (21, 26, 27, 32), (150, 110, 70, 255), INK, 0.9)           # 두레박
    # 돌 우물 몸통
    poly(d, [(6, 38), (42, 38), (44, 52), (24, 58), (4, 52)], (170, 168, 160, 255), INK, 1.3)
    ell(d, (6, 32, 42, 46), (86, 130, 170, 255), INK, 1.3)
    ell(d, (10, 35, 38, 44), (60, 100, 140, 255), None)
    for x in (10, 20, 30, 38):
        line(d, [(x, 48), (x - 1, 54)], (120, 118, 112, 255), 0.8)
    save(im, name, 48, 60)

def seodang(name):
    im, d = canvas(48, 60)
    rect(d, (6, 28, 42, 54), (214, 196, 160, 255), INK, 1.2)
    poly(d, [(0, 30), (24, 12), (48, 30), (42, 32), (24, 20), (6, 32)], (82, 84, 96, 255), INK, 1.2)
    rect(d, (18, 38, 30, 54), (120, 84, 60, 255), INK, 1.0)
    rect(d, (9, 33, 16, 41), (240, 236, 220, 255), INK, 0.8)
    rect(d, (32, 33, 39, 41), (240, 236, 220, 255), INK, 0.8)
    # 책
    rect(d, (14, 3, 34, 10), (240, 232, 205, 255), INK, 0.9)
    line(d, [(16, 6), (32, 6)], (140, 120, 100, 255), 0.6)
    save(im, name, 48, 60)

def jeongja(name):
    im, d = canvas(48, 60)
    for x in (8, 38):
        rect(d, (x, 26, x + 3, 54), (150, 70, 56, 255), INK, 1.0)
    rect(d, (6, 50, 42, 56), (196, 164, 120, 255), INK, 1.0)
    poly(d, [(0, 28), (24, 6), (48, 28), (42, 30), (24, 14), (6, 30)], (70, 100, 120, 255), INK, 1.2)
    line(d, [(24, 6), (24, 3)], INK, 1.2)
    rect(d, (8, 34, 40, 38), (196, 120, 90, 255), INK, 0.8)
    save(im, name, 48, 60)

def jangseung(name):
    im, d = canvas(40, 64)
    rect(d, (13, 12, 27, 58), (150, 108, 70, 255), INK, 1.3)
    ell(d, (8, 4, 32, 26), (166, 122, 82, 255), INK, 1.3)
    ell(d, (12, 11, 17, 15), (255, 250, 235, 255), INK, 0.8)
    ell(d, (23, 11, 28, 15), (255, 250, 235, 255), INK, 0.8)
    ell(d, (14, 12, 16, 14), INK, None); ell(d, (24, 12, 26, 14), INK, None)
    line(d, [(15, 20), (25, 20)], (120, 40, 36, 255), 1.4)
    rect(d, (10, 30, 30, 33), (170, 70, 56, 255), INK, 0.8)
    line(d, [(14, 44), (26, 44)], (110, 80, 54, 255), 1.0)
    save(im, name, 40, 64)

# ───────────── 캐릭터 (발 밑이 가운데 아래)

def rat(name, w=56, h=40):
    im, d = canvas(w, h)
    grey = (150, 146, 142, 255)
    line(d, [(10, 26), (4, 20), (3, 12), (7, 7)], (214, 160, 160, 255), 2.2)   # 꼬리
    line(d, [(10, 26), (4, 20), (3, 12), (7, 7)], INK, 0.8)
    ell(d, (8, 12, 38, 34), grey, INK, 1.4)                                    # 몸통
    ell(d, (14, 20, 34, 34), (196, 190, 184, 255), None)
    poly(d, [(32, 14), (50, 22), (34, 32)], grey, INK, 1.3)                    # 머리와 뾰족한 코
    ell(d, (48, 20, 52, 24), (230, 130, 140, 255), INK, 0.8)                   # 코
    ell(d, (26, 6, 38, 18), (232, 170, 176, 255), INK, 1.2)                    # 귀
    ell(d, (38, 19, 42, 23), (20, 16, 16, 255), None)                          # 눈
    ell(d, (39, 19.4, 40.4, 20.6), (255, 255, 255, 255), None)
    for x in (20, 30):                                                         # 발
        ell(d, (x, 32, x + 6, 37), (214, 170, 170, 255), INK, 0.9)
    line(d, [(46, 25), (52, 27)], INK, 0.5); line(d, [(46, 27), (52, 30)], INK, 0.5)  # 수염
    save(im, name, w, h)

def rat_curled(name):
    im, d = canvas(48, 36)
    grey = (150, 146, 142, 255)
    ell(d, (6, 8, 42, 34), grey, INK, 1.4)
    ell(d, (14, 18, 36, 33), (196, 190, 184, 255), None)
    ell(d, (28, 4, 40, 16), (232, 170, 176, 255), INK, 1.2)                    # 귀
    ell(d, (6, 20, 18, 30), (214, 170, 170, 255), INK, 0.9)
    line(d, [(10, 28), (24, 33), (38, 28)], (214, 160, 160, 255), 2.4)         # 몸을 감은 꼬리
    line(d, [(12, 22), (16, 22)], INK, 0.9)                                    # 감은 눈
    save(im, name, 48, 36)

def lord(name):
    w, h = 44, 70
    im, d = canvas(w, h)
    blue = (74, 112, 200, 255)
    poly(d, [(11, 30), (33, 30), (40, 66), (4, 66)], blue, INK, 1.4)            # 도포
    poly(d, [(18, 30), (26, 30), (22, 44)], (244, 240, 230, 255), INK, 1.0)     # 옷깃
    line(d, [(12, 44), (32, 44)], (200, 50, 50, 255), 2.0)                      # 띠
    for dx in (-7, 7):
        poly(d, [(11 if dx < 0 else 33, 32), (4 if dx < 0 else 40, 52), (9 if dx < 0 else 35, 54), (13 if dx < 0 else 31, 36)], shade(blue, 0.88), INK, 1.0)
    rect(d, (14, 66, 21, 69), INK, INK, 0.5); rect(d, (23, 66, 30, 69), INK, INK, 0.5)
    ell(d, (13, 12, 31, 32), SKIN, INK, 1.3)                                    # 얼굴
    ell(d, (4, 10, 40, 18), (30, 28, 34, 255), INK, 1.3)                        # 갓 챙
    ell(d, (14, 0, 30, 14), (40, 36, 44, 255), INK, 1.3)                        # 갓 윗부분
    ell(d, (17, 21, 20, 24), INK, None); ell(d, (24, 21, 27, 24), INK, None)
    line(d, [(16, 19), (20, 20)], INK, 0.8); line(d, [(28, 19), (24, 20)], INK, 0.8)
    line(d, [(19, 27), (25, 27)], (150, 70, 70, 255), 0.8)
    save(im, name, w, h)

def lord_run(name):
    """사람 모습 그대로 쥐처럼 네 발로 달리는 도령."""
    w, h = 64, 44
    im, d = canvas(w, h)
    blue = (74, 112, 200, 255)
    line(d, [(10, 24), (4, 22), (1, 14)], (214, 160, 160, 255), 2.0)             # 옷 밑으로 삐져나온 쥐 꼬리
    for (x0, y0, x1, y1) in [(10, 30, 5, 41), (22, 32, 20, 41)]:                  # 뒷다리(바지와 신)
        line(d, [(x0, y0), (x1, y1)], shade(blue, 0.8), 4.0)
        ell(d, (x1 - 4, y1 - 1.5, x1 + 3, y1 + 2.5), (30, 28, 34, 255), INK, 0.8)
    poly(d, [(8, 16), (38, 14), (42, 28), (40, 36), (10, 36)], blue, INK, 1.4)    # 엎드린 도포
    line(d, [(14, 30), (36, 29)], (200, 50, 50, 255), 1.8)                        # 띠
    for (x0, y0, x1, y1) in [(36, 26, 44, 40), (44, 24, 54, 40)]:                 # 앞다리(소매와 손)
        line(d, [(x0, y0), (x1, y1)], shade(blue, 0.92), 4.0)
        ell(d, (x1 - 2.5, y1 - 2, x1 + 3.5, y1 + 2.5), SKIN, INK, 0.8)
    ell(d, (42, 8, 60, 27), SKIN, INK, 1.3)                                       # 얼굴
    ell(d, (38, 6, 62, 14), (30, 28, 34, 255), INK, 1.3)                          # 갓 챙
    ell(d, (44, -1, 58, 9), (40, 36, 44, 255), INK, 1.3)                          # 갓 윗부분
    ell(d, (53, 16, 56, 19.5), INK, None)
    line(d, [(51, 14), (56, 15.5)], INK, 0.8)                                     # 굳은 눈썹
    line(d, [(54, 23), (58, 22.5)], (150, 70, 70, 255), 0.7)
    save(im, name, w, h)

def cat(name):
    w, h = 64, 48
    im, d = canvas(w, h)
    fur = (156, 120, 190, 255)
    line(d, [(10, 30), (4, 22), (6, 10), (12, 6)], fur, 4.0)                     # 꼬리
    line(d, [(10, 30), (4, 22), (6, 10), (12, 6)], INK, 0.8)
    ell(d, (8, 18, 44, 40), fur, INK, 1.5)                                       # 몸통
    ell(d, (14, 28, 38, 40), (206, 188, 224, 255), None)
    ell(d, (34, 10, 56, 32), fur, INK, 1.5)                                      # 머리
    poly(d, [(36, 14), (38, 3), (45, 11)], fur, INK, 1.3)                        # 귀
    poly(d, [(46, 12), (52, 3), (54, 15)], fur, INK, 1.3)
    poly(d, [(38, 12), (39, 7), (42, 11)], (236, 170, 190, 255), None)
    ell(d, (43, 17, 49, 23), (255, 224, 80, 255), INK, 0.9)                      # 눈
    ell(d, (45.2, 17.5, 46.8, 22.5), INK, None)
    poly(d, [(52, 24), (55, 24), (53.5, 26)], (240, 140, 150, 255), INK, 0.6)    # 코
    for x in (14, 34):
        ell(d, (x, 36, x + 8, 44), fur, INK, 1.1)
    line(d, [(50, 25), (60, 22)], INK, 0.5); line(d, [(50, 27), (60, 29)], INK, 0.5)
    save(im, name, w, h)

def villager(name, kind):
    w, h = 44, 66
    im, d = canvas(w, h)
    if kind == 'ajumma':
        poly(d, [(10, 30), (34, 30), (40, 64), (4, 64)], (200, 70, 90, 255), INK, 1.4)      # 붉은 치마
        poly(d, [(12, 24), (32, 24), (34, 38), (10, 38)], (246, 242, 232, 255), INK, 1.3)    # 흰 저고리
        line(d, [(18, 28), (26, 40)], (190, 60, 70, 255), 1.4)
        ell(d, (13, 6, 31, 26), SKIN, INK, 1.3)
        ell(d, (11, 3, 33, 16), (36, 30, 30, 255), INK, 1.2)
        ell(d, (17, -1, 27, 6), (36, 30, 30, 255), INK, 1.0)                                  # 쪽
    elif kind == 'child':
        poly(d, [(12, 40), (32, 40), (36, 64), (8, 64)], (240, 200, 90, 255), INK, 1.4)       # 노란 옷
        poly(d, [(12, 40), (32, 40), (30, 50), (14, 50)], (250, 232, 150, 255), INK, 1.0)
        ell(d, (11, 18, 33, 42), SKIN, INK, 1.3)
        ell(d, (17, 8, 27, 20), (30, 26, 28, 255), INK, 1.1)                                  # 상투
        ell(d, (10, 17, 34, 28), (30, 26, 28, 255), INK, 1.1)
        rect(d, (30, 44, 40, 52), (240, 232, 205, 255), INK, 0.9)                             # 책
    elif kind == 'peddler':
        poly(d, [(12, 26), (32, 26), (37, 64), (7, 64)], (150, 118, 78, 255), INK, 1.4)
        rect(d, (24, 16, 42, 46), (186, 150, 100, 255), INK, 1.3)                             # 짐
        line(d, [(26, 16), (26, 46)], (120, 90, 60, 255), 1.0)
        ell(d, (13, 8, 31, 28), SKIN, INK, 1.3)
        poly(d, [(8, 12), (22, 3), (36, 12)], (140, 108, 60, 255), INK, 1.2)                  # 벙거지
    else:  # farmer
        poly(d, [(10, 28), (34, 28), (38, 64), (6, 64)], (118, 140, 108, 255), INK, 1.4)
        line(d, [(14, 36), (30, 36)], (96, 116, 88, 255), 1.0)
        ell(d, (13, 10, 31, 30), SKIN, INK, 1.3)
        poly(d, [(2, 14), (22, -1), (42, 14), (22, 18)], (214, 184, 100, 255), INK, 1.3)      # 삿갓
        line(d, [(36, 30), (40, 14)], (130, 96, 60, 255), 1.6)                                # 괭이 자루
    for (x, y) in [(18, 20), (26, 20)]:
        ell(d, (x - 1.2, y - 1.2, x + 1.2, y + 1.2), INK, None)
    if kind != 'farmer':
        line(d, [(19, 25), (25, 25)], (150, 70, 70, 255), 0.7)
    save(im, name, w, h)

# ───────────── 그림 컷 (540 × 960)

def cut_canvas():
    return canvas(540, 960)

def cut_finish(im, name):
    im = im.resize((540, 960), Image.LANCZOS)
    im.save(os.path.join(OUT, name))

def cut1(name):
    # 쌀 창고 앞, 화난 도령의 큰 그림자와 문 밖으로 튀어 나가는 고양이
    global S
    old = S; S = 1
    im = Image.new('RGBA', (540, 960))
    d = ImageDraw.Draw(im)
    for y in range(960):                                   # 저물녘 하늘
        t = y / 960
        d.line([(0, y), (540, y)], fill=(int(48 + 60 * t), int(30 + 28 * t), int(40 + 20 * t), 255))
    d.ellipse([360, 70, 470, 180], fill=(246, 214, 150, 255))   # 노을 달
    d.rectangle([0, 640, 540, 960], fill=(54, 38, 34, 255))
    d.polygon([(0, 640), (540, 640), (540, 700), (0, 700)], fill=(70, 50, 42, 255))
    # 쌀 창고: 기와지붕, 흙벽, 열린 문
    d.rectangle([200, 300, 520, 640], fill=(122, 90, 70, 255), outline=INK, width=4)
    d.polygon([(170, 310), (360, 190), (550, 310), (520, 330), (360, 236), (200, 330)], fill=(60, 64, 78, 255), outline=INK)
    for x in range(222, 520, 46):
        d.line([(x, 306), (x, 640)], fill=(100, 72, 56, 255), width=3)
    d.rectangle([290, 380, 430, 640], fill=(255, 214, 120, 255), outline=INK, width=4)   # 불빛 새는 문
    d.polygon([(290, 380), (250, 400), (250, 640), (290, 640)], fill=(90, 62, 46, 255), outline=INK)   # 열린 문짝
    for (cx, cy, rx, ry) in [(330, 610, 38, 24), (392, 612, 36, 22), (360, 570, 34, 22)]:     # 쌀가마
        d.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=(238, 226, 190, 255), outline=INK, width=3)
    # 도령의 커다란 그림자 (화면을 덮음)
    sh = Image.new('RGBA', (540, 960), (0, 0, 0, 0))
    sd = ImageDraw.Draw(sh)
    sd.polygon([(0, 960), (0, 340), (80, 210), (230, 170), (340, 250), (360, 420), (460, 960)], fill=(8, 4, 8, 150))
    im = Image.alpha_composite(im, sh)
    d = ImageDraw.Draw(im)
    # 도령: 어두운 윗도리와 갓, 찌푸린 얼굴
    d.polygon([(30, 960), (60, 520), (170, 450), (260, 540), (300, 960)], fill=(12, 10, 18, 255), outline=INK)
    d.ellipse([70, 330, 250, 520], fill=(236, 196, 160, 255), outline=INK, width=4)
    d.ellipse([30, 320, 290, 380], fill=(20, 18, 26, 255), outline=INK, width=4)
    d.ellipse([88, 230, 232, 346], fill=(30, 26, 38, 255), outline=INK, width=4)
    d.polygon([(100, 408), (150, 428), (150, 440), (100, 424)], fill=INK)            # 찌푸린 눈썹 (왼)
    d.polygon([(240, 408), (190, 428), (190, 440), (240, 424)], fill=INK)            # (오른)
    d.ellipse([112, 440, 140, 464], fill=(60, 18, 18, 255)); d.ellipse([200, 440, 228, 464], fill=(60, 18, 18, 255))
    d.ellipse([120, 446, 134, 460], fill=(255, 70, 50, 255)); d.ellipse([206, 446, 220, 460], fill=(255, 70, 50, 255))
    d.polygon([(130, 490), (210, 490), (200, 506), (140, 506)], fill=(150, 50, 46, 255), outline=INK)  # 앙다문 입
    d.line([(160, 520), (180, 520)], fill=INK, width=3)
    # 문 밖으로 튀어 나가는 고양이와 달리는 선
    fur = (160, 124, 196, 255)
    d.polygon([(560, 700), (470, 690), (470, 705)], fill=(255, 255, 255, 0))
    for (y, l) in [(690, 120), (730, 160), (770, 100)]:
        d.line([(380 + l, y), (380, y)], fill=(255, 240, 220, 150), width=5)
    d.polygon([(250, 760), (210, 700), (170, 720)], fill=fur, outline=INK)
    d.ellipse([200, 700, 380, 820], fill=fur, outline=INK, width=4)
    d.ellipse([140, 660, 240, 760], fill=fur, outline=INK, width=4)
    d.polygon([(148, 676), (150, 626), (186, 664)], fill=fur, outline=INK)
    d.polygon([(200, 664), (236, 628), (240, 690)], fill=fur, outline=INK)
    d.ellipse([160, 700, 190, 724], fill=(255, 226, 80, 255), outline=INK, width=3)
    d.ellipse([200, 700, 230, 724], fill=(255, 226, 80, 255), outline=INK, width=3)
    d.ellipse([172, 704, 180, 722], fill=INK); d.ellipse([212, 704, 220, 722], fill=INK)
    d.polygon([(186, 736), (204, 736), (195, 748)], fill=(240, 140, 150, 255))
    d.line([(370, 780), (440, 740), (470, 690)], fill=fur, width=22)
    S = old
    cut_finish(im, name)

def cut2(name):
    # 쥐구멍 안의 어미 쥐와 마른 새끼들, 문 앞에서 돌아보며 다짐하는 쥐
    global S
    old = S; S = 1
    im = Image.new('RGBA', (540, 960))
    d = ImageDraw.Draw(im)
    for y in range(960):
        t = y / 960
        d.line([(0, y), (540, y)], fill=(int(14 + 14 * t), int(18 + 14 * t), int(40 + 20 * t), 255))
    d.ellipse([380, 90, 470, 180], fill=(244, 238, 200, 255))             # 달
    d.ellipse([360, 70, 490, 200], outline=(244, 238, 200, 60), width=10)
    d.rectangle([0, 560, 540, 960], fill=(54, 40, 34, 255))
    # 쥐구멍 입구: 흙무더기와 둥근 구멍
    d.ellipse([-30, 380, 420, 700], fill=(98, 76, 58, 255), outline=INK, width=5)
    d.ellipse([20, 430, 380, 680], fill=(16, 10, 10, 255), outline=INK, width=4)
    d.ellipse([50, 470, 350, 670], fill=(70, 46, 28, 255))                 # 안쪽 희미한 불빛
    d.rectangle([0, 600, 540, 960], fill=(54, 40, 34, 255))
    d.rectangle([20, 600, 380, 612], fill=(70, 50, 40, 255))
    # 어미 쥐: 마르고 웅크림
    grey = (136, 130, 126, 255)
    d.ellipse([60, 540, 250, 640], fill=grey, outline=INK, width=4)
    d.ellipse([20, 520, 110, 600], fill=grey, outline=INK, width=4)
    d.ellipse([30, 500, 70, 540], fill=(222, 160, 166, 255), outline=INK, width=3)
    d.ellipse([34, 546, 48, 560], fill=INK)
    d.line([(60, 590), (50, 600)], fill=INK, width=3)
    d.line([(250, 600), (320, 630), (330, 600)], fill=(214, 160, 160, 255), width=8)
    for k in range(5):                                                       # 앙상한 갈비
        d.arc([100 + k * 22, 560, 140 + k * 22, 620], 90, 270, fill=(90, 84, 80, 255), width=3)
    # 마른 새끼 셋
    pup = (176, 170, 160, 255)
    for i in range(3):
        px = 250 + i * 54
        d.ellipse([px, 585 - i * 4, px + 62, 635 - i * 4], fill=pup, outline=INK, width=3)
        d.ellipse([px - 10, 588 - i * 4, px + 20, 618 - i * 4], fill=pup, outline=INK, width=3)
        d.ellipse([px - 6, 580 - i * 4, px + 8, 594 - i * 4], fill=(224, 170, 176, 255), outline=INK, width=2)
        d.ellipse([px - 3, 602 - i * 4, px + 3, 608 - i * 4], fill=INK)
        for k in range(3): d.line([(px + 20 + k * 9, 600 - i * 4), (px + 20 + k * 9, 622 - i * 4)], fill=(100, 94, 88, 255), width=2)
    # 문 앞에서 돌아보며 다짐하는 쥐 (오른쪽, 크게)
    me = (158, 152, 148, 255)
    d.line([(470, 740), (520, 760), (530, 720), (500, 700)], fill=(214, 160, 160, 255), width=14)
    d.ellipse([320, 640, 500, 780], fill=me, outline=INK, width=5)
    d.polygon([(330, 660), (240, 710), (330, 760)], fill=me, outline=INK)    # 돌아본 머리와 코
    d.ellipse([228, 702, 252, 722], fill=(230, 130, 140, 255), outline=INK, width=3)
    d.ellipse([310, 620, 370, 680], fill=(232, 170, 176, 255), outline=INK, width=4)
    d.ellipse([296, 690, 318, 712], fill=(14, 10, 10, 255))
    d.ellipse([302, 694, 310, 702], fill=(255, 255, 255, 255))
    d.polygon([(286, 678), (324, 688), (322, 696), (286, 686)], fill=INK)    # 굳은 눈썹
    d.line([(250, 716), (200, 706)], fill=INK, width=2); d.line([(250, 722), (200, 734)], fill=INK, width=2)
    for x in (360, 430):
        d.ellipse([x, 760, x + 50, 800], fill=(214, 170, 170, 255), outline=INK, width=4)
    S = old
    cut_finish(im, name)

if __name__ == '__main__':
    ground('ground_a.png', 1)
    ground('ground_b.png', 2, (216, 198, 158, 255))
    blocked('blocked.png', 3)
    wall_block('wall_block.png')
    wall_block('doghole.png', True)
    hole('hole.png'); nail('nail.png'); well('well.png')
    seodang('mission_seodang.png'); jeongja('mission_jeongja.png'); jangseung('mission_jangseung.png')
    rat('rat.png'); rat_curled('rat_curled.png'); lord('lord.png'); lord_run('lord_run.png'); cat('cat.png')
    for k, n in [('ajumma', 'villager_ajumma.png'), ('child', 'villager_child.png'), ('peddler', 'villager_peddler.png'), ('farmer', 'villager_farmer.png')]:
        villager(n, k)
    cut1('cut1.png'); cut2('cut2.png')
    print('done', sorted(os.listdir(OUT)))
