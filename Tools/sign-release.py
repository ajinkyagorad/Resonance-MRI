#!/usr/bin/env python3
"""Sign a preview or the App-ID-configured Store APK without exposing credentials."""
from pathlib import Path
import os,secrets,subprocess,json,sys
root=Path(__file__).resolve().parents[1]
store_mode='--store' in sys.argv
sdk=Path('/home/triton/Unity/Hub/Editor/6000.5.5f1/Editor/Data/PlaybackEngines/AndroidPlayer')
private=Path.home()/'.config/resonance-quest'
private.mkdir(mode=0o700,parents=True,exist_ok=True);private.chmod(0o700)
key=private/'horizon-release.jks';password=private/'horizon-release.pass'
if not key.exists():
 if password.exists():raise SystemExit('Unpaired existing signing credential: inspect without overwriting.')
 password.write_text(secrets.token_urlsafe(42));password.chmod(0o600)
 env=os.environ.copy();env['RESONANCE_SIGNING_PASSWORD']=password.read_text()
 result=subprocess.run([str(sdk/'OpenJDK/bin/keytool'),'-genkeypair','-keystore',str(key),'-storepass:env','RESONANCE_SIGNING_PASSWORD','-keypass:env','RESONANCE_SIGNING_PASSWORD','-alias','resonance','-keyalg','RSA','-keysize','3072','-validity','10000','-dname','CN=Resonance MRI, O=Nebulytic','-storetype','JKS'],env=env,capture_output=True,text=True)
 if result.returncode:raise SystemExit('Release key generation failed; credentials were not printed.')
 key.chmod(0o600)
assert key.is_file() and password.is_file()
VERSION='0.9.5'
source=root/(f'Builds/Resonance-MRI-Horizon-{VERSION}-unsigned.apk' if store_mode else f'Builds/Resonance-MRI-Quest-{VERSION}.apk')
target=root/(f'Builds/Resonance-MRI-Horizon-{VERSION}.apk' if store_mode else f'.pipeline/horizon-release/Resonance-MRI-Release-Preview-{VERSION}.apk')
target.parent.mkdir(parents=True,exist_ok=True)
env=os.environ.copy();env['JAVA_HOME']=str(sdk/'OpenJDK');env['PATH']=str(sdk/'OpenJDK/bin')+':'+env['PATH']
env['RESONANCE_SIGNING_PASSWORD']=password.read_text()
signer=str(sdk/'SDK/build-tools/36.0.0/apksigner')
subprocess.run([signer,'sign','--ks',str(key),'--ks-key-alias','resonance','--ks-pass','env:RESONANCE_SIGNING_PASSWORD','--key-pass','env:RESONANCE_SIGNING_PASSWORD','--v1-signing-enabled','true','--v2-signing-enabled','true','--v3-signing-enabled','true','--out',str(target),str(source)],env=env,check=True,capture_output=True)
result=subprocess.check_output([signer,'verify','--min-sdk-version','24','--verbose','--print-certs',str(target)],env=env,text=True)
assert 'Verified using v2 scheme (APK Signature Scheme v2): true' in result
assert 'Android Debug' not in result
(root/'validation/release-signing.txt').write_text(result)
print(json.dumps(dict(artifact=str(target),storeConfigured=store_mode,certificate='release certificate; verified v2/v3',privateSigningDirectory=str(private))))
