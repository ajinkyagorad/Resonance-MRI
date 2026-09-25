using System.Collections.Generic;
using UnityEngine;
using Nebulytic.Resonance.Sim;
namespace Nebulytic.Resonance
{
    // Compact object-local controls; they change presentation without seeking narration.
    public sealed class SpatialTools
    {
        readonly App app;
        readonly Transform tissueTools, scannerTools;
        readonly List<TMPro.TextMeshPro> captions = new List<TMPro.TextMeshPro>();
        readonly List<Renderer> buttons = new List<Renderer>();
        readonly string[] actions = { "sync", "tissue", "phase", "moments", "fields", "lesson", "fullscan", "receiver" };
        readonly Mesh sampleMesh = MeshKit.Sphere(0.004f,8,6), rfMesh = MeshKit.Needle(0.03f,0.08f,0.18f);
        readonly Material fieldMaterial = Mats.Needle(true);
        readonly MaterialPropertyBlock fieldBlock = new MaterialPropertyBlock(), rfBlock = new MaterialPropertyBlock();
        Matrix4x4[] fieldMatrices, rfMatrices; Vector4[] fieldColours, rfColours;
        int revision = -1; bool gradientOn, rfOn;
        public SpatialTools(App app)
        {
            this.app = app;
            tissueTools=new GameObject("Tissue controls").transform; tissueTools.SetParent(app.Cube.Root,false); tissueTools.localPosition=new Vector3(0,-CubeView.DisplaySize.y/2-0.045f,0);
            scannerTools=new GameObject("Scanner controls").transform; scannerTools.SetParent(app.Scanner.Root,false); scannerTools.localPosition=new Vector3(0,-0.35f,0);
            string[] names = { "Sync", "Tissue", "Phase", "M / μ", "Field", "Lesson", "Scan hand", "Receiver" };
            for (int i=0;i<actions.Length;i++)
            {
                Transform parent=i<5 ? tissueTools : scannerTools;
                Vector3 at=i<5 ? new Vector3((i-2)*0.09f,0,0) : new Vector3((i-6)*0.15f,0,0);
                var go=Mats.Object("Spatial control "+actions[i],parent,MeshKit.Sphere(0.009f,16,10),Mats.Glass(new Color(0.3f,0.85f,1f,0.9f)),at);
                var collider=go.AddComponent<SphereCollider>(); collider.radius=0.015f; collider.isTrigger=true;
                go.AddComponent<StripButton>().Action=actions[i]; buttons.Add(go.GetComponent<Renderer>());
                var icon=new LineBuilder();
                for(int k=0;k<16;k++)
                {
                    float a=k*Mathf.PI/8,b=(k+1)*Mathf.PI/8;
                    if(i==0) for(int side=-1;side<=1;side+=2) icon.Segment(new Vector3(side*0.0025f+0.003f*Mathf.Cos(a),0.003f*Mathf.Sin(a),-0.01f),new Vector3(side*0.0025f+0.003f*Mathf.Cos(b),0.003f*Mathf.Sin(b),-0.01f),0.0012f,Color.white);
                    else if(i==2) icon.Segment(new Vector3(0.005f*Mathf.Cos(a),0.005f*Mathf.Sin(a),-0.01f),new Vector3(0.005f*Mathf.Cos(b),0.005f*Mathf.Sin(b),-0.01f),0.0012f,Color.white);
                    else if(i==4 || i==7) icon.Segment(new Vector3(-0.006f+k*0.00075f,0.004f*Mathf.Sin(a*2),-0.01f),new Vector3(-0.006f+(k+1)*0.00075f,0.004f*Mathf.Sin(b*2),-0.01f),0.0012f,Color.white);
                }
                if(i==1 || i==6) for(int k=-1;k<=1;k++) icon.Segment(new Vector3(-0.005f,k*0.004f,-0.01f),new Vector3(0.005f,k*0.004f,-0.01f),0.0012f,Color.white);
                if(i==3 || i==5) icon.Arrow(new Vector3(0,-0.005f,-0.01f),new Vector3(0,0.005f,-0.01f),0.0012f,Color.white,0.3f);
                Mats.Object("Control icon",go.transform,icon.Commit(new Mesh {name="Spatial icon"}),Mats.Line(Look.TEXT));
                captions.Add(Labels.Make(parent,names[i],at+Vector3.down*0.025f,0.24f,Look.TEXT));
            }
        }
        public void Tick()
        {
            tissueTools.rotation=Quaternion.LookRotation(tissueTools.position-app.Eye,Vector3.up);
            scannerTools.rotation=Quaternion.LookRotation(scannerTools.position-app.Eye,Vector3.up);
            for(int k=0;k<buttons.Count;k++) { bool usable=!app.Demonstrating||k==2; buttons[k].gameObject.SetActive(usable); captions[k].gameObject.SetActive(usable); buttons[k].transform.rotation=Quaternion.LookRotation(buttons[k].transform.position-app.Eye,Vector3.up); bool active=k switch {0=>app.SyncTissue,1=>app.ShowTissue,2=>app.PhaseColour,3=>app.EnsembleMoments,4=>app.ShowFields,5=>app.StepIndex<3,6=>app.StepIndex==9,_=>app.StepIndex==3}; buttons[k].sharedMaterial.SetColor("_Color",active?new Color(0.2f,0.85f,1,0.9f):new Color(0.6f,0.75f,0.85f,0.14f)); }
            var s=app.Sim.State; if(s==null)return;
            if(revision!=s.Revision)
            {
                revision=s.Revision;
                fieldMatrices=new Matrix4x4[s.Cube.N]; rfMatrices=new Matrix4x4[s.Cube.N];
                fieldColours=new Vector4[s.Cube.N]; rfColours=new Vector4[s.Cube.N];
            }
            if(!app.ShowFields || app.Demonstrating) { gradientOn=rfOn=false; return; }
            var sim=app.Sim;
            // Cached per-ampere vector fields; only superposition runs each frame.
            gradientOn=System.Math.Abs(sim.Ix)+System.Math.Abs(sim.Iy)+System.Math.Abs(sim.Iz)>0.01; rfOn=sim.RfOn;
            Matrix4x4 frame=app.Cube.Root.localToWorldMatrix;
            for(int i=0;i<s.Cube.N;i++)
            {
                var f=s.Cube.F[i];
                double df=s.Cube.DeltaF(i,new Currents(s.P.MagnetCurrent,sim.Ix,sim.Iy,sim.Iz))-s.Cube.DeltaF(i,new Currents(s.P.MagnetCurrent,0,0,0));
                float u=Mathf.Clamp01(0.5f+(float)df/3000f);
                var colour=Color.Lerp(Look.GZ,Look.GY,u); colour.a=0.10f+0.28f*Mathf.Abs(u-0.5f)*2;
                Vector3 pos=Frames.ToS(s.Cube.Pos[i]-s.CubeCentre)*Look.BlockMagnification;
                fieldMatrices[i]=frame*Matrix4x4.Translate(pos); fieldColours[i]=colour;
                if(sim.RfOn)
                {
                    double re=sim.RfRe*f.B1Re-sim.RfIm*f.B1Im, im=sim.RfRe*f.B1Im+sim.RfIm*f.B1Re;
                    double c=System.Math.Cos(-app.DisplayCarrier),sn=System.Math.Sin(-app.DisplayCarrier);
                    Vector3 d=Frames.ToS(re*c-im*sn,re*sn+im*c,0);
                    float strength=Mathf.Clamp01(d.magnitude/0.000012f);
                    rfMatrices[i]=frame*Matrix4x4.TRS(pos,Quaternion.FromToRotation(Vector3.up,d.magnitude>1e-15f ? d/d.magnitude : Vector3.up),new Vector3(0.018f,0.035f*strength,0.018f));
                    rfColours[i]=new Vector4(1,0.35f,0.35f,0.55f);
                }
            }
            fieldBlock.SetVectorArray("_Tint",fieldColours); rfBlock.SetVectorArray("_Tint",rfColours);
            Submit(null);
        }
        public void Dispose() { Object.Destroy(sampleMesh); Object.Destroy(rfMesh); Object.Destroy(fieldMaterial); }

        public void Submit(Camera camera)
        {
            if(fieldMatrices==null)return;
            var rp=new RenderParams(fieldMaterial) { camera=camera, matProps=fieldBlock, worldBounds=new Bounds(app.Cube.Root.position,Vector3.one*2), shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows=false };
            if(gradientOn)Graphics.RenderMeshInstanced(rp,sampleMesh,0,fieldMatrices,fieldMatrices.Length);
            rp.matProps=rfBlock;
            if(rfOn)Graphics.RenderMeshInstanced(rp,rfMesh,0,rfMatrices,rfMatrices.Length);
        }
    }
}
