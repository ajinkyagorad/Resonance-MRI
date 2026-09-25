from pathlib import Path
import sys, shutil
P=Path('/dev/shm/resonance-film-20260925')
sys.path.insert(0,str(P/'python-deps'))
import qrcode
CSS='''@font-face{font-family:Inter;src:url(assets/inter.woff2)}*{box-sizing:border-box}body{margin:0;background:#0b1520;color:#f5f9fc;font-family:Inter,sans-serif}#main{width:1920px;height:1080px;position:relative;overflow:hidden}#brand{position:absolute;left:112px;top:82px;display:flex;gap:25px;align-items:center;font-size:32px;letter-spacing:6px}#brand img{width:78px;height:78px;border-radius:15px}#words{position:absolute;left:112px;top:270px;z-index:2;max-width:1050px}.eyebrow{color:#91eab4;font-size:23px;letter-spacing:4px;margin:0 0 28px}h1{font-size:86px;letter-spacing:-4px;margin:0 0 30px}h2{font-size:43px;font-weight:600;line-height:1.36;margin:0 0 42px;color:#d4e3ef;max-width:740px}.note{font-size:26px;line-height:1.7;color:#adc3d3;margin:0}#scene{position:absolute;right:-290px;top:155px;width:1350px;height:760px;object-fit:contain;z-index:0}.address{font-size:29px;color:#70d5f1;margin:38px 0 12px}.fine{font-size:22px;color:#a4bac9}#qr{position:absolute;right:132px;top:294px;text-align:center;letter-spacing:3px;color:#b4c8d5;font-size:21px}#qr img{width:380px;height:380px}#credits{position:absolute;bottom:60px;left:112px;color:#9fb4c2;font-size:20px;line-height:1.8}#credits p{margin:0}'''
for kind,duration in [('intro',18),('outro',19)]:
 q=P/'hyperframes'/kind;(q/'assets').mkdir(parents=True,exist_ok=True)
 for src,name in [('/home/triton/projects/Nebulytic-store-164/img/logo.png','logo.png'),('/home/triton/flagship/film/assets/vendor/gsap.min.js','gsap.min.js'),('/home/triton/flagship/film/assets/fonts/inter-600.woff2','inter.woff2')]:shutil.copy2(src,q/'assets'/name)
 qrcode.make('https://nebulytic.com/apps/resonance-mri/',box_size=12,border=4).save(q/'assets/qr.png')
 brand='<div id="brand"><img src="assets/logo.png"><span>NEBULYTIC</span></div>'
 if kind=='intro':
  shutil.copy2('/home/triton/projects/ResonanceQuest-spatial/validation/store-094/scanner-cover-source.png',q/'assets/scanner.png')
  content=brand+'<div id="words"><p class="eyebrow">AN OPEN SCIENCE EXPERIENCE</p><h1>Resonance MRI</h1><h2>From magnetic field to a reconstructed image.</h2><p class="note">The complete 3D lesson</p><p class="note">Actual Unity simulation</p></div><img id="scene" src="assets/scanner.png">'
  anim='tl.from("#brand",{opacity:0,y:12,duration:.8},.2).from("#words",{opacity:0,y:20,duration:1},2).from("#scene",{opacity:0,x:24,duration:1.2},2.8);'
 else:
  content=brand+'<div id="words"><p class="eyebrow">CONTINUE IN THREE DIMENSIONS</p><h1>Explore it yourself.</h1><h2>Resonance MRI</h2><p class="note">Free &amp; open source</p><p class="note">Quest preview · VR &amp; passthrough · Windows</p><p class="address">nebulytic.com/apps/resonance-mri/</p><p class="fine">Downloads, source code &amp; Meta Horizon release updates</p></div><div id="qr"><img src="assets/qr.png"><p>SCAN TO EXPLORE</p></div><div id="credits"><p>Anatomy: modified BodyParts3D · © The Database Center for Life Science · CC BY 4.0</p><p>Unity simulation by Nebulytic · Narration: Kokoro-82M / am_michael · Full credits in the description</p></div>'
  anim='tl.from("#brand",{opacity:0,duration:.6},.1).from("#words",{opacity:0,y:16,duration:.8},.25).from("#qr",{opacity:0,y:10,duration:.8},.5).from("#credits",{opacity:0,duration:.8},1);'
 css=CSS.replace('right:-290px;top:155px;width:1350px;height:760px','right:40px;top:240px;width:930px;height:620px')
 html=f'<!doctype html><html lang="en"><head><meta charset="utf-8"><title>Resonance MRI</title><script src="assets/gsap.min.js"></script><style>{css}</style></head><body><div id="main" data-composition-id="main" data-width="1920" data-height="1080" data-fps="24" data-duration="{duration}">{content}</div><script>window.__timelines=window.__timelines||{{}};const tl=gsap.timeline({{paused:true}});{anim}window.__timelines.main=tl;</script></body></html>'
 (q/'index.html').write_text(html)
 print(q)
