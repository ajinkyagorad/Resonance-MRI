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

/// <summary>Editor-only reference export from the real runtime meshes. No generated imagery.
/// Reframes existing elements and uses white background/dark labels for paper readability.
/// Different captures deliberately use different lesson times; manifest records each.</summary>
public static class ResonanceElementReference
{
    static App app;
    static Camera cam;
    static string folder;
    static readonly List<string> manifest = new List<string>();
    static async Task Wait(Func<bool> test, int seconds = 180)
    {
        var end = DateTime.UtcNow.AddSeconds(seconds);
        while(!test()) { if(DateTime.UtcNow>end) throw new TimeoutException(); await Task.Delay(80); }
    }
    static async Task Frames(int n)
    {
        for(int k=0;k<n;k++) { int f=Time.frameCount; await Wait(()=>Time.frameCount>f); }
    }
    static async Task At(string id, double f=0.8)
    {
        for(int i=0;i<app.Lesson.Steps.Count;i++)
        foreach(var c in app.Lesson.Steps[i].Cues)
        if(c.Data.id==id) {
            app.Seek(i,c.Start+c.Lead+f*Math.Max(.1,c.Len-c.Lead-Lesson.CueGap),false);
            await Frames(3); return;
        }
        throw new Exception("Unknown cue "+id);
    }
    public static async void Run()
    {
        folder=Path.GetFullPath("validation/element-reference-093");
        Directory.CreateDirectory(folder);
        int exit=1;
        try {
            EditorSceneManager.OpenScene(ResonanceBuild.ScenePath);
            EditorSettings.enterPlayModeOptionsEnabled=true;
            EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            var ready=new TaskCompletionSource<bool>();
            void Changed(PlayModeStateChange s) { if(s==PlayModeStateChange.EnteredPlayMode) { EditorApplication.playModeStateChanged-=Changed; ready.SetResult(true); } }
            EditorApplication.playModeStateChanged+=Changed; EditorApplication.isPlaying=true; await ready.Task;
            await Wait(()=>UnityEngine.Object.FindAnyObjectByType<App>()?.Ready==true,600);
            app=UnityEngine.Object.FindAnyObjectByType<App>();
            app.FixedStep=true; app.instantEmphasis=true;
            await Wait(()=>app.Sim.State!=null && app.Lesson.Steps.Count>0 && app.World.gameObject.activeSelf,600);
            app.Pause();
            cam=new GameObject("Reference export camera").AddComponent<Camera>();
            cam.enabled=false; cam.orthographic=true; cam.clearFlags=CameraClearFlags.SolidColor;
            cam.backgroundColor=Color.white; cam.cullingMask=1<<30;
            cam.allowHDR=false; cam.allowMSAA=true; cam.nearClipPlane=.01f; cam.farClipPlane=30;
            Debug.Log("ELEMENT_EXPORT_READY");
            await At("2.30",.9);
            Capture("02-moment-grid",app.Cube.Root);
            await At("2.21",.8);
            Capture("09-t1-recovery",app.Cube.Root);
            await At("2.22",.7);
            Capture("10-t2-coherence",app.Cube.Root);
            await Wait(()=>app.Sim.State.AcquisitionFinished || app.Sim.State.Error!=null,1500);
            if(app.Sim.State.Error!=null)throw new Exception(app.Sim.State.Error);
            await At("6.6",.8);
            app.Attention.SetRefs(null); app.Attention.Tick(0,true);
            app.Scanner.Tick(0,false,app.DisplayCarrier);
            Capture("01-scanner",app.Scanner.Root);
            await At("3.8",.65);
            Capture("03-molecules-and-spin",app.CloseUp.Root);
            await At("4.9",.8);
            var ph=app.Scanner.Physics;
            var rx=new List<Transform>{ph.Find("ADC"),ph.Find("Mixer"),ph.Find("Filter")};
            rx.AddRange(ph.GetComponentsInChildren<TextMeshPro>().Where(t=>t.text.StartsWith("ADC\n")||t.text.StartsWith("mixer\n")||t.text.StartsWith("filter\n")).Select(t=>t.transform));
            foreach(var t in rx.Where(t=>t!=null))t.gameObject.SetActive(true);
            Capture("04-receiver",ph,rx.Where(t=>t!=null).ToArray());
            await At("9.4",.6);
            Capture("05-pulse-sequence",app.Plots.Sequence);
            await At("7.6",.9);
            Capture("06-rf-and-iq",app.Plots.Signal);
            await At("9.7",1);
            Capture("07-kspace-and-image",app.Plots.Data);
            await At("10.6",1);
            Capture("08-reconstructed-volume",app.Plots.Data,new[]{app.Plots.Data.Find("3-D image")});
            var controls=new List<Transform>();
            foreach(Transform t in app.Strip.Root)if(t.name!="Title"&&t.name!="Clock"&&t.name!="Subtitles")controls.Add(t);
            Capture("11-controls",app.Strip.Root,controls.ToArray());
            File.WriteAllText(Path.Combine(folder,"manifest.txt"),"Unity "+Application.unityVersion+" / app "+ResonanceBuild.Version+"\nActual runtime geometry and shaders; white camera background; dark export labels; orthographic reframing.\nCaptures use different recorded lesson states, not one simultaneous scanner instant.\n"+string.Join("\n",manifest));
            exit=0;
        } catch(Exception e) { Debug.LogException(e); }
        finally { Debug.Log("ELEMENT_EXPORT_EXIT "+exit); EditorApplication.isPlaying=false; EditorApplication.Exit(exit); }
    }
    static void Capture(string name,Transform orientation,Transform[] roots=null)
    {
        roots=roots??new[]{orientation};
        var transforms=roots.Where(r=>r!=null).SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Distinct().ToArray();
        var layers=transforms.Select(t=>t.gameObject.layer).ToArray();
        var labels=transforms.Select(t=>t.GetComponent<TextMeshPro>()).Where(t=>t!=null).ToArray();
        var colors=labels.Select(t=>t.color).ToArray();
        var rots=labels.Select(t=>t.transform.rotation).ToArray();
        var materials=labels.Select(t=>t.fontSharedMaterial).ToArray();
        var exportFont=new Material(Labels.Font.material);
        exportFont.SetFloat("_OutlineWidth",0);
        exportFont.DisableKeyword("UNDERLAY_ON");
        exportFont.DisableKeyword("OUTLINE_ON");
        var renderers=transforms.Select(t=>t.GetComponent<Renderer>()).Where(r=>r!=null && r.enabled && r.gameObject.activeInHierarchy).ToArray();
        var recolored=new Dictionary<Renderer,Material>();
        if(renderers.Length==0)throw new Exception("No visible renderers "+name);
        try {
            for(int i=0;i<transforms.Length;i++)transforms[i].gameObject.layer=30;
            // Keep actual world orientation; use the same viewing direction as the app camera at this element.
            var direction=(orientation.position-app.HeadCamera.transform.position).normalized;
            cam.transform.rotation=Quaternion.LookRotation(direction,Vector3.up);
            for(int i=0;i<labels.Length;i++) {
                labels[i].fontSharedMaterial=exportFont;
                Color c=colors[i];
                if(orientation==app.Strip.Root)c=Color.white;
                else if(c.r>.65f&&c.g>.65f&&c.b>.65f)c=new Color(.09f,.11f,.14f,1);
                else { c.r*=.72f;c.g*=.72f;c.b*=.72f;c.a=1; }
                labels[i].color=c;
                labels[i].transform.rotation=cam.transform.rotation;
                labels[i].ForceMeshUpdate();
            }
            if(orientation==app.Strip.Root)foreach(var r in renderers)
                if(r.name.StartsWith("Icon ")||r.name=="Bar"||r.name=="Fill") {
                    recolored[r]=r.sharedMaterial;
                    r.sharedMaterial=new Material(r.sharedMaterial);
                    r.sharedMaterial.SetColor("_Color",new Color(.08f,.12f,.20f,1));
                }
            var inv=Quaternion.Inverse(cam.transform.rotation);
            Vector3 lo=Vector3.one*float.MaxValue,hi=Vector3.one*float.MinValue;
            foreach(var r in renderers) {
                var mf=r.GetComponent<MeshFilter>();
                if(mf!=null && (mf.sharedMesh==null||mf.sharedMesh.vertexCount==0))continue;
                Bounds b=r.bounds;
                for(int k=0;k<8;k++) {
                    var v=b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1));
                    v=inv*v;lo=Vector3.Min(lo,v);hi=Vector3.Max(hi,v);
                }
            }
            const int w=1800,h=1300;
            float aspect=(float)w/h;
            cam.orthographicSize=Mathf.Max((hi.y-lo.y)*.5f,(hi.x-lo.x)*.5f/aspect)*1.10f;
            var center=(lo+hi)*.5f;
            cam.transform.position=cam.transform.rotation*(center-Vector3.forward*(hi.z-lo.z+2));
            var rt=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32){antiAliasing=4};
            var prior=RenderTexture.active; cam.targetTexture=rt;cam.aspect=aspect;
            if(roots.Contains(app.Scanner.Root))app.Scanner.Submit(cam);
            if(roots.Contains(app.CloseUp.Root))app.CloseUp.Submit(cam);
            cam.Render();RenderTexture.active=rt;
            var tex=new Texture2D(w,h,TextureFormat.RGB24,false);
            tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();
            File.WriteAllBytes(Path.Combine(folder,name+".png"),tex.EncodeToPNG());
            manifest.Add(name+" | cue="+app.Cue?.Data.id+" | t="+app.T.ToString("R")+" | tau="+app.Tau.ToString("R")+" | renderers="+renderers.Length);
            RenderTexture.active=prior;cam.targetTexture=null;
            UnityEngine.Object.Destroy(tex);rt.Release();UnityEngine.Object.Destroy(rt);
            Debug.Log("ELEMENT_CAPTURE "+name);
        } finally {
            foreach(var entry in recolored){var temp=entry.Key.sharedMaterial;entry.Key.sharedMaterial=entry.Value;UnityEngine.Object.Destroy(temp);}
            for(int i=0;i<transforms.Length;i++)transforms[i].gameObject.layer=layers[i];
            for(int i=0;i<labels.Length;i++){labels[i].fontSharedMaterial=materials[i];labels[i].color=colors[i];labels[i].transform.rotation=rots[i];labels[i].ForceMeshUpdate();}
            UnityEngine.Object.Destroy(exportFont);
        }
    }
}
