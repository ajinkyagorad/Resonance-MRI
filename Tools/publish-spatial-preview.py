from pathlib import Path
import json, shutil, zipfile, hashlib
p = Path(__file__).resolve().parents[1]
v = '0.9.3'
runtime = json.loads((p/'validation/runtime-report.json').read_text())
apkcheck = json.loads((p/'validation/apk-report.json').read_text())
assert runtime['passed'] and runtime['version'] == v and not runtime['errors'] and apkcheck['passed']
for target in ['android','windows']:
    b = json.loads((p/f'validation/build-{target}.json').read_text())
    assert b['result'] == 'Succeeded' and b['errors'] == 0
out = Path('/home/triton/builds/resonance-quest')
shots = Path('/home/triton/builds/shots/resonance-quest')
out.mkdir(exist_ok=True, parents=True); shots.mkdir(exist_ok=True, parents=True)
apk = p/f'Builds/Resonance-MRI-Quest-{v}.apk'
assert hashlib.sha256(apk.read_bytes()).hexdigest() == apkcheck['sha256']
shutil.copy2(apk,out/apk.name)
with zipfile.ZipFile(out/f'Resonance-MRI-Windows-{v}.zip','w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    win=p/f'Builds/Windows-{v}'
    assert (win/'Resonance MRI.exe').is_file() and (win/'UnityPlayer.dll').is_file()
    for f in win.rglob('*'):
        if f.is_file() and not any('DoNotShip' in s or 'DontShip' in s or 'BurstDebugInformation' in s for s in f.parts):
            z.write(f,Path('Resonance MRI')/f.relative_to(win))
    for name in ['README.md','Docs/AI/Revision-0.9.3.md','Docs/AI/SPATIAL-REFERENCE.md','Docs/AI/Spatial-RF-model-0.9.3.md','Docs/AI/TRANSCRIPT-0.9.3.md']:
        z.write(p/name,Path('Resonance MRI')/name)
for f in (p/'validation/renders/cues').glob('*.png'): shutil.copy2(f,shots/f'v{v}-spatial-{f.name}')
shutil.copy2(p/'validation/renders/cues/F01-uniform-grid.png',out/(apk.name+'.png'))
(out/(apk.name+'.notes.txt')).write_text('Inspection preview: persistent 6x6x8 lattice using a regular stereo mesh, single-spin and ideal-volume foundations, T1/T2 demonstrations, Gx/Gy encoding and a two-position signal separation example. Neutral studio previews replace the kitchen composite. Numerical and server runtime checks passed; physical Quest testing remains outstanding. RF uses a wider transmit birdcage and independent transverse surface receiver. No new third-party asset imports. See Revision-0.9.3.md.\n')
for name in ['Spatial-RF-model-0.9.3.md','Revision-0.9.3.md','TRANSCRIPT-0.9.3.md','SPATIAL-REFERENCE.md']:shutil.copy2(p/'Docs/AI'/name,out/(name if '0.9.3' in name else 'Spatial-reference-0.9.3.md'))
for name in ['runtime-report','sim-report','apk-report','build-android','build-windows']:
    shutil.copy2(p/'validation'/(name+'.json'),out/(name+'-0.9.3.json'))
print('Published separate Quest and Windows 0.9.3 previews; older versions preserved.')
