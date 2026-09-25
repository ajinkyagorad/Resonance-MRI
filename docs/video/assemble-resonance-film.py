"""Reproducible audio, captions, chapters and final film from deterministic Unity renders."""
from pathlib import Path
import json, subprocess, wave, array, math, textwrap, re, argparse
P=Path('/dev/shm/resonance-film-20260925')
def run(args): subprocess.run(args,check=True)
def timestamp(seconds,ass=False):
 n=round(seconds*(100 if ass else 1000));base=100 if ass else 1000
 h,n=divmod(n,3600*base);m,n=divmod(n,60*base);s,f=divmod(n,base)
 return f'{h:d}:{m:02}:{s:02}.{f:02}' if ass else f'{h:02}:{m:02}:{s:02},{f:03}'
def segments(text,start,duration):
 chunks=textwrap.wrap(text,width=100,break_long_words=False,break_on_hyphens=False)
 total=sum(len(x) for x in chunks);at=start
 for chunk in chunks:
  end=at+duration*len(chunk)/total
  yield at,end,'\n'.join(textwrap.wrap(chunk,width=52,break_long_words=False,break_on_hyphens=False))
  at=end
ASS='''[Script Info]
ScriptType: v4.00+
PlayResX: 1920
PlayResY: 1080
WrapStyle: 2
[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Caption,DejaVu Sans,36,&H00FFFFFF,&H00FFFFFF,&HBB20150B,&HBB20150B,0,0,0,0,100,100,0,0,3,9,0,2,150,150,42,1
Style: Chapter,DejaVu Sans,27,&H00D3C5AF,&H00FFFFFF,&H0020150B,&H0020150B,0,0,0,0,100,100,0,0,1,1,0,7,64,64,35,1
[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
'''
def pcm(path):return subprocess.check_output(['ffmpeg','-v','error','-i',str(path),'-f','s16le','-ar','24000','-ac','1','pipe:1'])
def prepare():
 manifest=json.loads((P/'manifest.json').read_text());extra=json.loads((P/'extra.json').read_text())
 (P/'edit').mkdir(exist_ok=True);allsubs=[];chapters=[(0,'Resonance MRI · Nebulytic')];offset=18.0
 transcript=['# Resonance MRI — full film transcript','', 'Actual Unity 0.9.5 simulation replay. Film-only narration corrections; teaching-model numerical settings.','',extra[0]['text'],'']
 allsubs.extend(segments(extra[0]['text'],1,extra[0]['duration']))
 for ch in manifest['chapters']:
  tag=f"{ch['index']+1:02}";duration=ch['duration'];buf=array.array('h',[0])*math.ceil(duration*24000)
  ass=ASS+f"Dialogue: 0,0:00:00.00,{timestamp(duration,True)},Chapter,,0,0,0,,{tag} / 10    {ch['title']}\n"
  chapters.append((offset,ch['title']));transcript+=['## '+ch['title'],'']
  for c in ch['cues']:
   samples=array.array('h');samples.frombytes(pcm(c['audio']));actual=len(samples)/24000
   if abs(actual-c['voice'])>.12:raise RuntimeError(f"Audio timing mismatch {c['id']}: {actual} vs {c['voice']}")
   start=c['start']+c['lead'];ix=round(start*24000)
   if ix+len(samples)>len(buf):raise RuntimeError('Audio exceeds chapter')
   buf[ix:ix+len(samples)]=samples
   for a,b,t in segments(c['text'],start,actual):
    safe=t.replace('\n',r'\N').replace('{','(').replace('}',')')
    ass+=f'Dialogue: 1,{timestamp(a,True)},{timestamp(b,True)},Caption,,0,0,0,,{safe}\n'
    allsubs.append((a+offset,b+offset,t))
   transcript+=[f"**{c['id']}** {c['text']}",'']
  with wave.open(str(P/f'edit/{tag}.wav'),'wb') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(24000);w.writeframes(buf.tobytes())
  (P/f'edit/{tag}.ass').write_text(ass)
  offset+=duration
 chapters.append((offset,'Explore Resonance MRI'));allsubs.extend(segments(extra[1]['text'],offset+1,extra[1]['duration']));transcript+=['## Explore it yourself','',extra[1]['text']]
 (P/'transcript.md').write_text('\n'.join(transcript))
 (P/'captions.en.srt').write_text('\n\n'.join(f'{i+1}\n{timestamp(a)} --> {timestamp(b)}\n{t}' for i,(a,b,t) in enumerate(allsubs))+'\n')
 (P/'chapters.txt').write_text('\n'.join(f'{int(t)//60:02}:{int(t)%60:02} {title}' for t,title in chapters)+'\n')
 (P/'timing.json').write_text(json.dumps({'duration':offset+19,'chapters':chapters,'caption_blocks':len(allsubs)},indent=2))
 master=array.array('h',[0])*round((offset+19)*24000)
 def place(audio,at):
  samples=array.array('h');samples.frombytes(pcm(audio));i=round(at*24000);master[i:i+len(samples)]=samples
 place(P/'audio/intro.wav',1);at=18
 for ch in manifest['chapters']:
  place(P/f"edit/{ch['index']+1:02}.wav",at);at+=ch['duration']
 place(P/'audio/outro.wav',at+1)
 with wave.open(str(P/'edit/master.wav'),'wb') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(24000);w.writeframes(master.tobytes())
 print('Prepared',len(manifest['chapters']),'chapters;',round(offset+19,2),'seconds;',len(allsubs),'caption blocks')
