# Frix marka görselleri: kulaklıklı tilki maskotu.
# Tek kaynak: SHAPES listesi hem PNG/ICO (Pillow) hem SVG çıktısını üretir.
# Kullanım: python make_brand.py <repo-kökü> [arka-plan-hex] [--variants]
import math
import os
import sys
from PIL import Image, ImageDraw, ImageFont

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..", "..")
BG = next((a for a in sys.argv[2:] if a.startswith("#")), "#1d2b3a")
FONT_DIR = os.path.join(ROOT, "TS3AudioBot", "Assets", "Fonts")

ORANGE = "#ec6b2d"
ORANGE_D = "#c9531c"
CREAM = "#fff6ea"
INK = "#2a201c"
HP = "HP"  # kulaklık rengi: zemine göre seçilir


def hp_color(bg):
    bg = bg.lstrip("#")
    r, g, b = (int(bg[i:i + 2], 16) for i in (0, 2, 4))
    return INK if (0.299 * r + 0.587 * g + 0.114 * b) > 140 else "#f3e7d6"

# 100x100 koordinat sisteminde şekiller: (tür, renk, veri)
SHAPES = [
    ("arc", HP, (16, 13, 84, 81, 196, 344, 6)),            # kulaklık bandı
    ("poly", ORANGE, [(26, 44), (34, 14), (48, 36)]),        # kulaklar
    ("poly", ORANGE, [(74, 44), (66, 14), (52, 36)]),
    ("poly", INK, [(31.5, 38), (35, 23), (42, 34)]),
    ("poly", INK, [(68.5, 38), (65, 23), (58, 34)]),
    ("poly", ORANGE, [(22, 42), (78, 42), (50, 84)]),        # yüz
    ("poly", CREAM, [(22, 42), (50, 60), (50, 84)]),
    ("poly", CREAM, [(78, 42), (50, 60), (50, 84)]),
    ("poly", ORANGE_D, [(40, 42), (60, 42), (50, 58)]),
    ("arc", INK, (32, 47, 42, 55, 200, 340, 2.2)),          # gözler
    ("arc", INK, (58, 47, 68, 55, 200, 340, 2.2)),
    ("ellipse", INK, (46.5, 79, 53.5, 85)),                  # burun
    ("rrect", HP, (12, 41, 25, 64, 5)),                     # kulaklık kapakları
    ("rrect", HP, (75, 41, 88, 64, 5)),
    ("rrect", ORANGE, (15, 46, 19, 59, 2)),
    ("rrect", ORANGE, (81, 46, 85, 59, 2)),
]


def draw_fox(d, ox, oy, size, bg=None):
    k = size / 100
    P = lambda x, y: (ox + x * k, oy + y * k)
    hp = hp_color(bg or BG)
    for kind, col, data in SHAPES:
        col = hp if col == HP else col
        if kind == "poly":
            d.polygon([P(x, y) for x, y in data], fill=col)
        elif kind == "ellipse":
            x0, y0, x1, y1 = data
            d.ellipse([*P(x0, y0), *P(x1, y1)], fill=col)
        elif kind == "rrect":
            x0, y0, x1, y1, r = data
            d.rounded_rectangle([*P(x0, y0), *P(x1, y1)], radius=r * k, fill=col)
        elif kind == "arc":
            x0, y0, x1, y1, a0, a1, w = data
            d.arc([*P(x0, y0), *P(x1, y1)], a0, a1, fill=col, width=max(1, int(w * k)))


def logo(size, bg=None):
    bg = bg or BG
    S = 4
    W = size * S
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, 0, W - 1, W - 1], radius=int(W * 0.22), fill=bg)
    pad = W * 0.06
    draw_fox(d, pad, pad * 1.1, W - 2 * pad, bg)
    return img.resize((size, size), Image.LANCZOS)


