from pathlib import Path
import json,shutil,hashlib,zipfile,struct
# Publishes Resonance MRI 0.8.4 next to the earlier releases (nothing earlier is removed or overwritten except the
# unversioned "latest" copies README.md, VALIDATION.md and the *-report.json files, which always describe the newest).
V='0.8.4'
root=Path('/home/triton/projects/ResonanceQuest');build=Path('/home/triton/builds/resonance-quest');shots=Path('/home/triton/builds/shots/resonance-quest')
runtime=json.loads((root/'validation/runtime-report.json').read_text());apkcheck=json.loads((root/'validation/apk-report.json').read_text())
sim=json.loads((root/'validation/sim-report.json').read_text())
android=json.loads((root/'validation/build-android.json').read_text());windows=json.loads((root/'validation/build-windows.json').read_text())
assert runtime['passed'] and runtime['version']==V and apkcheck['passed'] and not sim['failures']
assert android['result']=='Succeeded' and windows['result']=='Succeeded' and android['errors']==0 and windows['errors']==0
apk=root/f'Builds/Resonance-MRI-Quest-{V}.apk';assert hashlib.sha256(apk.read_bytes()).hexdigest()==apkcheck['sha256']
win=root/f'Builds/Windows-{V}';exe=win/'Resonance MRI.exe';data=exe.read_bytes();offset=struct.unpack_from('<I',data,0x3c)[0]
assert data[:2]==b'MZ' and struct.unpack_from('<H',data,offset+4)[0]==0x8664
assert (win/'UnityPlayer.dll').is_file() and (win/'Resonance MRI_Data').is_dir()
for old in ['Resonance-MRI-Quest-0.7.0.apk','Resonance-MRI-Windows-0.7.0.zip','Resonance-MRI-Quest-0.8.0.apk','Resonance-MRI-Windows-0.8.0.zip','Resonance-MRI-Quest-0.8.1.apk','Resonance-MRI-Windows-0.8.1.zip','Resonance-MRI-Quest-0.8.3.apk','Resonance-MRI-Windows-0.8.3.zip']:assert (build/old).is_file(),'earlier release missing: '+old
build.mkdir(parents=True,exist_ok=True);shots.mkdir(parents=True,exist_ok=True)
docs=[f'Revision-{V}.md','Revision-0.8.3.md','Revision-0.8.1.md','Revision-0.8.0.md','Scanner-model.md','Receiver-reference.md','Asset-publication-audit.md','Spec-0.8.0.md']
shutil.copy2(apk,build/apk.name)
zipname=f'Resonance-MRI-Windows-{V}.zip'
with zipfile.ZipFile(build/zipname,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 for file in win.rglob('*'):
  if file.is_file() and not any('DoNotShip' in s or 'DontShip' in s or 'BurstDebugInformation' in s for s in file.parts):z.write(file,Path('Resonance MRI')/file.relative_to(win))
 z.write(root/'README.md','Resonance MRI/README.md')
 for doc in docs:z.write(root/'Docs/AI'/doc,'Resonance MRI/Docs/AI/'+doc)
# Renders of this release only (0.8.2: the overview and nine key views, and one sheet of them).
cues=sorted((root/'validation/renders/cues').glob('*.png'));sheet=root/'validation/renders/key-views.png'
assert len(cues)==10 and sheet.is_file(),(len(cues),sheet.is_file())
for file in cues:shutil.copy2(file,shots/f'v{V}-view-{file.name}')
shutil.copy2(sheet,shots/f'v{V}-key-views.png')
shutil.copy2(root/'validation/renders/cues/00-overview.png',build/(apk.name+'.png'))
shutil.copy2(root/'README.md',build/'README.md')
notes=f"""Resonance MRI {V}: fixes from the 0.8.3 headset review.
A proper introduction: what an MRI scanner is, its parts (magnet, gradient coils, RF coil, receiver) and every view and plot, before the physics; each sentence points at what it names, and every number it speaks is on screen.
Solid, saturated colours and no grey: a cyan cutaway magnet, gradient coils in their axis colours (x red, y green, z blue; B0 is cyan), a blue base, dark backings.
Protons coloured by precession phase and brightened by tip angle, inside a clearly framed block of the hand whose matching outline in the scanner is joined to it by zoom lines; both turn together.
Gradients shown in the block: an arrow in the gradient's colour toward the stronger field, with the frequency at both ends, while the colours twist along it.
Plots on solid dark panels with thick lines and large labels; k-space as a solid relief; images in a violet-to-white map.
Free 3-axis rotation when grabbed and on the desktop; all text about twice as large, with dark outlines.
Private debug-signed update (versionCode 13). Headset frame rate still needs checking on device.
"""
(build/(apk.name+'.notes.txt')).write_text(notes)
checks=runtime['checks'];fails=runtime['failures']
cost=json.loads((root/'validation/frame-cost.json').read_text())
rows="\n".join(f"| {p['cue']} | {p['main_thread_ms']:.2f} | {p['cube_ms']:.2f} | {p['scanner_ms']:.2f} | {p['plots_ms']:.2f} | {p['worker_ms']:.2f} / {p['worker_hand_ms']:.2f} | {p['alloc_bytes_per_frame']:.0f} | {p['fragments_per_pixel']:.2f} | {p['raymarch_samples_per_frame_quest3']/1e6:.1f} M | {p['triangles_per_eye']/1e3:.0f} k / {p['draws_per_eye']} |" for p in cost['points'])
report=f"""# Resonance MRI {V} validation

Status: server validation passed; physical Quest validation not run.

- Simulation core (dotnet, the same C# sources as the app): {len(sim['checks'])} checks passed, {sim['failures'] if isinstance(sim['failures'],int) else len(sim['failures'])} failed.
- Unity {runtime['unity']} runtime harness: {len(checks)} checks passed, {len(fails)} failed. Layout of all seven items, lesson physics, gradient ramps measured on the field lines, every coil visible at every cue, all 96 sentences lighting exactly their referents, the two-step introduction, the random-position proton volume, proton selection and close-up, controls, tissue resampling, quality settings, palette, text, allocation and submitted geometry.
- {len(runtime['shots'])} review renders: the overview and nine key views, each the mixed-reality eye buffer composited exactly as the headset composites it over passthrough, over a bright room photograph (CC0), and one sheet of them.
- Native Android ARM64 IL2CPP build succeeded with {android['errors']} errors and {android['warnings']} reported warnings. {len(apkcheck['checks'])} APK identity, ABI, signing, manifest, notice and packed-resource checks passed.
- Windows x64 Mono build succeeded with {windows['errors']} errors; PE architecture, UnityPlayer and data folder verified. It was not launched on a Windows device.
- Rendering used {runtime['renderer']}. These are software renders, not Quest captures.
- App runtime errors: {len(runtime['errors'])}. Separately recorded Editor environment errors: {len(runtime.get('environmentErrors',[]))} (Unity SearchDatabase indexing and the missing OVRPlugin on a desktop Editor, not app code).

## Per-frame cost (server editor, real-time playback, spins on the worker)

Main-thread milliseconds on one shared x86 server core under Mono JIT. The worker runs in parallel, and the load average is recorded in frame-cost.json. The GPU columns are proxies, measured in MR (the headset default): shaded fragments per pixel, volume samples per frame at the Quest 3 default eye resolution (both eyes), and the triangles and draws submitted per eye.

| cue | main thread ms | grid + close-up ms | scanner ms | plots ms | worker ms (cube / hand) | alloc B/frame | fragments/px | raymarch samples | triangles / draws per eye |
|---|---|---|---|---|---|---|---|---|---|
{rows}

Quest 3 CPU and GPU milliseconds could not be measured without the headset. The strip's "ms" icon shows them live on the device, and frame-timings.csv records 30 s means in the app's data folder.

- Contextual boundary permission is in the APK; whole-app BOUNDARYLESS_APP is absent. Passthrough colour mapping is disabled in code.
- No Store submission and no claim of diagnostic capability.

## Artifact

APK SHA-256: {apkcheck['sha256']}
APK bytes: {apkcheck['bytes']}

Details: runtime-report-{V}.json, sim-report-{V}.json, apk-report-{V}.json, frame-cost-{V}.json, narration-audio-{V}.json.
"""
(build/'VALIDATION.md').write_text(report)
narr={v:json.loads((root/f'Assets/Resources/Lesson/Lesson-{v}.json').read_text()) for v in ['am_michael','bm_george']}
(root/'validation/narration-audio.json').write_text(json.dumps(dict(version=V,voices={v:[dict(id=c['id'],text=c['text'],refs=c['refs'],seconds=c['dur'],clip=c['clip']) for s in d['steps'] for c in s['cues']] for v,d in narr.items()}),indent=1))
for name in ['runtime-report.json','sim-report.json','apk-report.json','build-android.json','build-windows.json','narration-audio.json','render-manifest.json','frame-cost.json']:
 shutil.copy2(root/'validation'/name,build/name)
 file=Path(name);shutil.copy2(build/name,build/(file.stem+f'-{V}'+file.suffix))
shutil.copy2(build/'VALIDATION.md',build/f'VALIDATION-{V}.md')
shutil.copy2(build/'README.md',build/f'README-{V}.md')
hashes={}
for name in [apk.name,zipname]:
 digest=hashlib.sha256((build/name).read_bytes()).hexdigest();hashes[name]=digest;(build/(name+'.sha256')).write_text(digest+'  '+name+'\n')
(root/'validation/publication.json').write_text(json.dumps(dict(version=V,artifacts=hashes,checks='source hashes, x64 PE, required Windows files, runtime harness, simulation core and APK report passed',physicalQuestTested=False),indent=2))
print(json.dumps(dict(directory=str(build),artifacts=[dict(name=n,bytes=(build/n).stat().st_size,sha256=h) for n,h in hashes.items()]),indent=2))