def mux():
 manifest=json.loads((P/'manifest.json').read_text());outputs=[]
 for kind,dur in [('intro',18),('outro',19)]:
  dest=P/f'edit/{kind}.mp4'
  if not dest.exists():run(['ffmpeg','-v','error','-y','-i',str(P/f'{kind}-silent.mp4'),'-i',str(P/f'audio/{kind}.wav'),'-filter_complex','[1:a]adelay=1000,apad[a]','-map','0:v','-map','[a]','-t',str(dur),'-c:v','libx264','-crf','18','-preset','veryfast','-threads','3','-pix_fmt','yuv420p','-c:a','aac','-ar','48000','-ac','2','-b:a','192k','-video_track_timescale','12288',str(dest)])
 outputs.append(P/'edit/intro.mp4')
 for ch in manifest['chapters']:
  tag=f"{ch['index']+1:02}";dest=P/f'edit/{tag}.mp4'
  if not dest.exists():run(['ffmpeg','-v','error','-y','-i',str(P/ch['file']),'-i',str(P/f'edit/{tag}.wav'),'-vf',f'scale=1728:972,pad=1920:1080:96:0:color=0x0b1520,ass={P}/edit/{tag}.ass','-t',str(ch['duration']),'-c:v','libx264','-preset','veryfast','-crf','18','-threads','3','-pix_fmt','yuv420p','-c:a','aac','-b:a','192k','-ar','48000','-ac','2','-video_track_timescale','12288',str(dest)])
  outputs.append(dest);print('Muxed',tag,flush=True)
 outputs.append(P/'edit/outro.mp4')
 (P/'edit/concat.txt').write_text('\n'.join(f"file '{p}'" for p in outputs))
 measure=subprocess.run(['ffmpeg','-hide_banner','-i',str(P/'edit/master.wav'),'-af','loudnorm=I=-16:TP=-1.5:LRA=9:print_format=json','-f','null','-'],capture_output=True,text=True,check=True).stderr
 loud=json.loads(measure[measure.rfind('{'):]);(P/'loudness-before.json').write_text(json.dumps(loud,indent=2))
 norm='loudnorm=I=-16:TP=-1.5:LRA=9:measured_I='+loud['input_i']+':measured_TP='+loud['input_tp']+':measured_LRA='+loud['input_lra']+':measured_thresh='+loud['input_thresh']+':offset='+loud['target_offset']+':linear=true:print_format=json'
 with (P/'master-encode.log').open('w') as log:
  subprocess.run(['ffmpeg','-hide_banner','-y','-f','concat','-safe','0','-i',str(P/'edit/concat.txt'),'-i',str(P/'edit/master.wav'),'-map','0:v','-map','1:a','-c:v','copy','-af',norm,'-c:a','aac','-b:a','192k','-ar','48000','-ac','2','-movflags','+faststart',str(P/'Resonance-MRI-full-1080p.mp4')],check=True,stdout=log,stderr=log)
 print('Final assembled',flush=True)
if __name__=='__main__':
 parser=argparse.ArgumentParser();parser.add_argument('command',choices=['prepare','mux']);args=parser.parse_args()
 prepare() if args.command=='prepare' else mux()
