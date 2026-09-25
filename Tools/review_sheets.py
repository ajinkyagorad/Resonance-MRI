"""Compose the review sheet of the key views from the validation renders (0.8.2: about ten views per iteration).

The sheet shows each key view as rendered from the default head pose, with the spoken sentence, its referents and the
physical time printed underneath (on the sheet only; the app has no such text).
Usage: python3 Tools/review_sheets.py [validation/render-manifest.json]
"""
from pathlib import Path
import json, sys, textwrap
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[1]
manifest = json.loads(Path(sys.argv[1] if len(sys.argv) > 1 else root / 'validation/render-manifest.json').read_text())
font_path = root / 'Assets/Resources/AtlasSans.ttf'
font = ImageFont.truetype(str(font_path), 22); small = ImageFont.truetype(str(font_path), 17)
shots = manifest['shots']
cols, tw, th = 5, 720, 450
rows = (len(shots) + cols - 1) // cols
cell_h = th + 96
sheet = Image.new('RGB', (cols * tw, rows * cell_h + 50), (12, 14, 17))
d = ImageDraw.Draw(sheet)
d.text((12, 12), 'Key views', fill=(220, 224, 228), font=font)
for i, s in enumerate(shots):
    x, y = (i % cols) * tw, 50 + (i // cols) * cell_h
    im = Image.open(root / s['file']).convert('RGB').resize((tw, th), Image.LANCZOS)
    sheet.paste(im, (x, y))
    t = s['t']; tt = f"{t * 1e6:.3f} us" if t < 1e-3 else f"{t * 1e3:.3f} ms" if t < 1 else f"{t:.3f} s"
    head = f"{s['cue'] or 'overview'}  [{', '.join(s['refs'])}]  t = {tt}{'  lab frame' if s['lab'] else ''}"
    d.text((x + 8, y + th + 4), head, fill=(150, 156, 162), font=small)
    for k, line in enumerate(textwrap.wrap(s['text'], 64)[:3]):
        d.text((x + 8, y + th + 26 + 22 * k), line, fill=(225, 228, 232), font=small)
out = root / 'validation/renders/key-views.png'
sheet.save(out)
print('wrote', out)
