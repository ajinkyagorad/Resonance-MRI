"""Tissue label volume for the simulation (SPEC 3.4): HandLabels.bytes from the BodyParts3D source meshes.

Uses the same object frame, grid and rasterisation as build_anatomy.py (x = right-hand basis vector, y = up/dorsal,
z = wrist toward fingertips; origin 25 mm proximal to the wrist landmark; 144 x 80 x 336 voxels at 1.25 mm) and does not
touch the published OBJ meshes. Class priority: cortex, marrow, tendon, muscle, skin, fat, soft tissue.
Run with the pipeline Python: .pipeline/bin/python Tools/build_labels.py
"""
from pathlib import Path
import zipfile, io, json, struct
import numpy as np
import trimesh
from scipy import ndimage as ndi

root = Path(__file__).resolve().parents[1]
out = root / 'Assets/Resources/Anatomy'
archive = root / 'SourceData/isa_BP3D_4.0_obj_99.zip'
rows = [l.split('\t') for l in (root / 'SourceData/isa_element_parts.txt').read_text().splitlines()[1:]]
zf = zipfile.ZipFile(archive)
wrist = np.array([-254., -119., 804.]); long = np.array([-23., -67., -151.]); long /= np.linalg.norm(long)
up = np.array([0., 1., 0.]); up -= np.dot(up, long) * long; up /= np.linalg.norm(up)
right = np.cross(up, long); basis = np.array([right, up, long]); centre = wrist - long * 25
size = np.array([.18, .10, .42]); shape = np.array([144, 80, 336]); pitch = .00125

def load(ident):
    raw = zf.read('isa_BP3D_4.0_obj_99/' + ident + '.obj')
    m = trimesh.load(io.BytesIO(raw), file_type='obj', force='mesh', process=True)
    m.vertices = (m.vertices - centre) @ basis.T * .001
    for axis in range(3):
        n = np.eye(3)[axis]
        m = m.slice_plane(-size[axis] / 2 * n, n)
        m = m.slice_plane(size[axis] / 2 * n, -n)
    if len(m.faces): m.remove_unreferenced_vertices()
    return m

def raster(m):
    grid = np.zeros(shape, dtype=bool)
    if len(m.faces) == 0: return grid
    vox = m.voxelized(pitch).points
    ij = np.floor((vox + size / 2) / pitch).astype(int)
    valid = ((ij >= 0) & (ij < shape)).all(1); ij = ij[valid]
    grid[tuple(ij.T)] = True
    grid = np.maximum.accumulate(grid, axis=1) & np.maximum.accumulate(grid[:, ::-1, :], axis=1)[:, ::-1, :]
    return grid

bone_names = ['right radius', 'right ulna', 'right scaphoid', 'right lunate', 'right triquetral', 'right pisiform', 'right trapezium', 'right trapezoid', 'right capitate', 'right hamate']
intrinsic = ['abductor digiti minimi of right hand', 'flexor digiti minimi brevis of right hand', 'opponens digiti minimi of right hand', 'set of lumbricals of right hand', 'set of palmar interossei of right hand', 'set of dorsal interossei of right hand']
extrinsic = {'right flexor digitorum superficialis': 'FDS', 'right flexor digitorum profundus': 'FDP', 'right extensor digitorum': 'ED'}

bones = np.zeros(shape, bool); intr = np.zeros(shape, bool); ext = {k: np.zeros(shape, bool) for k in extrinsic.values()}; skin = None
for fma, name, ident in rows:
    isbone = name in bone_names or ('right' in name and ('metacarpal bone' in name or 'phalanx of right' in name and ('finger' in name or 'thumb' in name)))
    if not (isbone or name in intrinsic or name in extrinsic or name == 'skin'): continue
    m = load(ident)
    if not len(m.faces): continue
    if name == 'skin':
        parts = m.split(only_watertight=False); m = max(parts, key=lambda q: len(q.faces)); skin = raster(m)
    elif isbone: bones |= raster(m)
    elif name in intrinsic: intr |= raster(m)
    else: ext[extrinsic[name]] |= raster(m)
    print(name, flush=True)

zc = (np.arange(shape[2]) + .5) * pitch - size[2] / 2
distal = (zc > 0.025)[None, None, :]
extall = ext['FDS'] | ext['FDP'] | ext['ED']
outer = skin | bones | intr | extall
outer = ndi.binary_closing(outer, iterations=1)
depth_skin = ndi.distance_transform_edt(outer, sampling=pitch)
depth_bone = ndi.distance_transform_edt(bones, sampling=pitch)
labels = np.zeros(shape, np.uint8)
free = outer.copy()
def assign(mask, cls):
    global free
    m = mask & free; labels[m] = cls; free &= ~m
assign(bones & (depth_bone < .0013), 6)          # cortical bone
assign(bones, 5)                                 # marrow
assign(extall & distal, 4)                       # tendon distal to the wrist landmark
assign(intr | (extall & ~distal), 3)             # muscle
assign(outer & (depth_skin < .0015), 1)          # skin
assign(outer & (depth_skin < .0040), 2)          # subcutaneous fat
assign(outer, 7)                                 # unsegmented soft tissue
counts = {int(k): int(v) for k, v in zip(*np.unique(labels, return_counts=True))}
def centroid_y(mask):
    ij = np.argwhere(mask); return float((ij[:, 1].mean() + .5) * pitch - size[1] / 2) if len(ij) else float('nan')
checks = dict(ED_y=centroid_y(ext['ED']), FDP_y=centroid_y(ext['FDP']))
checks['dorsal_is_plus_y'] = checks['ED_y'] > checks['FDP_y']
# Thumb side: the first metacarpal's centroid x in the object frame.
head = struct.pack('<4siiif3f', b'RNML', int(shape[0]), int(shape[1]), int(shape[2]), pitch, *(-size / 2))
(out / 'HandLabels.bytes').write_bytes(head + labels.transpose(2, 1, 0).tobytes())
report = dict(counts=counts, names=['air', 'skin', 'fat', 'muscle', 'tendon', 'marrow', 'cortex', 'soft'], **checks)
(root / 'Docs/AI/hand-labels.json').write_text(json.dumps(report, indent=2))
print('LABELS_READY', json.dumps(report), flush=True)
