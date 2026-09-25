"""Brightness and clarity of review renders, and side-by-side sheets (0.8.1).

Usage: python3 Tools/mr_compare.py <out-dir> label=image [label=image ...]
       python3 Tools/mr_compare.py <out-dir> --pairs before-dir after-dir   (RunMR outputs, *-mr-grey.png / *-mr-room.png)

For every image the virtual content is separated from the backdrop (the mid-grey or room the eye buffer was composited
over, or the grey VR studio of the 0.7.0 renders, taken as the most common luminance). Over the content pixels:
  mean_Y      mean relative luminance (0-1),
  p90_Y       90th percentile luminance,
  darkened    share of content pixels darker than the backdrop behind them (what makes passthrough look dimmed),
  contrast    RMS contrast of the content (std / mean luminance),
  edges       mean Sobel gradient of luminance over the content (crispness).
"""
import json, sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont


def luminance(rgb):
    c = rgb / 255.0
    lin = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    return 0.2126 * lin[..., 0] + 0.7152 * lin[..., 1] + 0.0722 * lin[..., 2]


def metrics(path):
    rgb = np.asarray(Image.open(path).convert('RGB'), dtype=np.float64)
    Y = luminance(rgb)
    # Backdrop: the most common luminance (the room), per row band to allow a lit wall over a darker floor.
    rows = np.array_split(np.arange(Y.shape[0]), 12)
    bg = np.zeros_like(Y)
    for r in rows:
        band = Y[r]
        hist, edges = np.histogram(band, bins=256, range=(0, 1))
        mode = edges[np.argmax(hist)] + 0.5 / 256
        bg[r] = mode
    diff = np.abs(rgb - rgb.mean())  # unused scale reference
    content = np.abs(Y - bg) > 0.012
    n = int(content.sum())
    if n == 0:
        return dict(content_share=0.0)
    Yc = Y[content]
    gx = np.zeros_like(Y); gy = np.zeros_like(Y)
    gx[1:-1, 1:-1] = (Y[:-2, 2:] + 2 * Y[1:-1, 2:] + Y[2:, 2:]) - (Y[:-2, :-2] + 2 * Y[1:-1, :-2] + Y[2:, :-2])
    gy[1:-1, 1:-1] = (Y[2:, :-2] + 2 * Y[2:, 1:-1] + Y[2:, 2:]) - (Y[:-2, :-2] + 2 * Y[:-2, 1:-1] + Y[:-2, 2:])
    g = np.hypot(gx, gy)
    return dict(content_share=round(n / Y.size, 4), mean_Y=round(float(Yc.mean()), 4), p90_Y=round(float(np.percentile(Yc, 90)), 4),
                darkened=round(float((Y[content] < bg[content] * 0.9).mean()), 4), contrast=round(float(Yc.std() / max(1e-6, Yc.mean())), 3),
                edges=round(float(g[content].mean()), 4), backdrop_Y=round(float(np.median(bg)), 4))


def sheet(pairs, out, title):
    """pairs: list of (label, path). One row, scaled to 640 px wide each, labels above."""
    ims = [Image.open(p).convert('RGB') for _, p in pairs]
    w = 640; hs = [int(im.height * w / im.width) for im in ims]; h = max(hs)
    canvas = Image.new('RGB', (w * len(ims), h + 34), (24, 26, 30))
    d = ImageDraw.Draw(canvas)
    try: font = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf', 18)
    except OSError: font = ImageFont.load_default()
    for k, (im, (label, _)) in enumerate(zip(ims, pairs)):
        canvas.paste(im.resize((w, hs[k])), (k * w, 34))
        d.text((k * w + 8, 7), label, fill=(235, 238, 242), font=font)
    canvas.save(out)


def main():
    out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
    if len(sys.argv) > 2 and sys.argv[2] == '--pairs':
        before, after = Path(sys.argv[3]), Path(sys.argv[4]); res = {}
        for a in sorted(after.glob('*-mr-grey.png')):
            name = a.name.replace('-mr-grey.png', '')
            b = before / a.name
            row = {'after_grey': metrics(a), 'after_room': metrics(after / (name + '-mr-room.png'))}
            if b.exists():
                row['before_grey'] = metrics(b); row['before_room'] = metrics(before / (name + '-mr-room.png'))
                sheet([('0.8.0 over grey', b), ('0.8.1 over grey', a)], out / f'{name}-before-after.png', name)
            res[name] = row
        (out / 'mr-before-after.json').write_text(json.dumps(res, indent=1))
        print(json.dumps(res, indent=1)[:4000])
        return
    items = [a.split('=', 1) for a in sys.argv[2:]]
    res = {label: metrics(Path(path)) for label, path in items}
    (out / 'metrics.json').write_text(json.dumps(res, indent=1))
    print(json.dumps(res, indent=1))


if __name__ == '__main__':
    main()
