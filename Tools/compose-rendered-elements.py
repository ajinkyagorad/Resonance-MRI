from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFont
import json, zipfile
base=Path("/home/triton/projects/ResonanceQuest-spatial/validation/element-reference-093")
canvas=Image.new("RGB",(4800,3200),"white")
d=ImageDraw.Draw(canvas)
font="/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
bold="/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
title=ImageFont.truetype(bold,64)
heading=ImageFont.truetype(bold,30)
note=ImageFont.truetype(font,28)
d.text((80,35),"Resonance MRI · rendered elements",font=title,fill="#171e29")
d.text((82,116),"App 0.9.3 · actual Unity meshes and shaders · white-background reference",font=note,fill="#56616d")
items=[
("01-scanner","Scanner, hand, coils and fields",(50,195,1600,1425)),
("02-moment-grid","Volumetric moment lattice",(1620,195,3180,1425)),
("03-molecules-and-spin","Water molecules and spin frames",(3200,195,4750,1425)),
("04-receiver","Receiver components",(50,1450,1200,2260)),
("05-pulse-sequence","Pulse sequence",(1220,1450,2390,2260)),
("06-rf-and-iq","RF and complex signal",(2410,1450,3580,2260)),
("07-kspace-and-image","Measured k-space and image",(3600,1450,4750,2260)),
("08-reconstructed-volume","Reconstructed volume",(50,2300,1020,3050)),
("09-t1-recovery","T1 example",(1040,2300,2100,3050)),
("10-t2-coherence","T2 example",(2120,2300,3180,3050)),
("11-controls","Playback and mode controls",(3200,2300,4750,3050)),
]
bounds={}
for stem,label,rect in items:
    im=Image.open(base/(stem+".png")).convert("RGB")
    diff=ImageChops.difference(im,Image.new("RGB",im.size,"white")).convert("L")
    bbox=diff.point(lambda v:255 if v>7 else 0).getbbox()
    if bbox is None: raise RuntimeError("Empty render "+stem)
    l,t,r,b=bbox
    bbox=(max(0,l-18),max(0,t-18),min(im.width,r+18),min(im.height,b+18))
    im=im.crop(bbox)
    x0,y0,x1,y1=rect
    d.text((x0+22,y0),label,font=heading,fill="#242d38")
    avw,avh=x1-x0-40,y1-y0-65
    ratio=min(avw/im.width,avh/im.height)
    im=im.resize((round(im.width*ratio),round(im.height*ratio)),Image.Resampling.LANCZOS)
    canvas.paste(im,(x0+(x1-x0-im.width)//2,y0+55+(avh-im.height)//2))
    if stem=="04-receiver":
        for fraction,text in ((0.09,"ADC"),(0.50,"Mixer"),(0.92,"Filter")):
            d.text((x0+int((x1-x0)*fraction),y1-80),text,font=note,fill="#56616d",anchor="mm")
    bounds[stem]=dict(source_crop=bbox,source=str(base/(stem+".png")))
d.text((80,3100),"Component captures use different lesson moments. Geometry retained; framing, background and label contrast adapted for this sheet.",font=note,fill="#56616d")
out=base/"MRI-rendered-elements-white-0.9.3.png"
canvas.save(out)
(base/"composition.json").write_text(json.dumps(bounds,indent=2))
with zipfile.ZipFile(base/"MRI-rendered-elements-source-0.9.3.zip","w",zipfile.ZIP_DEFLATED) as z:
    for p in sorted(base.glob("*.png")):
        if p.name!=out.name:z.write(p,p.name)
    for f in ("manifest.txt","composition.json","README.md"):z.write(base/f,f)
print(str(out))
print(out.stat().st_size)
