#!/usr/bin/env python3
"""Pitch-preserving narration at 2x/4x; original voice/text/timeline durations retained.
Requires ffmpeg (atempo) and ffprobe. Reproducible from checked-in original WAVs.
"""
from pathlib import Path
import concurrent.futures, subprocess, json
root=Path(__file__).resolve().parents[1]
jobs=[(f,rate) for f in sorted((root/'Assets/Resources/Narration').glob('*/*.wav')) for rate in (2,4)]
def render(job):
    src,rate=job
    dst=root/f'Assets/Resources/NarrationSpeed{rate}'/src.parent.name/(src.stem+'.ogg')
    dst.parent.mkdir(parents=True,exist_ok=True)
    if not dst.exists():
        filt='atempo=2' if rate==2 else 'atempo=2,atempo=2'
        subprocess.run(['ffmpeg','-hide_banner','-loglevel','error','-threads','1','-i',str(src),'-af',filt,'-c:a','libvorbis','-q:a','4','-y',str(dst)],check=True)
    return str(dst.relative_to(root))
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool: outputs=list(pool.map(render,jobs))
report=root/'validation/playback-audio.json'
report.parent.mkdir(exist_ok=True)
report.write_text(json.dumps({'rates':[1,2,4],'originalClips':len(jobs)//2,'derivedClips':len(outputs),'method':'ffmpeg atempo; pitch preserved','files':outputs},indent=2))
print(f'Prepared {len(outputs)} narration variants')
