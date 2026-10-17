"""Builds the player's animation frames and the shadow's from Player_Cutout.png.

    python ArtSource/Story/make_characters.py

Writes Assets/Story/Art/Player/*.png and Assets/Story/Art/Shadow/*.png (4 frames per animation,
all the same canvas so nothing jitters). The pivot is the canvas centre = the body's centre.
Imported at 137 px per unit the player is 1.4 units tall (fits a 0.55 x 1.4 collider); the
shadow at 91 px per unit is 2.1.
"""
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

HERE = Path(__file__).parent
OUT = HERE.parent.parent / "Assets/Story/Art"
BODY = 192            # body height in pixels (1.4 units at 137 PPU)
CANVAS = (160, 240)   # body centred, room to stretch and tilt
FEET = (CANVAS[0] // 2, CANVAS[1] // 2 + BODY // 2)
EYES = [(114, 168), (161, 172)]  # in Player_Cutout.png

src = Image.open(HERE / "Player_Cutout.png").convert("RGBA")
scale = BODY / src.height


def hat_x(image):
    """The hat's centre: lined up in every frame so the head doesn't jitter."""
    a = np.asarray(image)[: image.height // 5, :, 3] > 0
    return np.nonzero(a)[1].mean()


def resize(im, w, h):
    return im.convert("RGBa").resize((max(1, round(w)), max(1, round(h))), Image.LANCZOS).convert("RGBA")


def pose(sx=1.0, sy=1.0, angle=0.0, lift=0.0, dx=0.0, image=src):
    """The body squashed/stretched from the feet, raised, shifted and tilted around the feet."""
    w, h = image.width * scale * sx, image.height * scale * sy
    body = resize(image, w, h)
    canvas = Image.new("RGBA", CANVAS)
    canvas.alpha_composite(body, (round(FEET[0] - hat_x(image) * scale * sx + dx), round(FEET[1] - body.height - lift)))
    if angle:
        canvas = canvas.rotate(angle, resample=Image.BICUBIC, center=FEET)
    return canvas


def tint(im, rgb, amount):
    a = np.asarray(im).astype(float)
    a[..., :3] = a[..., :3] * (1 - amount) + np.array(rgb) * amount
    return Image.fromarray(a.astype(np.uint8))


def save(frames, folder, name):
    (OUT / folder).mkdir(parents=True, exist_ok=True)
    for i, f in enumerate(frames):
        f.save(OUT / folder / f"{name}_{i}.png")


# ---- player ----
save([pose(1 - d, 1 + 2 * d) for d in (0, 0.006, 0.012, 0.006)], "Player", "Player_Idle")
# The artist's hand-drawn walk (Walk/Walk_0..3, cut by cut_black.py). All four share one placement,
# so the stride's own bob and sway stay as drawn: the lowest foot of the cycle stands on the ground.
walk = [Image.open(HERE / f"Walk/Walk_{i}.png").convert("RGBA") for i in range(4)]
ground = max(np.nonzero(np.asarray(w)[..., 3].any(1))[0][-1] for w in walk) + 1
centre = np.mean([hat_x(w.crop((0, np.nonzero(np.asarray(w)[..., 3].any(1))[0][0], w.width, w.height))) for w in walk])
walk_frames = []
for w in walk:
    big = resize(w, w.width * scale, w.height * scale)
    canvas = Image.new("RGBA", CANVAS)
    canvas.alpha_composite(big, (round(FEET[0] - centre * scale), round(FEET[1] - ground * scale)))
    walk_frames.append(canvas)
save(walk_frames, "Player", "Player_Walk")
# Played once, holding the last frame: push off, stretch, float, fall.
save([pose(1.1, 0.88), pose(0.9, 1.12), pose(0.97, 1.04), pose(1.0, 1.0, -3)], "Player", "Player_Jump")
pale = tint(src, (170, 190, 235), 0.25)
save([pose(1.04, 0.94, 0, 0, d, pale) for d in (-4, 4, -3, 3)], "Player", "Player_Scare")

# ---- shadow: the same silhouette, made of smoke, with the player's eyes ----
SH_BODY, PAD = 192, 28
sh_scale = SH_BODY / src.height
alpha = np.asarray(resize(src, src.width * sh_scale, SH_BODY))[..., 3].astype(float) / 255
lum = np.asarray(resize(src, src.width * sh_scale, SH_BODY).convert("L")).astype(float) / 255
H, W = alpha.shape
CH, CW = H + 2 * PAD, W + 2 * PAD
ys, xs = np.mgrid[:CH, :CW].astype(float)
eyes = [((ex * sh_scale) + PAD, (ey * sh_scale) + PAD) for ex, ey in EYES]


def place(a):
    c = np.zeros((CH, CW))
    c[PAD:PAD + H, PAD:PAD + W] = a
    return c


body_a, body_l = place(alpha), place(lum)
for k in range(4):
    ph = 2 * math.pi * k / 4
    depth = np.clip((ys - PAD) / H, 0, 1)                      # 0 at the head, 1 at the feet
    # The edges ripple like smoke, more towards the bottom.
    shift = (np.sin(ys / 9 + ph) * 1.2 + np.sin(ys / 4.3 - 2 * ph) * 0.6) * (0.4 + depth)
    a = np.zeros_like(body_a)
    l = np.zeros_like(body_l)
    for y in range(CH):
        a[y] = np.interp(xs[y] - shift[y, 0], xs[y], body_a[y])
        l[y] = np.interp(xs[y] - shift[y, 0], xs[y], body_l[y])
    # The legs dissolve into rising smoke.
    noise = 0.5 + 0.3 * np.sin(xs / 5.1 + 2.2 * np.sin(ys / 11 + ph)) + 0.2 * np.sin(ys / 6.4 + xs / 13 + ph)
    fade = np.clip((depth - 0.55) / 0.45, 0, 1)
    a *= np.clip((noise - fade) * 3 + 1 - fade, 0, 1)
    # Body: near black, a ghost of the player's own lines; a violet rim; a soft smoky halo.
    core = np.dstack([10 + (l - 0.5) * 22, 7 + (l - 0.5) * 16, 16 + (l - 0.5) * 30])
    rim = np.clip(a - np.asarray(Image.fromarray((a * 255).astype(np.uint8)).filter(ImageFilter.MinFilter(5))) / 255, 0, 1)
    rgb = core * (1 - rim[..., None]) + np.array([80, 45, 120]) * rim[..., None]
    halo = np.asarray(Image.fromarray((a * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(7))).astype(float) / 255 * 0.5
    out_a = a + halo * (1 - a)
    rgb = (rgb * a[..., None] + np.array([25, 12, 40]) * (halo * (1 - a))[..., None]) / np.maximum(out_a, 1e-6)[..., None]
    # The eyes: the player's green, glowing. They flicker a little.
    glow_rgb = np.zeros((CH, CW, 3))
    glow_a = np.zeros((CH, CW))
    for ex, ey in eyes:
        d2 = ((xs - ex) / 1.3) ** 2 + (ys - ey) ** 2
        g = np.exp(-d2 / 30) * (1.0, 0.85, 1.0, 0.7)[k] + np.exp(-d2 / 2.5)
        glow_a = np.maximum(glow_a, np.clip(g, 0, 1))
    glow_rgb[:] = np.array([140, 255, 200])
    glow_rgb[glow_a > 0.85] = np.array([235, 255, 245])
    rgb = rgb * (1 - glow_a[..., None]) + glow_rgb * glow_a[..., None]
    out_a = np.maximum(out_a, glow_a * (body_a > 0.1))
    img = np.dstack([np.clip(rgb, 0, 255), np.clip(out_a, 0, 1) * 255]).astype(np.uint8)
    (OUT / "Shadow").mkdir(parents=True, exist_ok=True)
    Image.fromarray(img).save(OUT / "Shadow" / f"Shadow_{k}.png")

print("frames written to", OUT)
