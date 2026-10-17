"""The game icon: the player's face, half of it already the shadow, the fire behind.

    python ArtSource/Story/make_icon.py   ->  Assets/Story/Art/Icon/GameIcon.png (1024 px)
"""
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

HERE = Path(__file__).parent
OUT = HERE.parent.parent / "Assets/Story/Art/Icon/GameIcon.png"
N = 1024
SPLIT_X = 140        # the nose, in Player_Cutout.png
SHADOW_EYE = (161, 172)
CROP = (0, 0, 233, 236)  # hat to collar
HAT_CENTRE_X = 124

src = Image.open(HERE / "Player_Cutout.png").convert("RGBA").crop(CROP)
k = 4.25
head = src.convert("RGBa").resize((round(src.width * k), round(src.height * k)), Image.LANCZOS).convert("RGBA")
head = head.filter(ImageFilter.UnsharpMask(3, 80, 2))
h = np.asarray(head).astype(float)
H, W = h.shape[:2]
ox = round(N / 2 - HAT_CENTRE_X * k)
oy = 60                           # collar runs off the bottom edge
ys, xs = np.mgrid[:N, :N].astype(float)

# Background: black, the fire glowing up from below, a few sparks.
bg = np.zeros((N, N, 3))
d = np.hypot((xs - N / 2) / 1.3, ys - N * 1.05) / N
glow = np.clip(1 - d * 1.15, 0, 1) ** 1.6
bg += glow[..., None] * np.array([255, 110, 30]) + (glow ** 3)[..., None] * np.array([0, 90, 60])
bg += np.array([12, 8, 16]) * (1 - glow[..., None])
rng = np.random.default_rng(3)
for _ in range(40):
    sx, sy, r = rng.uniform(40, N - 40), rng.uniform(N * 0.35, N), rng.uniform(2, 6)
    s = np.exp(-((xs - sx) ** 2 + (ys - sy) ** 2) / (2 * r * r)) * rng.uniform(0.5, 1)
    bg = bg * (1 - s[..., None]) + np.array([255, 200, 120]) * s[..., None]

# The head on a full canvas.
layer = np.zeros((N, N, 4))
y0, x0 = max(oy, 0), max(ox, 0)
layer[y0:oy + H, x0:ox + W] = h[y0 - oy:N - oy, x0 - ox:N - ox][: N - y0, : N - x0]
a = layer[..., 3] / 255
rgb = layer[..., :3]

# Right of a ragged smoky line, the player has already become the shadow.
edge = ox + SPLIT_X * k + np.sin(ys / 37) * 14 + np.sin(ys / 13 + 1) * 7
shadow_side = np.clip((xs - edge) / 10 + 0.5, 0, 1)
lum = rgb.mean(axis=2) / 255
dark = np.dstack([10 + (lum - 0.5) * 30, 7 + (lum - 0.5) * 22, 18 + (lum - 0.5) * 40])
rim = np.clip(a - np.asarray(Image.fromarray((a * 255).astype(np.uint8)).filter(ImageFilter.MinFilter(13))) / 255, 0, 1)
dark = dark * (1 - rim[..., None]) + np.array([120, 60, 190]) * rim[..., None]
rgb = rgb * (1 - shadow_side[..., None]) + dark * shadow_side[..., None]
# Smoke curling off the shadow side.
smoke = np.asarray(Image.fromarray((a * shadow_side * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(22))) / 255 * 0.6

# The shadow's eye glows the player's green.
ex, ey = ox + SHADOW_EYE[0] * k, oy + SHADOW_EYE[1] * k
d2 = ((xs - ex) / 1.3) ** 2 + (ys - ey) ** 2
eye = np.clip(np.exp(-d2 / 1800) * 1.1 + np.exp(-d2 / 160), 0, 1)
eye_rgb = np.where((eye > 0.8)[..., None], np.array([240, 255, 245]), np.array([120, 255, 190]))

out = bg * (1 - smoke[..., None]) + np.array([30, 14, 45]) * smoke[..., None]
out = out * (1 - a[..., None]) + rgb * a[..., None]
out = out * (1 - eye[..., None]) + eye_rgb * eye[..., None]
# A little vignette so it sits well on any background.
v = np.clip(1 - (np.hypot(xs - N / 2, ys - N / 2) / (N * 0.75)) ** 3, 0, 1)
out *= (0.55 + 0.45 * v)[..., None]

OUT.parent.mkdir(parents=True, exist_ok=True)
Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)).save(OUT)
print("icon written to", OUT)
