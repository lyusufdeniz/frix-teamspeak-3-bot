# Bot boştayken gösterilen avatarı üretir (Pillow gerekir): python make_avatar.py ../../TS3AudioBot/Assets/SleepingKitty.png
from PIL import Image, ImageDraw, ImageFont
import sys

W, H = 300, 405
S = 3  # süper örnekleme (daha keskin yazı)
img = Image.new("RGB", (W * S, H * S), (15, 17, 23))
d = ImageDraw.Draw(img)

F = r"C:\Windows\Fonts"
title = ImageFont.truetype(F + r"\segoeuib.ttf", 30 * S)
sub = ImageFont.truetype(F + r"\segoeui.ttf", 15 * S)
cmd = ImageFont.truetype(F + r"\consolab.ttf", 17 * S)
desc = ImageFont.truetype(F + r"\segoeui.ttf", 14 * S)
foot = ImageFont.truetype(F + r"\segoeui.ttf", 13 * S)

GREEN = (0, 230, 118)
ACCENT = (79, 140, 255)
TEXT = (231, 233, 238)
MUTED = (139, 145, 165)

def s(v):
    return int(v * S)

# Üst şerit
d.rectangle([0, 0, W * S, s(92)], fill=(23, 26, 35))
d.rectangle([0, s(92), W * S, s(95)], fill=GREEN)

# Nota simgesi
cx, cy = s(34), s(46)
d.ellipse([cx - s(11), cy + s(6), cx + s(5), cy + s(20)], fill=GREEN)
d.rectangle([cx + s(2), cy - s(22), cx + s(6), cy + s(14)], fill=GREEN)
d.polygon([(cx + s(6), cy - s(22)), (cx + s(22), cy - s(14)), (cx + s(22), cy - s(6)), (cx + s(6), cy - s(13))], fill=GREEN)

d.text((s(64), s(14)), "Frix", font=title, fill=TEXT)
d.text((s(140), s(27)), "TS3 Bot", font=sub, fill=MUTED)
d.text((s(65), s(56)), "Boşta · şarkı açmak için yaz:", font=sub, fill=MUTED)

rows = [
    ("!ara", "şarkı adı / link"),
    ("!gec", "sonraki şarkı"),
    ("!dur", "duraklat · devam"),
    ("!bitir", "müziği durdur"),
    ("!ses 50", "ses seviyesi"),
    ("!calan", "çalan şarkı"),
    ("!siradaki", "kuyruk"),
    ("!yardim", "tüm komutlar"),
]
y = s(112)
for c, t in rows:
    d.text((s(18), y), c, font=cmd, fill=GREEN)
    d.text((s(140), y + s(2)), t, font=desc, fill=TEXT)
    y += s(32)

d.rectangle([s(18), s(370), W * S - s(18), s(371)], fill=(42, 47, 64))
d.text((s(18), s(378)), "Örn: !ara tarkan kuzu kuzu", font=foot, fill=ACCENT)

img = img.resize((W, H), Image.LANCZOS)
img.save(sys.argv[1], optimize=True)
print("saved", sys.argv[1])
