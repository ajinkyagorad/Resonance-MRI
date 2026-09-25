#!/usr/bin/env python3
"""Decimates the displayed anatomy (bones, muscles and skin) for the headset's frame budget (0.8.2; muscles 0.8.3).

build_anatomy.py writes subdivided, smoothed surfaces: about 226 000 triangles for the bones and skin, which the scanner
draws at a few centimetres. This keeps about 10 % of each surface's triangles by quadric edge collapse
(fast-simplification), recomputes smooth vertex normals and rewrites the OBJ files in place, keeping the winding.
The tissue labels are voxelized from the source archive (build_labels.py), not from these files, so the simulation is
unchanged. 0.8.3 draws the muscles again (as in 0.5.0), decimated the same way.

Run after build_anatomy.py. Files already decimated (first line) are skipped. Updates the triangle counts and the change
note in AnatomyManifest.json and both copies of anatomy-provenance.json.
"""
import json
from pathlib import Path

import numpy as np
import fast_simplification

root = Path(__file__).resolve().parents[1]
anatomy = root / 'Assets/Resources/Anatomy'
KEEP, FLOOR = 0.10, 240
NOTE = ' Displayed bone and skin surfaces decimated to about 10 % of their triangles (quadric edge collapse) for the headset frame budget.'
NOTE_MUSCLE = ' Muscle surfaces decimated the same way (0.8.3).'
MARK = '# decimated by Tools/decimate_anatomy.py'


def read_obj(path):
    v, f = [], []
    for line in path.read_text().splitlines():
        if line.startswith('v '):
            v.append([float(x) for x in line.split()[1:4]])
        elif line.startswith('f '):
            f.append([int(tok.split('/')[0]) - 1 for tok in line.split()[1:4]])
    return np.array(v, dtype=np.float64), np.array(f, dtype=np.int64)


def normals(v, f):
    fn = np.cross(v[f[:, 1]] - v[f[:, 0]], v[f[:, 2]] - v[f[:, 0]])  # area-weighted
    n = np.zeros_like(v)
    for k in range(3):
        np.add.at(n, f[:, k], fn)
    return n / np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-12)


def nearest(points, v):
    """Index of the nearest vertex of v for each point (chunked brute force)."""
    idx = np.empty(len(points), dtype=np.int64)
    for k in range(0, len(points), 256):
        d = ((points[k:k + 256, None, :] - v[None, :, :]) ** 2).sum(-1)
        idx[k:k + 256] = d.argmin(1)
    return idx


def main():
    counts = {}
    for path in sorted(anatomy.glob('*.obj')):
        if not (path.name.startswith('Bone_') or path.name.startswith('Muscle_') or path.name == 'Skin.obj'):
            continue
        if path.read_text().split('\n', 1)[0].startswith(MARK):
            v, f = read_obj(path); counts[path.name] = (len(v), len(f))
            print(f'{path.name}: already decimated ({len(f)} triangles)'); continue
        v, f = read_obj(path)
        target = max(FLOOR, int(round(KEEP * len(f))))
        v2, f2 = fast_simplification.simplify(v.astype(np.float32), f.astype(np.int32), 1 - target / len(f), agg=5)
        v2 = v2.astype(np.float64); f2 = f2.astype(np.int64)
        # The surfaces are open (cropped), so check the winding and shape face by face against the original normals.
        n0 = normals(v, f); cen = v2[f2].mean(1); near = nearest(cen, v)
        fn = np.cross(v2[f2[:, 1]] - v2[f2[:, 0]], v2[f2[:, 2]] - v2[f2[:, 0]])
        agree = float((np.einsum('ij,ij->i', fn, n0[near]) > 0).mean())
        dist = float(np.sqrt(((cen - v[near]) ** 2).sum(1)).mean())
        assert agree > 0.8 and dist < 1e-3, (path.name, agree, dist)  # an inverted surface would score near 0; thin sheets score lower
        n2 = normals(v2, f2)
        out = [f'{MARK} from {len(f)} triangles (BodyParts3D, CC BY 4.0)']
        out += [f'v {x:.8f} {y:.8f} {z:.8f}' for x, y, z in v2]
        out += [f'vn {x:.6f} {y:.6f} {z:.6f}' for x, y, z in n2]
        out += [f'f {a + 1}//{a + 1} {b + 1}//{b + 1} {c + 1}//{c + 1}' for a, b, c in f2]
        path.write_text('\n'.join(out) + '\n')
        counts[path.name] = (len(v2), len(f2))
        print(f'{path.name}: {len(f)} -> {len(f2)} triangles; {agree:.1%} of faces face as before; centroids {dist * 1e3:.2f} mm from the original vertices')
    for doc in (anatomy / 'AnatomyManifest.json', root / 'Docs/AI/anatomy-provenance.json', root / 'Assets/StreamingAssets/Notices/anatomy-provenance.json'):
        if not doc.exists():
            continue
        d = json.loads(doc.read_text())
        for s in d.get('structures', []):
            if s['file'] in counts:
                s['vertices'], s['triangles'] = counts[s['file']]
        if NOTE.strip() not in d.get('changes', ''):
            d['changes'] = d.get('changes', '') + NOTE
        if any(f.startswith('Muscle_') for f in counts) and NOTE_MUSCLE.strip() not in d.get('changes', ''):
            d['changes'] = d.get('changes', '') + NOTE_MUSCLE
        text = doc.read_text()
        doc.write_text(json.dumps(d, indent=2) + ('\n' if text.endswith('\n') else ''))
    total = sum(t for _, t in counts.values())
    if counts:
        print(f'{len(counts)} surfaces, {total} triangles')


if __name__ == '__main__':
    main()
