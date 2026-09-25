"""APK identity, manifest policy, signing, packaging and packed-asset checks for the private Quest update (0.8.4)."""
from pathlib import Path
import subprocess, json, zipfile, hashlib, os, re
VERSION, CODE = '0.9.3', 16
r = Path(__file__).resolve().parents[1]; apk = r / f'Builds/Resonance-MRI-Quest-{VERSION}.apk'
sdk = Path('/home/triton/Unity/Hub/Editor/6000.5.5f1/Editor/Data/PlaybackEngines/AndroidPlayer')
aapt = sdk / 'SDK/build-tools/36.0.0/aapt2'; signer = sdk / 'SDK/build-tools/36.0.0/apksigner'
badging = subprocess.check_output([str(aapt), 'dump', 'badging', str(apk)], text=True)
manifest = subprocess.check_output([str(aapt), 'dump', 'xmltree', str(apk), '--file', 'AndroidManifest.xml'], text=True)
env = os.environ.copy(); env['JAVA_HOME'] = str(sdk / 'OpenJDK'); env['PATH'] = str(sdk / 'OpenJDK/bin') + ':' + env['PATH']
signed = subprocess.run([str(signer), 'verify', '--verbose', '--print-certs', str(apk)], capture_output=True, text=True, env=env)
checks = {}
def check(name, ok): checks[name] = bool(ok)
check('package_identity', "package: name='com.nebulytic.resonance'" in badging)
check(f'version_code_{CODE}', f"versionCode='{CODE}'" in badging)
check('version_' + VERSION.replace('.', ''), f"versionName='{VERSION}'" in badging)
check('minimum_sdk_32', "minSdkVersion:'32'" in badging)
check('target_sdk_34', "targetSdkVersion:'34'" in badging)
check('arm64_only', "native-code: 'arm64-v8a'" in badging and 'armeabi-v7a' not in badging)
check('immersive_headtracking', 'android.hardware.vr.headtracking' in manifest)
check('passthrough_feature', 'com.oculus.feature.PASSTHROUGH' in manifest)
check('contextual_boundary_permission', 'com.oculus.permission.BOUNDARY_VISIBILITY' in manifest)
check('no_whole_app_boundaryless', 'com.oculus.feature.BOUNDARYLESS_APP' not in manifest)
check('hand_tracking', 'oculus.permission.HAND_TRACKING' in manifest)
check('valid_signature', signed.returncode == 0)
check('release_not_debuggable', not any('debuggable' in l and '0xffffffff' in l for l in manifest.splitlines()))
with zipfile.ZipFile(apk) as z:
    names = z.namelist()
    check('il2cpp_runtime', 'lib/arm64-v8a/libil2cpp.so' in names)
    check('meta_runtime', any('OVRPlugin' in n for n in names))
    check('openxr_runtime', any('openxr' in n.lower() and n.endswith('.so') for n in names))
    check('no_unexpected_architectures', all(n.startswith('lib/arm64-v8a/') for n in names if n.startswith('lib/') and n.endswith('.so')))
    for notice in ['BodyParts3D.txt', 'Receiver-reference.txt', 'Asset-publication.txt', 'Scanner-model.txt', 'Kokoro-Narration.txt', 'DejaVu-Sans.txt']:
        check('distributed_notice_' + notice, any(n.endswith('/' + notice) or n.endswith(notice) for n in names))
    check('no_slice_lab_notice', not any('Slice-Lab' in n for n in names))
    # 0.8.4: no server address and no AgentBridge access token in the packed data. The Meta XR SDK's DevAgentBuildProcessor
    # injects both into Resources/DevAgentSettings before every build; ResonanceBuildSanitizer clears them. The tokens are
    # read from the Unity editor prefs (never printed).
    import base64
    addrs = {'85.10.235.242', '100.127.10.14'} | set(subprocess.run(['hostname', '-I'], capture_output=True, text=True).stdout.split())
    tokens = set()
    for prefs in [Path.home() / '.local/share/unity3d/prefs', Path.home() / '.config/unity3d/prefs']:
        if prefs.exists():
            for value in re.findall(r'<pref name="[^"]*AccessToken[^"]*" type="string">([^<]*)</pref>', prefs.read_text(errors='replace')):
                try: value = base64.b64decode(value).decode()
                except Exception: pass
                if len(value) >= 16: tokens.add(value)
    data = b''.join(z.read(n) for n in names if n.startswith('assets/bin/Data/') and not n.endswith('/'))
    check('no_server_address_packed', not any(a and a.encode() in data for a in addrs))
    check(f'no_agent_bridge_token_packed_({len(tokens)}_known)', not any(t.encode() in data for t in tokens))
packed = (r / 'validation/packed-android.txt').read_text()
for name in ['FieldTables.bytes', 'HandLabels.bytes', 'AnatomyManifest.json', 'AtlasSansSDF.asset', 'MetaQuestTouchPlus',
             'Conductor.shader', 'Volume.shader', 'Needle.shader', 'Solid.shader', 'Glass.shader', 'Flat.shader', 'Glow.shader', 'Line3D.shader',
             'Image.shader', 'Plate.shader', 'Relief.shader', 'Backdrop.shader',
             'Lesson-am_michael.json', 'Lesson-bm_george.json']:
    check('packed_' + name, name in packed)
check('no_old_tissue_volume', 'HandTissue.bytes' not in packed)
for voice in ['am_michael', 'bm_george']:
    lesson = json.loads((r / f'Assets/Resources/Lesson/Lesson-{voice}.json').read_text())
    clips = [c[k] for s in lesson['steps'] for c in s['cues'] for k in ('clip', 'clipGeneric') if c.get(k)]
    check(f'narration_{voice}_every_cue_has_audio', all(c['clip'] for s in lesson['steps'] for c in s['cues']))
    check(f'narration_{voice}_all_{len(clips)}_clips_packed', all(f'Resources/{c}.wav' in packed for c in clips))
    check(f'narration_{voice}_no_unused_clips', set(re.findall(rf'Resources/Narration/{voice}/\w+\.wav', packed)) <= {f'Resources/{c}.wav' for c in clips})
check('hand_model_decimated', sum(s['triangles'] for s in json.loads((r / 'Assets/Resources/Anatomy/AnatomyManifest.json').read_text())['structures'] if s['file'].startswith('Bone_') or s['file'] == 'Skin.obj') < 30000)
report = dict(passed=all(checks.values()), physicalQuestTested=False, bytes=apk.stat().st_size, sha256=hashlib.sha256(apk.read_bytes()).hexdigest(), checks=checks)
(r / 'validation/apk-manifest.txt').write_text(manifest)
(r / 'validation/apk-badging.txt').write_text(badging)
(r / 'validation/apk-signature.txt').write_text(signed.stdout + signed.stderr)
(r / 'validation/apk-report.json').write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))
if not report['passed']: raise SystemExit(1)
