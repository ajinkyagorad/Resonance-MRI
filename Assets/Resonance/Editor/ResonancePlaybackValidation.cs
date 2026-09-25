
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Nebulytic.Resonance;

public static class ResonancePlaybackValidation
{
    static App app; static List<string> checks=new List<string>();
    static string folder="validation/playback-094";
    static void Check(bool value,string name) { if(!value)throw new Exception(name);checks.Add(name);Debug.Log("PLAYBACK_PASS "+name); }
    static async Task Wait(Func<bool> condition,int seconds=600)
    { var end=DateTime.UtcNow.AddSeconds(seconds);while(!condition()){if(DateTime.UtcNow>end)throw new TimeoutException();await Task.Delay(50);} }
    static async Task Frames(int n=3)
    { for(int j=0;j<n;j++){int f=Time.frameCount;await Wait(()=>Time.frameCount>f);} }
    static Ray RayAt(float f) { var p=app.Strip.TimelinePoint(f);return new Ray(app.Eye,(p-app.Eye).normalized); }
    public static async void Run()
    {
        Directory.CreateDirectory(folder);int exit=1;
        try {
            EditorSceneManager.OpenScene(ResonanceBuild.ScenePath);
            EditorSettings.enterPlayModeOptionsEnabled=true;
            EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.isPlaying=true;
            await Wait(()=>UnityEngine.Object.FindAnyObjectByType<App>()?.Ready==true);
            app=UnityEngine.Object.FindAnyObjectByType<App>();app.FixedStep=true;app.instantEmphasis=true;
            await Wait(()=>app.Sim.State!=null && app.Lesson.Steps.Count>0 && app.World.gameObject.activeSelf);
            app.Pause();app.Controls.enabled=false;
            app.Seek(0,2,false);await Frames();Physics.SyncTransforms();
            var hit=app.Controls.Pick(RayAt(.6f),out _);
            Check(hit && hit.GetComponent<StripButton>()?.Action=="seek","Timeline is ray-selectable over strip collider");
            Check(app.BeginScrub(RayAt(.25f)),"Scrub begins");
            Check(!app.Playing && app.Scrubbing,"Scrub pauses");
            app.DragScrub(RayAt(.75f));await Frames();
            Check(Math.Abs(app.Tau-app.StepDuration*.75)<.002,"Drag seeks to requested time");
            Check(app.Cue.Index==app.Lesson.CueAt(app.Lesson.Steps[0],app.Tau),"Caption cue follows seek");
            app.EndScrub();Check(!app.Playing && !app.Scrubbing,"Paused seek remains paused");
            var startCue=app.Cue.Index;
            app.MoveSection(1);await Frames();
            Check(app.Cue.Index==startCue+1 && !app.Playing,"Next section preserves pause");
            app.MoveSection(-1);await Frames();
            Check(app.Cue.Index==startCue && !app.Playing,"Previous section returns without chapter restart");
            app.Seek(0,0,false);app.MoveSection(-1);await Frames();
            Check(app.StepIndex==0 && app.Tau==0,"Previous at beginning is clamped");
            app.Seek(app.Lesson.Steps.Count-1,1e6,false);app.MoveSection(1);await Frames();
            Check(app.StepIndex==app.Lesson.Steps.Count-1 && !app.Playing,"Next at end remains valid and paused");
            foreach(int rate in new[]{1,2,4}) {
                app.SetPlaybackRate(rate);app.Seek(0,1,true);int before=Time.frameCount;await Frames(6);int count=Time.frameCount-before;
                Check(Math.Abs(app.Tau-(1+count*rate/72.0))<rate/72.0+.001,"Clock advances at "+rate+"x");
                app.Pause();double tau=app.Tau;await Frames();
                Check(app.Tau==tau,"Pause freezes at "+rate+"x");
            }
            double keep=app.Tau;app.SetPlaybackRate(2);Check(app.Tau==keep,"Speed change preserves playhead");
            app.SetPlaybackRate(7);Check(app.PlaybackRate==2,"Unsupported speed ignored");
            app.SetPlaybackRate(1);app.Seek(0,1,true);await Frames();
            Check(app.BeginScrub(RayAt(.3f)),"Playing scrub begins");app.EndScrub();Check(app.Playing,"Release resumes previously playing narration");
            app.BeginScrub(RayAt(.4f));app.Controls.ReleaseAll();Check(!app.Playing&&!app.Scrubbing,"Focus/tracking cancellation stays paused");
            foreach(string voice in new[]{"am_michael","bm_george"}) {
                var data=Lesson.LoadData(voice);
                var cue=data.steps[0].cues.First(c=>!string.IsNullOrEmpty(c.clip));
                var original=Resources.Load<AudioClip>(cue.clip);
                foreach(int rate in new[]{2,4}) {
                    var fast=Resources.Load<AudioClip>("NarrationSpeed"+rate+"/"+cue.clip.Substring("Narration/".Length));
                    Check(fast && Math.Abs(fast.length*rate-original.length)<.25,"Pitch-preserved "+rate+"x audio loads for "+voice);
                }
            }
            app.Seek(0,0,false);app.Strip.ToggleCredits();await Frames();Physics.SyncTransforms();
            var button=app.Strip.Root.GetComponentsInChildren<StripButton>().Single(b=>b.Action=="repository");
            var ray=new Ray(app.Eye,(button.transform.position-app.Eye).normalized);
            Check(app.Controls.Pick(ray,out _)?.GetComponent<StripButton>()?.Action=="repository","GitHub source button is ray-selectable");
            Check(new Uri(App.RepositoryUrl).Host=="github.com","Source link points to GitHub");
            app.Strip.ToggleCredits();Check(!button.gameObject.activeInHierarchy,"Source button hidden with info card");
            for(int si=0;si<app.Lesson.Steps.Count;si++) {
                var c=app.Lesson.Steps[si].Cues.FirstOrDefault(c=>c.Data.demo=="single");
                if(c==null)continue;
                app.Seek(si,c.Start+c.Lead+c.Voice*.5,false);await Frames(5);
                Check(app.Cube.DrawnNeedles==288,"Single-spin lesson retains 288 sample moments");
                var mesh=app.Cube.Root.Find("Visible moments").GetComponent<MeshFilter>().sharedMesh;
                Check(mesh.colors.Any(c=>c.a<.3f)&&mesh.colors.Any(c=>c.a>.9f),"Selected moment distinguished from translucent context");
                var halo=app.Cube.Root.Find("Selected proton halo");
                Check(Vector3.Distance(halo.position,app.Cube.CellWorld(app.Cube.Selected))<.001f,"Highlight anchored to selected lattice position");
                app.Action("phase");await Frames();
                var phase=app.Cube.Root.Find("Transverse phase projections").GetComponent<MeshFilter>().sharedMesh;
                Check(phase.vertexCount>0,"Phase projection appears beside full moments");
                Check(phase.colors.All(c=>c.a<1),"Phase arrows retain transparency");
                Capture("single-spin-and-phase.png");break;
            }
            app.Seek(0,1,false);await Frames(5);Capture("playback-controls.png");
            app.Strip.ToggleCredits();await Frames();Capture("source-link.png");
            File.WriteAllText(folder+"/report.json",JsonUtility.ToJson(new Report{version=ResonanceBuild.Version,passed=true,checks=checks.ToArray()},true));exit=0;
        } catch(Exception e) {Debug.LogException(e);File.WriteAllText(folder+"/report.json",JsonUtility.ToJson(new Report{version=ResonanceBuild.Version,passed=false,checks=checks.ToArray(),error=e.ToString()},true));}
        finally {EditorApplication.isPlaying=false;EditorApplication.Exit(exit);}
    }
    static void Capture(string file)
    {
        var c=app.HeadCamera;var rt=new RenderTexture(1920,1080,24);var old=c.targetTexture;c.targetTexture=rt;
        c.Render();RenderTexture.active=rt;var tex=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        tex.ReadPixels(new Rect(0,0,1920,1080),0,0);tex.Apply();File.WriteAllBytes(folder+"/"+file,tex.EncodeToPNG());
        c.targetTexture=old;RenderTexture.active=null;UnityEngine.Object.Destroy(tex);rt.Release();UnityEngine.Object.Destroy(rt);
    }
    [Serializable]class Report {public string version,error;public bool passed;public string[] checks;}
}
