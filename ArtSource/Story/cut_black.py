"""Cuts a drawing off a pure black (or transparent) background and adds the white sticker outline.

    python -I ArtSource/Story/cut_black.py in.png out.png
"""
import sys
from collections import deque
import numpy as np
from PIL import Image, ImageFilter
src = np.asarray(Image.open(sys.argv[1]).convert('RGBA')).astype(int)
a = src[..., :3]
H, W, _ = a.shape
bgc = (a.max(axis=2) <= 6) | (src[..., 3] < 128)
seen = np.zeros((H, W), bool)
q = deque()
for y in range(H):
    for x in (0, W - 1):
        if bgc[y, x] and not seen[y, x]: seen[y, x] = True; q.append((y, x))
for x in range(W):
    for y in (0, H - 1):
        if bgc[y, x] and not seen[y, x]: seen[y, x] = True; q.append((y, x))
while q:
    y, x = q.popleft()
    for dy, dx in ((1,0),(-1,0),(0,1),(0,-1)):
        v, u = y + dy, x + dx
        if 0 <= v < H and 0 <= u < W and bgc[v, u] and not seen[v, u]:
            seen[v, u] = True; q.append((v, u))
def comps(mask):
    lab = np.zeros(mask.shape, int); n = 0
    for y, x in zip(*np.where(mask)):
        if lab[y, x]: continue
        n += 1; lab[y, x] = n; q = deque([(y, x)])
        while q:
            cy, cx = q.popleft()
            for dy, dx in ((1,0),(-1,0),(0,1),(0,-1)):
                v, u = cy + dy, cx + dx
                if 0 <= v < H and 0 <= u < W and mask[v, u] and not lab[v, u]:
                    lab[v, u] = n; q.append((v, u))
    return lab, n
# Gaps between the arms and the body are black too, just enclosed: big black pockets are background.
lab, n = comps(bgc & ~seen)
for i in range(1, n + 1):
    if (lab == i).sum() > 60: seen |= lab == i
body = ~seen
lab, n = comps(body)
sizes = [(lab == i).sum() for i in range(1, n + 1)]
body = lab == 1 + int(np.argmax(sizes))
# The first picture has a white sticker outline: give this one the same, so frames match.
alpha = Image.fromarray((body * 255).astype(np.uint8))
outline = np.asarray(alpha.filter(ImageFilter.MaxFilter(7))) > 0
rgb = np.where(body[..., None], a, 244)
keep = outline
ys, xs = np.where(keep)

img = np.dstack([rgb, keep * 255]).astype(np.uint8)
Image.fromarray(img).save(sys.argv[2])  # uncropped: frames keep the artist's placement
