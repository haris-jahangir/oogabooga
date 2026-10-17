"""The light ending's hospital: the room as pixel art, the player sitting up in bed, waking.

    python -I ArtSource/Story/make_hospital.py  ->  Assets/Story/Art/Ending_Hospital.png

The room is rendered small and blown up 3x (blocky, like the other backdrops); the player is
drawn over it at full detail, with the blanket over their lap. Import at 97.2 px per unit.
"""
import re
import subprocess
from io import BytesIO
from pathlib import Path

from PIL import Image

HERE = Path(__file__).parent
OUT = HERE.parent.parent / "Assets/Story/Art/Ending_Hospital.png"
W, H, UP = 576, 324, 3
BLANKET = re.compile(r'\s*<path d="M780 640[^>]*/>')
PATIENT = re.compile(r'\s*<ellipse cx="760"[^>]*/>\s*<path d="M708 575[^>]*/>|\s*<path d="M742 588[^>]*/>')
# Pulled up over the player's lap.
NEW_BLANKET = '<path d="M672 668 C700 640 790 628 880 632 C1000 600 1200 590 1390 640 L1390 700 L672 700 Z" fill="#d7e9f2" stroke="#3c4645" stroke-width="7" stroke-linejoin="round"/>'
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
player = Image.open(HERE / "Player_Cutout.png").convert("RGBA")
player = player.crop((0, 0, player.width, CROP_ROWS))
k = HEIGHT * s / CROP_ROWS
player = player.convert("RGBa").resize((round(player.width * k), round(CROP_ROWS * k)), Image.LANCZOS).convert("RGBA")
room.alpha_composite(player, (round(CENTRE_X * s - player.width / 2), round(TOP * s)))
room.alpha_composite(blanket)
room.convert("RGB").save(OUT)
print("hospital written to", OUT)
