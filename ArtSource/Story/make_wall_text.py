"""The painted warning on the warehouse wall: dripping red letters, a little crooked.

    python -I ArtSource/Story/make_wall_text.py  ->  Assets/Story/Art/WallText_StayInTheLight.png
"""
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = Path(__file__).parent
OUT = HERE.parent.parent / "Assets/Story/Art/WallText_StayInTheLight.png"
FONT = HERE.parent.parent / "Assets/Story/Resources/Fonts/Creepster-Regular.ttf"
LINES = [("WHEN THEY SCREAM,", 92), ("STAY IN THE LIGHT", 118)]
W, H = 1100, 380
rng = np.random.default_rng(3)

mask = Image.new("L", (W, H))
d = ImageDraw.Draw(mask)
y = 30
for text, size in LINES:
    f = ImageFont.truetype(str(FONT), size)
    w = d.textlength(text, font=f)
    d.text(((W - w) / 2, y), text, font=f, fill=255)
    y += size + 30
# Drips: paint running down from the bottom of some strokes.
a = np.asarray(mask).astype(float) / 255
drip = np.zeros_like(a)
for _ in range(26):
    xs = np.nonzero(a.max(axis=0) > 0.5)[0]
    x = int(rng.choice(xs))
    col = np.nonzero(a[:, x] > 0.5)[0]
    y0 = int(col.max())
    length = int(rng.uniform(12, 60))
    width = rng.uniform(2, 5)
    for k in range(length):
        r = width * (1 - k / length) ** 0.5
        yy = min(H - 1, y0 + k)
        x0, x1 = int(x - r), int(x + r) + 1
        drip[yy, max(0, x0):min(W, x1)] = 1
    # a drop at the end
    cy = min(H - 4, y0 + length)
    drip[max(0, cy - 3):cy + 3, max(0, int(x - width)):int(x + width) + 1] = 1
paint = np.clip(a + drip, 0, 1)
# Worn paint: speckle the alpha.
paint *= np.clip(0.75 + 0.35 * rng.random(paint.shape), 0, 1)
alpha = Image.fromarray((paint * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.6))
img = Image.new("RGBA", (W, H), (190, 26, 18, 0))
img.putalpha(alpha)
img = img.rotate(-3, resample=Image.BICUBIC, expand=False)
img.save(OUT)
print("wrote", OUT)
