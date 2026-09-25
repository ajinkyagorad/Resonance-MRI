using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Nebulytic.Resonance;

// Offline filming only. The simulation, geometry and app's shipped lesson remain unchanged.
public static class ResonanceFilmCapture
{
    static readonly string Dir=System.Environment.GetEnvironmentVariable("RESONANCE_FILM_DIR") ?? "/dev/shm/resonance-film-20260925";
    const int W=1920,H=1080,Fps=24;
    static App app; static Camera cam; static RenderTexture rt; static Texture2D pixels;
    static Transform focus; static Vector3 originalEye; static string previousCue;
    static T Private<T>(object owner,string name)=>(T)owner.GetType().GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(owner);
    [Serializable] class Override { public string id,text,audio; public float duration; }
    [Serializable] class Overrides { public Override[] items; }
    [Serializable] class CueRecord { public string id,text,audio,shot; public double start,voice,lead,length,physicalStart,physicalEnd; }
    [Serializable] class Chapter { public int index,frames; public string title,file; public double duration; public List<CueRecord> cues=new List<CueRecord>(); }
    [Serializable] class Manifest { public string source="Unity 0.9.5, deterministic simulation replay; film-only narration corrections"; public int width=W,height=H,fps=Fps; public List<Chapter> chapters=new List<Chapter>(); }
    static async Task Wait(Func<bool> test,int seconds=1200) { var end=DateTime.UtcNow.AddSeconds(seconds); while(!test()){if(DateTime.UtcNow>end)throw new TimeoutException();await Task.Delay(10);} }
    static async Task Frame(){int f=Time.frameCount;await Wait(()=>Time.frameCount>f);}
    static Transform Select(LessonCue c)
    {
        if(!string.IsNullOrEmpty(c.Data.demo))return app.Cube.Root;
        var r=c.Data.refs?.FirstOrDefault()??"scanner";
        if(r.StartsWith("closeup"))return app.CloseUp.Root;
        if(r.StartsWith("spins")||r.StartsWith("cube"))return app.Cube.Root;
        if(r.StartsWith("signal"))return app.Plots.Signal;
        if(r.StartsWith("sequence"))return app.Plots.Sequence;
        if(r.StartsWith("kspace")||r=="image"||r=="recon")return app.Plots.Data;
        return app.Scanner.Root;
    }
    static void CameraFor(LessonCue c,double fraction)
    {
        focus=Select(c); Vector3 centre=focus.position; float height=.78f; Vector3 direction=Vector3.forward;
        if(focus==app.Scanner.Root){centre+=Vector3.down*.025f;height=.76f;}
        else if(focus==app.Cube.Root){height=string.IsNullOrEmpty(c.Data.demo)?.66f:1.06f;centre+=Vector3.up*.07f;}
        else if(focus==app.CloseUp.Root){centre+=focus.up*.045f;height=.75f;direction=focus.forward;}
        else if(focus==app.Plots.Sequence){centre+=focus.up*-.025f;height=.40f;direction=focus.forward;}
        else if(focus==app.Plots.Signal){centre+=focus.up*.025f;height=.46f;direction=focus.forward;}
        else if(focus==app.Plots.Data){centre+=focus.up*-.005f;height=.47f;direction=focus.forward;}
        // Slow dolly within a shot; keep the scientific coordinate system fixed in space.
        float scale=1f-.025f*Mathf.SmoothStep(0,1,(float)fraction);
        cam.fieldOfView=38; cam.orthographic=false;
        float distance=height*scale/(2*Mathf.Tan(cam.fieldOfView*Mathf.Deg2Rad/2));
        cam.transform.position=centre-direction*distance;
        cam.transform.LookAt(centre,Vector3.up);
        cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Look.Hex(0x0B1520);
        if(previousCue!=c.Data.id && focus==app.Plots.Sequence){var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;typeof(PlotsView).GetField("seqSpan",flags).SetValue(app.Plots,.0012);typeof(PlotsView).GetField("seqT0",flags).SetValue(app.Plots,double.NaN);}
        previousCue=c.Data.id;
    }
    static void Render()
    {
        var old=new Dictionary<GameObject,bool>();
        foreach(Transform t in app.World){old[t.gameObject]=t.gameObject.activeSelf;t.gameObject.SetActive(t==focus);}
        var layers=new Dictionary<GameObject,int>();
        foreach(var t in focus.GetComponentsInChildren<Transform>(true)){layers[t.gameObject]=t.gameObject.layer;t.gameObject.layer=30;}
        int oldMask=cam.cullingMask;cam.cullingMask=1<<30;
        // Interactive handles are useful in XR but have no function in a filmed lesson.
        foreach(var t in focus.GetComponentsInChildren<Transform>(true))
            if(t.name=="Scanner controls"||t.name=="Cube controls") {old[t.gameObject]=t.gameObject.activeSelf;t.gameObject.SetActive(false);}
        app.Environment.Room.SetActive(false);
        cam.targetTexture=rt;
        if(focus==app.Scanner.Root)app.Scanner.Submit(cam);
        if(focus==app.Cube.Root){
            app.Cube.Submit(cam);
            foreach(var prefix in new[]{"field","rf"}){
                bool on=Private<bool>(app.Spatial,prefix=="field"?"gradientOn":"rfOn");
                if(!on)continue;
                var matrices=Private<Matrix4x4[]>(app.Spatial,prefix+"Matrices");
                var rp=new RenderParams(Private<Material>(app.Spatial,"fieldMaterial")){camera=cam,layer=30,matProps=Private<MaterialPropertyBlock>(app.Spatial,prefix+"Block"),worldBounds=new Bounds(app.Cube.Root.position,Vector3.one*2)};
                Graphics.RenderMeshInstanced(rp,Private<Mesh>(app.Spatial,prefix=="field"?"sampleMesh":"rfMesh"),0,matrices,matrices.Length);
            }
        }
        if(focus==app.CloseUp.Root)app.CloseUp.Submit(cam);
        cam.Render(); RenderTexture.active=rt;
        pixels.ReadPixels(new Rect(0,0,W,H),0,0);pixels.Apply(false);
        RenderTexture.active=null;cam.targetTexture=null;
        cam.cullingMask=oldMask;foreach(var p in layers)p.Key.layer=p.Value;
        foreach(var p in old)p.Key.SetActive(p.Value);
    }
    public static async void Run()
    {
        Directory.CreateDirectory(Dir+"/proofs");Directory.CreateDirectory(Dir+"/chapters");int code=1;
        try {
            EditorSceneManager.OpenScene(ResonanceBuild.ScenePath);
            EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.isPlaying=true;await Wait(()=>UnityEngine.Object.FindAnyObjectByType<App>()?.Ready==true);
            app=UnityEngine.Object.FindAnyObjectByType<App>();app.FixedStep=true;app.instantEmphasis=true;cam=app.HeadCamera;
            await Wait(()=>app.Sim.State!=null&&app.World.gameObject.activeSelf&&app.Lesson.Steps.Count>0);
            app.Controls.enabled=false;app.Pause();app.SetPlaybackRate(1);app.PhaseColour=true;await Wait(()=>app.Sim.State.AcquisitionFinished);
            originalEye=cam.transform.position;app.Environment.Room.SetActive(false);
            var edits=JsonUtility.FromJson<Overrides>(File.ReadAllText(Dir+"/overrides.json")).items.ToDictionary(x=>x.id);
            var data=Lesson.LoadData("am_michael");
            foreach(var step in data.steps)foreach(var c in step.cues)if(edits.TryGetValue(c.id,out var edit)){c.text=edit.text;c.dur=edit.duration;}
            app.Lesson.Build(data,"am_michael",app.Sim.State.Program,true,null);
            rt=new RenderTexture(W,H,24,RenderTextureFormat.ARGB32){antiAliasing=4};rt.Create();pixels=new Texture2D(W,H,TextureFormat.RGB24,false);
            var manifest=new Manifest();
            for(int s=0;s<app.Lesson.Steps.Count;s++) {
                var step=app.Lesson.Steps[s];int n=(int)Math.Ceiling(step.Len*Fps);
                var ch=new Chapter{index=s,title=step.Title,frames=n,duration=n/(double)Fps,file=$"chapters/{s+1:00}.mp4"};
                foreach(var c in step.Cues)ch.cues.Add(new CueRecord{id=c.Data.id,text=c.Text,audio=edits.TryGetValue(c.Data.id,out var edit)?edit.audio:Application.dataPath+"/Resources/"+c.Clip+".wav",shot=Select(c).name,start=c.Start,lead=c.Lead,length=c.Len,voice=c.Voice,physicalStart=c.T0,physicalEnd=c.T1});
                manifest.chapters.Add(ch);
            }
            File.WriteAllText(Dir+"/manifest.json",JsonUtility.ToJson(manifest,true));
            bool full=System.Environment.GetCommandLineArgs().Contains("-filmFull");
            if(!full){
                foreach(string id in new[]{"1.2","1.9","2.15","2.18","2.30","2.33","3.5","4.9","5.3","6.7","7.9","9.7","10.3"}) {
                    int s=app.Lesson.Steps.FindIndex(x=>x.Cues.Any(c=>c.Data.id==id));var c=app.Lesson.Steps[s].Cues.First(x=>x.Data.id==id);
                    app.Seek(s,c.Start+c.Lead+c.Voice*.55,false);await Frame();CameraFor(c,.55);await Frame();CameraFor(c,.55);Render();
                    File.WriteAllBytes(Dir+"/proofs/"+id+".png",pixels.EncodeToPNG());Debug.Log("FILM_PROOF "+id);
                }
            } else {
                // Advance the existing app explicitly once per output frame; avoid rendering the editor's Game view too.
                cam.enabled=false;app.enabled=false;
                var update=(Action)Delegate.CreateDelegate(typeof(Action),app,typeof(App).GetMethod("Update",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance));
                for(int s=0;s<app.Lesson.Steps.Count;s++) {
                    var ch=manifest.chapters[s];string path=Dir+"/"+ch.file;
                    if(File.Exists(path+".complete")){Debug.Log("FILM_SKIP "+ch.index);continue;}
                    var start=new System.Diagnostics.ProcessStartInfo("ffmpeg",$"-hide_banner -loglevel error -y -f rawvideo -pixel_format rgb24 -video_size {W}x{H} -framerate {Fps} -i pipe:0 -vf vflip -an -c:v libx264 -preset veryfast -crf 18 -threads 3 -pix_fmt yuv420p -movflags +faststart {path}"){UseShellExecute=false,RedirectStandardInput=true};
                    using(var ff=System.Diagnostics.Process.Start(start)) {
                        for(int f=0;f<ch.frames;f++) {
                            double tau=f/(double)Fps;int ci=app.Lesson.CueAt(app.Lesson.Steps[s],tau);var c=app.Lesson.Steps[s].Cues[Mathf.Clamp(ci,0,ch.cues.Count-1)];
                            await Frame();app.Seek(s,tau,false);CameraFor(c,(tau-c.Start)/c.Len);update();CameraFor(c,(tau-c.Start)/c.Len);Render();
                            var bytes=pixels.GetRawTextureData<byte>().ToArray();ff.StandardInput.BaseStream.Write(bytes,0,bytes.Length);
                            if(f%240==0)Debug.Log($"FILM_PROGRESS {s+1}/10 {f}/{ch.frames}");
                        }
                        ff.StandardInput.Close();ff.WaitForExit();if(ff.ExitCode!=0)throw new Exception("ffmpeg failed "+ff.ExitCode);
                    }
                    File.WriteAllText(path+".complete",ch.duration.ToString(System.Globalization.CultureInfo.InvariantCulture));Debug.Log("FILM_CHAPTER_DONE "+(s+1));
                }
            }
            code=0;
        }catch(Exception e){Debug.LogException(e);File.WriteAllText(Dir+"/capture-error.txt",e.ToString());}
        finally{if(rt)rt.Release();EditorApplication.isPlaying=false;EditorApplication.Exit(code);}
    }
}