def svg(path, bg=None):
    bg = bg or BG
    out = ['<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100" role="img" aria-label="Frix">',
           f'<rect width="100" height="100" rx="22" fill="{bg}"/>',
           '<g transform="translate(6 6.6) scale(0.88)">']
    hp = hp_color(bg)
    for kind, col, data in SHAPES:
        col = hp if col == HP else col
        if kind == "poly":
            out.append(f'<polygon fill="{col}" points="{" ".join(f"{x},{y}" for x, y in data)}"/>')
        elif kind == "ellipse":
            x0, y0, x1, y1 = data
            out.append(f'<ellipse fill="{col}" cx="{(x0+x1)/2}" cy="{(y0+y1)/2}" rx="{(x1-x0)/2}" ry="{(y1-y0)/2}"/>')
        elif kind == "rrect":
            x0, y0, x1, y1, r = data
            out.append(f'<rect fill="{col}" x="{x0}" y="{y0}" width="{x1-x0}" height="{y1-y0}" rx="{r}"/>')
        elif kind == "arc":
            x0, y0, x1, y1, a0, a1, w = data
            cx, cy, rx, ry = (x0 + x1) / 2, (y0 + y1) / 2, (x1 - x0) / 2, (y1 - y0) / 2
            sx, sy = cx + rx * math.cos(math.radians(a0)), cy + ry * math.sin(math.radians(a0))
            ex, ey = cx + rx * math.cos(math.radians(a1)), cy + ry * math.sin(math.radians(a1))
            large = 1 if (a1 - a0) % 360 > 180 else 0
            out.append(f'<path fill="none" stroke="{col}" stroke-width="{w}" '
                       f'd="M{sx:.2f} {sy:.2f} A{rx} {ry} 0 {large} 1 {ex:.2f} {ey:.2f}"/>')
    out.append('</g></svg>')
    open(path, "w", encoding="utf-8").write("\n".join(out) + "\n")


def banner(path, bg=None):
    bg = bg or BG
    W, H = 1280, 360
    S = 2
    img = Image.new("RGB", (W * S, H * S), bg)
    d = ImageDraw.Draw(img)
    s = lambda v: v * S
    bold = ImageFont.truetype(os.path.join(FONT_DIR, "JetBrainsMono-Bold.ttf"), s(104))
    reg = ImageFont.truetype(os.path.join(FONT_DIR, "JetBrainsMono-Regular.ttf"), s(26))
    draw_fox(d, s(880), s(28), s(320), bg)
    d.text((s(96), s(86)), "frix", font=bold, fill=CREAM)
    d.text((s(100), s(212)), "teamspeak 3 bot", font=reg, fill=ORANGE)
    d.text((s(100), s(256)), "music · radio · live streams · web panel", font=reg, fill="#9fb0c2")
    img = img.resize((W, H), Image.LANCZOS)
    img.save(path, optimize=True)


def variants(path):
    bgs = [("#1d2b3a", "lacivert"), ("#1f3a2e", "orman"), ("#2b1f36", "mor"), ("#f4ede1", "krem")]
    W = 260
    sheet = Image.new("RGB", (len(bgs) * (W + 20) + 20, W + 70), "#ffffff")
    d = ImageDraw.Draw(sheet)
    f = ImageFont.truetype(os.path.join(FONT_DIR, "JetBrainsMono-Bold.ttf"), 20)
    for i, (c, name) in enumerate(bgs):
        im = logo(W, c)
        sheet.paste(im, (20 + i * (W + 20), 20), im)
        d.text((24 + i * (W + 20), W + 32), f"{i + 1} · {name}", font=f, fill="#222")
    sheet.save(path)


if __name__ == "__main__":
    images = os.path.join(ROOT, "docs", "images")
    os.makedirs(images, exist_ok=True)
    logo(512).save(os.path.join(images, "logo.png"))
    svg(os.path.join(images, "logo.svg"))
    banner(os.path.join(images, "banner.png"))
    icons = {sz: logo(sz) for sz in (16, 24, 32, 48, 64, 128, 256)}
    icons[256].save(os.path.join(ROOT, "FrixTray", "frix.ico"), sizes=[(k, k) for k in icons],
                    append_images=[icons[k] for k in (16, 24, 32, 48, 64, 128)])
    icons[48].save(os.path.join(images, "favicon.ico"), sizes=[(16, 16), (32, 32), (48, 48)],
                   append_images=[icons[16], icons[32]])
    if "--variants" in sys.argv:
        variants(os.path.join(ROOT, "..", "logo-renkler.png"))
    print("ok")
