from pathlib import Path
import zipfile,io,json,hashlib,struct
import numpy as np
import trimesh
from scipy import ndimage as ndi
from skimage.measure import marching_cubes
root=Path(__file__).resolve().parents[1]; out=root/'Assets/Resources/Anatomy';out.mkdir(parents=True,exist_ok=True)
archive=root/'SourceData/isa_BP3D_4.0_obj_99.zip'
rows=[l.split('\t') for l in (root/'SourceData/isa_element_parts.txt').read_text().splitlines()[1:]]
zf=zipfile.ZipFile(archive)
wrist=np.array([-254.,-119.,804.]);long=np.array([-23.,-67.,-151.]);long/=np.linalg.norm(long)
up=np.array([0.,1.,0.]);up-=np.dot(up,long)*long;up/=np.linalg.norm(up)
right=np.cross(up,long);basis=np.array([right,up,long]);centre=wrist-long*25
size=np.array([.18,.10,.42]);shape=np.array([144,80,336]);pitch=.00125
def load(ident):
 raw=zf.read('isa_BP3D_4.0_obj_99/'+ident+'.obj')
 m=trimesh.load(io.BytesIO(raw),file_type='obj',force='mesh',process=True)
 m.vertices=(m.vertices-centre)@basis.T*.001
 # Clip distal forearm consistently with the physical imaging FOV.
 for axis in range(3):
  n=np.eye(3)[axis]
  m=m.slice_plane(-size[axis]/2*n,n)
  m=m.slice_plane(size[axis]/2*n,-n)
 if len(m.faces): m.remove_unreferenced_vertices()
 return m
def raster(m):
 grid=np.zeros(shape,dtype=bool)
 if len(m.faces)==0:return grid
 vox=m.voxelized(pitch).points
 ij=np.floor((vox+size/2)/pitch).astype(int)
 valid=((ij>=0)&(ij<shape)).all(1);ij=ij[valid]
 grid[tuple(ij.T)]=True
 # Close the interior along dorsal-palmar voxel columns, retaining inter-finger gaps.
 grid=np.maximum.accumulate(grid,axis=1)&np.maximum.accumulate(grid[:,::-1,:],axis=1)[:,::-1,:]
 return grid

manifest=[];bones=np.zeros(shape,bool);muscle=np.zeros(shape,bool)
bone_names=['right radius','right ulna','right scaphoid','right lunate','right triquetral','right pisiform','right trapezium','right trapezoid','right capitate','right hamate']
muscle_names=['abductor digiti minimi of right hand','flexor digiti minimi brevis of right hand','opponens digiti minimi of right hand','set of lumbricals of right hand','set of palmar interossei of right hand','set of dorsal interossei of right hand','right flexor digitorum superficialis','right flexor digitorum profundus','right extensor digitorum']
for fma,name,ident in rows:
 kind='bone' if name in bone_names or ('right' in name and ('metacarpal bone' in name or 'phalanx of right' in name and ('finger' in name or 'thumb' in name))) else 'muscle' if name in muscle_names else 'skin' if name=='skin' else None
 if kind is None:continue
 m=load(ident)
 if not len(m.faces):continue
 if kind=='skin':
  parts=m.split(only_watertight=False);m=max(parts,key=lambda q:len(q.faces))
  skin=raster(m);fname='Skin.obj'
 else:
  g=raster(m)
  if kind=='bone':bones|=g
  else:muscle|=g
  fname=('Bone_' if kind=='bone' else 'Muscle_')+ident+'.obj'
 # Subdivision and bounded smoothing improve curvature on the anatomical surface.
 if len(m.faces)<15000:
  for step in range(2 if kind=='bone' else 1):
   v,f=trimesh.remesh.subdivide(m.vertices,m.faces);m=trimesh.Trimesh(v,f,process=False)
  trimesh.smoothing.filter_taubin(m,lamb=.4,nu=.43,iterations=6)
 # Explicit normals avoid faceted OBJ imports.
 (out/fname).write_text(trimesh.exchange.obj.export_obj(m,include_normals=True))

 manifest.append(dict(file=fname,name=name,fma=fma,element=ident,vertices=len(m.vertices),triangles=len(m.faces)))
 print(kind,name,len(m.faces),flush=True)
skin|=bones|muscle
skin=ndi.binary_closing(skin,iterations=1)
cortex=bones & (ndi.distance_transform_edt(bones,sampling=pitch)<.0016)
marrow=bones & ~cortex
fat=skin & (ndi.distance_transform_edt(skin,sampling=pitch)<.0035) & ~bones
# Tissue classes and relaxation values for 0.8.0 come from Tools/build_labels.py (HandLabels.bytes) and Sim/Tissue.cs;
# the old illustrative parameter volume (HandTissue.bytes) is no longer produced.
manifest_doc=dict(source='BodyParts3D 4.0, official LATEST archive downloaded 2026-09-22',license='CC BY 4.0',attribution='BodyParts3D, © The Database Center for Life Science licensed under CC Attribution 4.0 International',source_url='https://dbarchive.biosciencedbc.jp/en/bodyparts3d/download.html',license_url='https://dbarchive.biosciencedbc.jp/en/bodyparts3d/lic.html',sha256=hashlib.sha256(archive.read_bytes()).hexdigest(),transform=dict(centre_mm=centre.tolist(),basis=basis.tolist(),units='metres'),changes='Right hand and distal forearm isolated; physical-scale rigid transform; surface subdivision and mild smoothing. Interiors filled along dorsal-palmar columns. Cortical shell, marrow and subcutaneous fat segmentation derived from source surfaces. Tissue parameters are illustrative, not measured patient MRI.',dimensions=shape.tolist(),spacing_mm=1.25,structures=manifest,voxel_counts=dict(skin=int(skin.sum()),bone=int(bones.sum()),marrow=int(marrow.sum()),cortex=int(cortex.sum())))
(root/'Docs/AI/anatomy-provenance.json').write_text(json.dumps(manifest_doc,indent=2))
(out/'AnatomyManifest.json').write_text(json.dumps(manifest_doc,indent=2))
(root/'Assets/StreamingAssets/Notices/anatomy-provenance.json').write_text(json.dumps(manifest_doc,indent=2))
print('ANATOMY_READY',manifest_doc['voxel_counts'],flush=True)
