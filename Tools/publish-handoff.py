from pathlib import Path
import json,shutil,zipfile,hashlib
# Horizon preparation kit for 0.8.4 (new app). No private key, password or credential is read or packaged here.
V='0.8.4'
p=Path('/home/triton/projects/ResonanceQuest');out=Path('/home/triton/builds/resonance-quest')
runtime=json.loads((p/'validation/runtime-report.json').read_text());apk=json.loads((p/'validation/apk-report.json').read_text())
assert runtime['passed'] and runtime['version']==V and apk['passed'] and 'Verified using v3 scheme (APK Signature Scheme v3): true' in (p/'validation/release-signing.txt').read_text()
files={'Asset-publication-audit.md':f'ASSETS-{V}.md','Receiver-reference.md':f'RECEIVER-{V}.md','Horizon-release.md':f'HORIZON-{V}.md','Store-listing-draft.md':f'STORE-LISTING-DRAFT-{V}.md',f'Revision-{V}.md':f'FEEDBACK-{V}.md','Scanner-model.md':f'SCANNER-MODEL-{V}.md','Spec-0.8.0.md':f'SPEC-{V}.md'}
for source,destination in files.items():shutil.copy2(p/'Docs/AI'/source,out/destination)
shutil.copy2(p/'Builds/Anatomy-CC-BY-4.0.zip',out/'Anatomy-CC-BY-4.0.zip')
preview=p/f'.pipeline/horizon-release/Resonance-MRI-Release-Preview-{V}.apk'
kit=out/f'Resonance-MRI-Horizon-Preparation-{V}.zip'
with zipfile.ZipFile(kit,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 for source in files:z.write(p/'Docs/AI'/source,source)
 for name in ['build-horizon.sh','sign-release.py']:z.write(p/'Tools'/name,'Tools/'+name)
 z.write(preview,f'Release-Preview/{preview.name}',compress_type=zipfile.ZIP_STORED)
 z.write(p/'validation/release-signing.txt','Release-Preview/signature.txt')
 z.write(p/'validation/runtime-report.json','Validation/runtime-report.json')
 z.write(p/'validation/sim-report.json','Validation/sim-report.json')
 z.write(p/'validation/apk-report.json','Validation/private-update-apk-report.json')
 z.write(p/'Builds/Anatomy-CC-BY-4.0.zip','Anatomy-CC-BY-4.0.zip',compress_type=zipfile.ZIP_STORED)
 z.write(p/'validation/renders/cues/00-overview.png','Server-review-renders/00-overview.png')
 z.write(p/'validation/renders/key-views.png','Server-review-renders/key-views.png')
 z.writestr('README.txt','Horizon preparation for a NEW app. The release-signed preview is installable but is not the final Store binary. It has a different certificate from the private update APK. Read Horizon-release.md. The final Store build needs the real Meta App ID and device entitlement/VRC testing. No private key or credential is included. This package has not been uploaded to Meta.\n')
record=dict(version=V,newApp=True,appIdConfigured=False,storeSubmitted=False,physicalQuestTested=False,releasePreviewSigned=True,entitlementGateImplemented=True,finalStoreBuildBlockedBy='New Meta App ID required',assets=f'Source licences and notices reviewed; see ASSETS-{V}.md',artifacts={})
for file in [kit,out/'Anatomy-CC-BY-4.0.zip']:
 digest=hashlib.sha256(file.read_bytes()).hexdigest();record['artifacts'][file.name]=dict(bytes=file.stat().st_size,sha256=digest)
 (out/(file.name+'.sha256')).write_text(digest+'  '+file.name+'\n')
(out/f'HORIZON-PREPARATION-{V}.json').write_text(json.dumps(record,indent=2))
print(json.dumps(record,indent=2))
