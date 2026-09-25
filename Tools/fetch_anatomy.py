from pathlib import Path
import urllib.request,hashlib,json
r=Path(__file__).resolve().parents[1];folder=r/'SourceData';folder.mkdir(exist_ok=True)
base='https://dbarchive.biosciencedbc.jp/data/bodyparts3d/LATEST/'
for name in ['isa_parts_list_e.txt','isa_element_parts.txt','isa_BP3D_4.0_obj_99.zip']:
 target=folder/name
 if not target.exists():urllib.request.urlretrieve(base+name,target)
 print(name,target.stat().st_size)
record=r/'Docs/AI/anatomy-provenance.json'
if record.exists():
 expected=json.loads(record.read_text())['sha256'];actual=hashlib.sha256((folder/'isa_BP3D_4.0_obj_99.zip').read_bytes()).hexdigest()
 if expected!=actual:raise RuntimeError('Archive changed upstream; review provenance before regenerating anatomy.')
