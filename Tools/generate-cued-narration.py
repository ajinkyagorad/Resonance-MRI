"""Per-cue narration for the guided lesson (SPEC 7).

Usage (server): python3 Tools/generate-cued-narration.py am_michael|bm_george [--no-audio] [--check]
Reads Docs/AI/narration-cues-source.json and Docs/AI/lesson-derived.json (numbers computed by Tools/simcore derive),
resolves placeholders, lints every sentence, synthesises one WAV per cue and variant with the existing local Kokoro
service (cached by voice + text + speed) and writes Assets/Resources/Lesson/Lesson-<voice>.json with durations and
clip paths. --no-audio writes estimated durations only; --check lints without writing.
"""
from pathlib import Path
import json, sys, hashlib, wave, re

root = Path(__file__).resolve().parents[1]
voice = sys.argv[1]
assert voice in ('am_michael', 'bm_george'), 'voice must be am_michael or bm_george'
no_audio = '--no-audio' in sys.argv; check_only = '--check' in sys.argv
prefetch = '--prefetch' in sys.argv  # fill the TTS cache only; do not touch Assets (safe while an editor runs)
source = json.loads((root / 'Docs/AI/narration-cues-source.json').read_text())
derived = json.loads((root / 'Docs/AI/lesson-derived.json').read_text())
speed = source.get('speed', 1.12)
# 0.8.4: colour words and "plot" are allowed (the colours are a code the narration must explain, and every plot is introduced
# by name); directions that change when the user moves things, and legend-style words, are not.
forbidden = {'graph', 'chart', 'legend', 'key', 'left', 'right', 'above', 'below',
             'textbook', 'book', 'imagine', 'cartoon', 'concept', 'representative'}
errors = []

def display(text): return re.sub(r'\[([^|\]]*)\|([^\]]*)\]', r'\1', text)   # subtitles
def spoken(text): return re.sub(r'\[([^|\]]*)\|([^\]]*)\]', r'\2', text)    # the voice

def resolve(text, cue_id):
    def sub(m):
        k = m.group(1)
        if k not in derived: errors.append(f'{cue_id}: unknown placeholder {k}'); return m.group(0)
        return derived[k]
    return re.sub(r'\{(\w+)\}', sub, text)

def lint(text, cue_id):
    if '[' in display(text) or '|' in display(text): errors.append(f'{cue_id}: malformed [display|spoken] group')
    text = spoken(text)
    words = text.split()
    if len(words) > 16: errors.append(f'{cue_id}: {len(words)} words (max 16): {text}')
    if not text.endswith('.'): errors.append(f'{cue_id}: must end with a full stop')
    for w in words:
        if re.sub(r"[^a-z]", "", w.lower()) in forbidden: errors.append(f'{cue_id}: forbidden word "{w}"')
    if '{' in text: errors.append(f'{cue_id}: unresolved placeholder')

cache = root / '.pipeline' / ('narration-v8-' + voice)
audio_dir = root / 'Assets/Resources/Narration' / voice
if not (no_audio or check_only):
    import requests
    cache.mkdir(parents=True, exist_ok=True)
    if not prefetch: audio_dir.mkdir(parents=True, exist_ok=True)

def synthesise(text, name):
    digest = hashlib.sha256((voice + text + str(speed)).encode()).hexdigest()[:12]
    cached = cache / f'{name}-{digest}.wav'
    if not cached.exists():
        # The same voice, text and speed under an earlier cue number (sentences renumbered): reuse that audio.
        same = sorted(cache.glob(f'*-{digest}.wav'))
        if same: cached.write_bytes(same[0].read_bytes())
    if not cached.exists():
        r = requests.post('http://127.0.0.1:8123/say', json={'text': text, 'voice': voice, 'speed': speed}, timeout=180); r.raise_for_status()
        a = requests.get('http://127.0.0.1:8123/wav/' + r.json()['id'], timeout=180); a.raise_for_status()
        cached.write_bytes(a.content)
    if not prefetch: (audio_dir / f'{name}.wav').write_bytes(cached.read_bytes())
    with wave.open(str(cached), 'rb') as w: return w.getnframes() / w.getframerate()

ids = set(); total = 0.0
out = dict(version=source['version'], speed=speed, steps=[])
for step in source['steps']:
    s = dict(step=step['step'], title=step['title'], strobe=step.get('strobe', ''), cues=[])
    for i, cue in enumerate(step['cues']):
        cid = cue['id']
        if cid in ids: errors.append(f'duplicate id {cid}')
        ids.add(cid)
        text = resolve(cue['text'], cid); lint(text, cid)
        generic = cue.get('generic', '')
        if generic: lint(generic, cid + 'g')
        say, sayGeneric = spoken(text), spoken(generic); text, generic = display(text), display(generic)
        fixed = {'rx_rate'}  # receiver bandwidth is not user-adjustable
        if any(k not in fixed for k in re.findall(r'\{(\w+)\}', cue['text'])) and not generic: errors.append(f'{cid}: parameter-dependent text needs a generic variant')
        c = dict(id=cid, demo=cue.get('demo',''), text=text, generic=generic, refs=cue.get('refs', []), sim=cue.get('sim', {}), dur=0.0, durGeneric=0.0, clip='', clipGeneric='')
        name = f"c{step['step']:02}_{i + 1:02}"
        if no_audio or check_only:
            c['dur'] = round(0.9 + 0.33 * len(say.split()), 3)
            if generic: c['durGeneric'] = round(0.9 + 0.33 * len(sayGeneric.split()), 3)
        else:
            c['dur'] = round(synthesise(say, name), 4); c['clip'] = f'Narration/{voice}/{name}'
            if generic: c['durGeneric'] = round(synthesise(sayGeneric, name + 'g'), 4); c['clipGeneric'] = f'Narration/{voice}/{name}g'
            print(json.dumps(dict(id=cid, seconds=c['dur'])), flush=True)
        total += c['dur']
        s['cues'].append(c)
    out['steps'].append(s)

if errors:
    print('\n'.join(errors)); raise SystemExit('narration lint failed')
if not (8 <= len(out['steps']) <= 12): raise SystemExit('lesson must have 8-12 steps')
if check_only:
    print(f'lint ok: {len(ids)} cues, estimated {total:.0f} s'); raise SystemExit(0)
if prefetch:
    print(f'prefetched {len(ids)} cues for {voice}: spoken {total:.0f} s'); raise SystemExit(0)
dest = root / 'Assets/Resources/Lesson'; dest.mkdir(parents=True, exist_ok=True)
(dest / f'Lesson-{voice}.json').write_text(json.dumps(out, indent=1, ensure_ascii=False))
print(f'wrote Lesson-{voice}.json: {len(ids)} cues, spoken {total:.0f} s' + (' (estimated)' if no_audio else ''))
