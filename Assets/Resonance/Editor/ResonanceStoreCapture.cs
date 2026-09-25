using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;
using Nebulytic.Resonance;

public static class ResonanceStoreCapture
{
    static App app; static Camera cam; static string dir="validation/store-094";
    [Serializable] class Shot {public string file,cue,clip;public double tau,physicalTime,duration;public int frames;}
    [Serializable] class Manifest {public string version="0.9.4",method="Actual Unity runtime; native VR studio; deterministic lesson replay"; public bool physicalQuestCapture=false;public List<Shot> screenshots=new List<Shot>(),segments=new List<Shot>();}
    static Manifest manifest=new Manifest();
    static async Task Wait(Func<bool> condition,int seconds=1200)
    {var end=DateTime.UtcNow.AddSeconds(seconds);while(!condition()){if(DateTime.UtcNow>end)throw new TimeoutException();await Task.Delay(50);}}
    static async Task Frames(int n=2){for(int k=0;k<n;k++){int f=Time.frameCount;await Wait(()=>Time.frameCount>f);}}
    static (int,LessonCue) Cue(string id)
    {for(int i=0;i<app.Lesson.Steps.Count;i++)foreach(var c in app.Lesson.Steps[i].Cues)if(c.Data.id==id)return(i,c);throw new Exception(id);}
    static async Task At(string id,double fraction=.6)
    {var (s,c)=Cue(id);app.Seek(s,c.Start+c.Lead+fraction*c.Voice,false);await Frames(4);}
    static void View(Transform focus=null)
    {
        cam.fieldOfView=76;
        cam.transform.rotation=Quaternion.Euler(8,0,0);
    }
    static Texture2D Render(int w,int h,bool alpha=false)
    {
        var rt=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32){antiAliasing=4};
        var old=cam.targetTexture;cam.targetTexture=rt;
        app.Cube.Submit(cam);app.CloseUp.Submit(cam);app.Spatial.Submit(cam);cam.Render();
        RenderTexture.active=rt;var tex=new Texture2D(w,h,alpha?TextureFormat.RGBA32:TextureFormat.RGB24,false);
        tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();cam.targetTexture=old;RenderTexture.active=null;
        rt.Release();UnityEngine.Object.Destroy(rt);return tex;
    }
    static void Png(string path,int w=2560,int h=1440,bool alpha=false)
    {var t=Render(w,h,alpha);File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.Destroy(t);}
    public static async void Run()
    {
        Directory.CreateDirectory(dir+"/screenshots");Directory.CreateDirectory(dir+"/trailer");int code=1;
        try{
            EditorSceneManager.OpenScene(ResonanceBuild.ScenePath);EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.isPlaying=true;await Wait(()=>UnityEngine.Object.FindAnyObjectByType<App>()?.Ready==true);
            app=UnityEngine.Object.FindAnyObjectByType<App>();app.FixedStep=true;app.instantEmphasis=true;cam=app.HeadCamera;
            await Wait(()=>app.Sim.State!=null&&app.World.gameObject.activeSelf&&app.Lesson.Steps.Count>0);
            app.Controls.enabled=false;app.Pause();app.SetPlaybackRate(1);
            await Wait(()=>app.Sim.State.AcquisitionFinished);
            var ids=new[]{"1.1","2.18","2.30","4.9","9.7"};
            for(int i=0;i<ids.Length;i++){
                await At(ids[i]);View(i==1||i==2?app.Cube.Root:i==3?app.Plots.Signal:i==4?app.Plots.Data:null);await Frames();
                string file=$"screenshots/{i+1:00}-{ids[i].Replace('.','-')}.png";Png(dir+"/"+file);
                manifest.screenshots.Add(new Shot{file=file,cue=ids[i],tau=app.Tau,physicalTime=app.T});
            }
            // Cover composition source: isolate real scanner meshes, labels and controls omitted only for branding.
            await At("6.6");View();var saved=new Dictionary<GameObject,bool>();
            foreach(Transform t in app.World){saved[t.gameObject]=t.gameObject.activeSelf;t.gameObject.SetActive(t==app.Scanner.Root);}
            var text=app.Scanner.Root.GetComponentsInChildren<TextMeshPro>(true);var texts=text.Select(t=>t.enabled).ToArray();
            foreach(var t in text)t.enabled=false;
            var controls=app.Scanner.Root.Find("Scanner controls");bool controlsOn=controls&&controls.gameObject.activeSelf;if(controls)controls.gameObject.SetActive(false);
            bool room=app.Environment.Room.activeSelf;app.Environment.Room.SetActive(false);
            cam.backgroundColor=Color.clear;cam.orthographic=true;cam.orthographicSize=.38f;
            cam.transform.position=app.Scanner.Root.position+new Vector3(0,.06f,-1.8f);cam.transform.LookAt(app.Scanner.Root.position);
            // Isolate this actual mesh render from instanced plot overlays; light all conductors for cover artwork.
            int oldMask=cam.cullingMask;cam.cullingMask=1<<30;
            var layers=new Dictionary<GameObject,int>();
            foreach(var t in app.Scanner.Root.GetComponentsInChildren<Transform>(true)){layers[t.gameObject]=t.gameObject.layer;t.gameObject.layer=30;}
            var blocks=new Dictionary<Renderer,MaterialPropertyBlock>();
            foreach(var r in app.Scanner.Root.GetComponentsInChildren<Renderer>()) {
                if(!r.sharedMaterial || !r.sharedMaterial.HasProperty("_Body"))continue;
                var old=new MaterialPropertyBlock();r.GetPropertyBlock(old);blocks[r]=old;
                var lit=new MaterialPropertyBlock();r.GetPropertyBlock(lit);
                lit.SetFloat("_Opacity",1);lit.SetFloat("_Body",.85f);lit.SetFloat("_Flow",.12f);r.SetPropertyBlock(lit);
            }
            Png(dir+"/scanner-cover-source.png",2400,1600,true);
            foreach(var kv in blocks)kv.Key.SetPropertyBlock(kv.Value);
            foreach(var kv in layers)kv.Key.layer=kv.Value;
            cam.cullingMask=oldMask;
            for(int i=0;i<text.Length;i++)text[i].enabled=texts[i];if(controls)controls.gameObject.SetActive(controlsOn);
            foreach(var kv in saved)kv.Key.SetActive(kv.Value);app.Environment.Room.SetActive(room);
            cam.orthographic=false;cam.backgroundColor=Look.Hex(0x17212A);cam.transform.position=new Vector3(0,1.6f,0);View();
            // Real motion, one exact replay timestamp per output frame; source narration assembled later from matching cues.
            foreach(string id in new[]{"1.1","2.18","2.21","2.22","2.30","9.7"}){
                var (s,c)=Cue(id);int n=(int)Math.Ceiling((c.Voice+.35)*24);string slug=id.Replace('.','-');
                Directory.CreateDirectory(dir+"/trailer/"+slug);
                for(int frame=0;frame<n;frame++){
                    app.Seek(s,c.Start+c.Lead+Math.Min(frame/24.0,c.Voice+.25),false);
                    await Frames(1);View(id.StartsWith("2.")?app.Cube.Root:id=="9.7"?app.Plots.Data:null);
                    var t=Render(1920,1080);File.WriteAllBytes(dir+"/trailer/"+slug+$"/{frame:0000}.jpg",t.EncodeToJPG(95));UnityEngine.Object.Destroy(t);
                }
                manifest.segments.Add(new Shot{file="trailer/"+slug,cue=id,clip=c.Clip,duration=n/24.0,frames=n});
                File.WriteAllText(dir+"/manifest.json",JsonUtility.ToJson(manifest,true));Debug.Log("STORE_SEGMENT "+id);
            }
            File.WriteAllText(dir+"/manifest.json",JsonUtility.ToJson(manifest,true));code=0;
        }catch(Exception e){Debug.LogException(e);File.WriteAllText(dir+"/error.txt",e.ToString());}
        finally{EditorApplication.isPlaying=false;EditorApplication.Exit(code);}
    }
}
