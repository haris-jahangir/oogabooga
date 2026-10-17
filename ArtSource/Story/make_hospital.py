"""The light ending's hospital: the room as pixel art, the player sitting up in bed, waking.

    python -I ArtSource/Story/make_hospital.py  ->  Assets/Story/Art/Ending_Hospital.png

The room is rendered small and blown up 3x (blocky, like the other backdrops); the player is
drawn over it at full detail, with the blanket over their lap. Import at 97.2 px per unit.
"""
import re
import subprocess
from io import BytesIO
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

HERE = Path(__file__).parent
OUT = HERE.parent.parent / "Assets/Story/Art/Ending_Hospital.png"
W, H, UP = 576, 324, 3
BLANKET = re.compile(r'\s*<path d="M780 640[^>]*/>')
PATIENT = re.compile(r'\s*<ellipse cx="760"[^>]*/>\s*<path d="M708 575[^>]*/>|\s*<path d="M742 588[^>]*/>')
# Pulled up over the player's lap.
NEW_BLANKET = '<path d="M672 668 C700 640 790 628 880 632 C1000 600 1200 590 1390 640 L1390 700 L672 700 Z" fill="#d7e9f2" stroke="#3c4645" stroke-width="7" stroke-linejoin="round"/>'
SKIN, LINE = (243, 231, 227), (63, 16, 8)


def drowsy(player):
    """Just woken, worn out, relieved: heavy half-closed lids over the eyes, and a small tired
    mouth instead of the smirk. Coordinates are in Player_Cutout.png."""
    a = np.asarray(player).copy()
    for (x0, x1, top, lid) in ((98, 121, 161, 169), (153, 171, 164, 171)):
        mask = Image.new("L", player.size)
        ImageDraw.Draw(mask).polygon([(x0, top), (x1, top), (x1, lid), ((x0 + x1) / 2, lid + 1), (x0, lid)], fill=255)
        m = np.asarray(mask) > 0
        a[m, :3] = (228, 210, 208)  # lid: a shade darker than the face
    player = Image.fromarray(a)
    d = ImageDraw.Draw(player)
    for (x0, x1, lid) in ((98, 121, 169), (153, 171, 171)):
        d.line([(x0, lid - 1), ((x0 + x1) / 2, lid + 1), (x1, lid)], fill=LINE, width=3, joint="curve")
    d.rectangle([125, 189, 151, 202], fill=SKIN)
    d.line([(133, 197), (138, 198), (143, 197)], fill=LINE, width=2, joint="curve")
    return player


# Hat to waist in Player_Cutout.png, and where it goes (in the 1920x1080 drawing).
CROP_ROWS, TOP, CENTRE_X, HEIGHT = 300, 330, 765, 330

svg = (HERE / "Ending_Hospital.svg").read_text()
room_svg = PATIENT.sub("", BLANKET.sub("", svg))
blanket_svg = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1920 1080" width="1920" height="1080">' + NEW_BLANKET + "</svg>"


def render(text):
    png = subprocess.run(["rsvg-convert", "-w", str(W), "-h", str(H)], input=text.encode(), capture_output=True, check=True).stdout
    im = Image.open(BytesIO(png)).convert("RGBA")
    alpha = im.getchannel("A").point(lambda a: 255 if a > 127 else 0)
    im = im.convert("RGB").quantize(64, Image.Quantize.FASTOCTREE).convert("RGBA")
    im.putalpha(alpha)
    return im.resize((W * UP, H * UP), Image.NEAREST)


room, blanket = render(room_svg), render(blanket_svg)
s = W * UP / 1920
player = drowsy(Image.open(HERE / "Player_Cutout.png").convert("RGBA"))
player = player.crop((0, 0, player.width, CROP_ROWS))
k = HEIGHT * s / CROP_ROWS
player = player.convert("RGBa").resize((round(player.width * k), round(CROP_ROWS * k)), Image.LANCZOS).convert("RGBA")
room.alpha_composite(player, (round(CENTRE_X * s - player.width / 2), round(TOP * s)))
room.alpha_composite(blanket)
room.convert("RGB").save(OUT)
print("hospital written to", OUT)
