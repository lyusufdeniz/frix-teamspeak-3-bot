# Tray simgesini üretir (Pillow gerekir): python make_icon.py ../../FrixTray/frix.ico
from PIL import Image, ImageDraw
import sys
S=256
img=Image.new("RGBA",(S,S),(0,0,0,0))
d=ImageDraw.Draw(img)
d.rounded_rectangle([8,8,S-8,S-8],radius=56,fill=(23,26,35,255))
G=(0,230,118,255)
d.ellipse([58,150,130,206],fill=G)
d.rectangle([116,52,132,184],fill=G)
d.polygon([(132,52),(200,84),(200,116),(132,88)],fill=G)
img.save(sys.argv[1],sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
print("ok")
