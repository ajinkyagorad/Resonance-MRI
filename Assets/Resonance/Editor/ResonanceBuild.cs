using System;using System.IO;using System.Linq;using Nebulytic.Resonance;using UnityEditor;using UnityEditor.Build;using UnityEditor.Build.Reporting;using UnityEditor.SceneManagement;using UnityEditor.XR.Management;using UnityEditor.XR.Management.Metadata;using UnityEditor.XR.OpenXR.Features;using UnityEngine;using UnityEngine.Rendering;using UnityEngine.XR.Management;using UnityEngine.XR.OpenXR;using UnityEngine.XR.OpenXR.Features;using UnityEngine.XR.OpenXR.Features.Interactions;
public static class ResonanceBuild {
 public const string ScenePath="Assets/Scenes/Resonance.unity";public const string Version="0.9.3";public const int VersionCode=16;
 public static void Configure(){
 PrepareMathFont();EditorSettings.serializationMode=SerializationMode.ForceText;Directory.CreateDirectory("Assets/Scenes");Directory.CreateDirectory("Assets/XR/Settings");Directory.CreateDirectory("Builds");
 var group=BuildTargetGroup.Android;
 if(!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey,out XRGeneralSettingsPerBuildTarget perTarget)){
 perTarget=ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();AssetDatabase.CreateAsset(perTarget,"Assets/XR/Settings/XRGeneralSettingsPerBuildTarget.asset");EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey,perTarget,true);}
 var settings=perTarget.SettingsForBuildTarget(group);
 if(settings==null){settings=ScriptableObject.CreateInstance<XRGeneralSettings>();perTarget.SetSettingsForBuildTarget(group,settings);AssetDatabase.AddObjectToAsset(settings,perTarget);}
 if(settings.Manager==null){settings.Manager=ScriptableObject.CreateInstance<XRManagerSettings>();AssetDatabase.AddObjectToAsset(settings.Manager,perTarget);}
 if(!XRPackageMetadataStore.AssignLoader(settings.Manager,"UnityEngine.XR.OpenXR.OpenXRLoader",group))throw new Exception("OpenXR loader unavailable");
 foreach(var set in OpenXRFeatureSetManager.FeatureSetsForBuildTarget(group))if(set.featureSetId=="com.meta.openxr.featureset.metaxr")set.isEnabled=true;
 OpenXRFeatureSetManager.SetFeaturesFromEnabledFeatureSets(group);
 var xr=OpenXRSettings.GetSettingsForBuildTargetGroup(group);xr.renderMode=OpenXRSettings.RenderMode.SinglePassInstanced;
 foreach(var feature in xr.GetFeatures<OpenXRFeature>())if(feature is OculusTouchControllerProfile||feature is MetaQuestTouchPlusControllerProfile||feature is MetaQuestTouchProControllerProfile)feature.enabled=true;
 EditorUtility.SetDirty(xr);EditorUtility.SetDirty(settings);EditorUtility.SetDirty(perTarget);
 PlayerSettings.companyName="Nebulytic";PlayerSettings.productName="Resonance MRI";PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.nebulytic.resonance");PlayerSettings.bundleVersion=Version;PlayerSettings.Android.bundleVersionCode=VersionCode;
 PlayerSettings.SplashScreen.show=false;PlayerSettings.Android.applicationEntry=AndroidApplicationEntry.GameActivity;PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
 PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel32;PlayerSettings.Android.targetSdkVersion=AndroidSdkVersions.AndroidApiLevel34;
 PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;PlayerSettings.colorSpace=ColorSpace.Linear;PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.Vulkan});
 PlayerSettings.gpuSkinning=true;PlayerSettings.MTRendering=true;PlayerSettings.graphicsJobs=false;PlayerSettings.enableFrameTimingStats=true;PlayerSettings.runInBackground=true;PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android,ManagedStrippingLevel.Low);PlayerSettings.stripEngineCode=false;
 PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=1000;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
 // Every quality level, not only the editor's: Android builds use their own level (0.8.1 shipped without MSAA there).
 int qualityLevel=QualitySettings.GetQualityLevel();for(int q=0;q<QualitySettings.names.Length;q++){QualitySettings.SetQualityLevel(q,false);QualitySettings.antiAliasing=4;QualitySettings.shadows=ShadowQuality.Disable;QualitySettings.pixelLightCount=1;QualitySettings.softParticles=false;QualitySettings.realtimeReflectionProbes=false;QualitySettings.vSyncCount=0;QualitySettings.anisotropicFiltering=AnisotropicFiltering.Enable;}QualitySettings.SetQualityLevel(qualityLevel,false);
 var so=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);var input=so.FindProperty("activeInputHandler");if(input!=null)input.intValue=1;so.ApplyModifiedPropertiesWithoutUndo();
 var cfg=OVRProjectConfig.CachedProjectConfig;cfg.handTrackingSupport=OVRProjectConfig.HandTrackingSupport.ControllersAndHands;cfg.insightPassthroughSupport=OVRProjectConfig.FeatureSupport.Required;cfg.boundaryVisibilitySupport=OVRProjectConfig.FeatureSupport.Supported;cfg.sceneSupport=OVRProjectConfig.FeatureSupport.None;
 cfg.systemLoadingScreenBackground=OVRProjectConfig.SystemLoadingScreenBackground.ContextualPassthrough;cfg.targetDeviceTypes=new(){OVRProjectConfig.DeviceType.Quest3,OVRProjectConfig.DeviceType.Quest3S};OVRProjectConfig.CommitProjectConfig(cfg);
 foreach(var path in Directory.GetFiles("Assets/Resources/Anatomy","*.obj"))AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
 CreateScene();IncludeShaders();AssetDatabase.SaveAssets();Debug.Log("RESONANCE_CONFIGURED");
 }
 static void CreateScene(){
 var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab");
 var rigObj=(GameObject)PrefabUtility.InstantiatePrefab(prefab);PrefabUtility.UnpackPrefabInstance(rigObj,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
 var rig=rigObj.GetComponent<OVRCameraRig>();var manager=rigObj.GetComponent<OVRManager>();manager.isInsightPassthroughEnabled=true;manager.trackingOriginType=OVRManager.TrackingOrigin.FloorLevel;manager.shouldBoundaryVisibilityBeSuppressed=true;
 var ms=new SerializedObject(manager);var sh=ms.FindProperty("launchSimultaneousHandsControllersOnStartup");if(sh!=null)sh.boolValue=true;ms.ApplyModifiedPropertiesWithoutUndo();
 var layer=rigObj.AddComponent<OVRPassthroughLayer>();layer.overlayType=OVROverlay.OverlayType.Underlay;
 var camera=rig.centerEyeAnchor.GetComponent<Camera>();camera.tag="MainCamera";camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.nearClipPlane=.025f;camera.farClipPlane=30;camera.depthTextureMode=DepthTextureMode.None;camera.allowHDR=false;camera.allowMSAA=true;
 if(!camera.GetComponent<AudioListener>())camera.gameObject.AddComponent<AudioListener>();
 var root=new GameObject("Resonance MRI");var app=root.AddComponent<App>();app.Rig=rig;app.HeadCamera=camera;
 var controls=root.AddComponent<Controls>();controls.App=app;controls.LeftModel=Controller(rig.leftControllerAnchor,0);controls.RightModel=Controller(rig.rightControllerAnchor,1);controls.LeftHand=Hand(rig.leftHandAnchor,0);controls.RightHand=Hand(rig.rightHandAnchor,1);app.Controls=controls;
 var environment=root.AddComponent<Nebulytic.Resonance.Environment>();environment.App=app;app.Environment=environment;root.AddComponent<Performance>();
 RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.62f,.69f,.74f);RenderSettings.ambientEquatorColor=new Color(.33f,.42f,.46f);RenderSettings.ambientGroundColor=new Color(.16f,.20f,.23f);
 var sun=new GameObject("Broad inspection light").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.1f;sun.transform.rotation=Quaternion.Euler(32,-40,0);sun.shadows=LightShadows.None;
 EditorSceneManager.SaveScene(scene,ScenePath);EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
 }
 static void PrepareMathFont(){
  var font=Resources.Load<TMPro.TMP_FontAsset>("AtlasSansSDF");const string symbols="′₂ₓᵧγΔφπ∫Σμρ↑↓©‹›↻Ⅱ▶×✓∂₀₁−→̄∝±⊥°≈·½–◂▸∥µ…—";
  if(symbols.All(c=>font.HasCharacter(c)))return;
  var so=new SerializedObject(font);so.FindProperty("m_SourceFontFile").objectReferenceValue=Resources.Load<Font>("AtlasSans");so.ApplyModifiedPropertiesWithoutUndo();font.atlasPopulationMode=TMPro.AtlasPopulationMode.Dynamic;
  if(!font.TryAddCharacters(symbols,out string missing))throw new Exception("Missing symbolic font glyphs: "+missing);
  font.atlasPopulationMode=TMPro.AtlasPopulationMode.Static;EditorUtility.SetDirty(font);foreach(var tex in font.atlasTextures)EditorUtility.SetDirty(tex);AssetDatabase.SaveAssets();
 }
 static Transform Controller(Transform parent,int side){
 var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.core/Prefabs/OVRControllerPrefab.prefab");var helper=prefab.GetComponent<OVRControllerHelper>();var source=side==0?helper.m_modelMetaTouchPlusLeftController:helper.m_modelMetaTouchPlusRightController;
 var obj=UnityEngine.Object.Instantiate(source);obj.name="Official Touch Plus "+side;obj.transform.SetParent(parent,false);obj.transform.localPosition=source.transform.localPosition;obj.transform.localRotation=source.transform.localRotation;obj.transform.localScale=source.transform.localScale;obj.SetActive(false);
 for(int key=0;key<5;key++)if(Controls.FindControl(obj.transform,side,key)==null)throw new Exception("Controller anchor missing "+side+" / "+key);
 return obj.transform;
 }
 static OVRHand Hand(Transform parent,int side){
 var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.core/Prefabs/OVRHandPrefab.prefab");var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);go.transform.SetParent(parent,false);var hand=go.GetComponent<OVRHand>();var so=new SerializedObject(hand);so.FindProperty("HandType").intValue=side;so.ApplyModifiedPropertiesWithoutUndo();return hand;
 }
 static void IncludeShaders(){
 var settings=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0];var so=new SerializedObject(settings);var list=so.FindProperty("m_AlwaysIncludedShaders");
 var shaders=AssetDatabase.FindAssets("t:Shader",new[]{"Assets/Resonance/Shaders"}).Select(g=>AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(g))).Concat(new[]{Shader.Find("Standard"),Shader.Find("Unlit/Texture"),Shader.Find("TextMeshPro/Distance Field")});
 foreach(var shader in shaders){if(shader==null)throw new Exception("Required shader missing");bool found=false;for(int i=0;i<list.arraySize;i++)if(list.GetArrayElementAtIndex(i).objectReferenceValue==shader)found=true;if(found)continue;int ix=list.arraySize;list.InsertArrayElementAtIndex(ix);list.GetArrayElementAtIndex(ix).objectReferenceValue=shader;}so.ApplyModifiedPropertiesWithoutUndo();
 }
 public static void BuildAndroid(){Configure();Build(BuildTarget.Android,"Builds/Resonance-MRI-Quest-"+Version+".apk","android");}
 public static void BuildWindows(){var generatedBindings="Builds/Windows-"+Version+"/RuntimeActionBindings.json";if(File.Exists(generatedBindings))File.Delete(generatedBindings);PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);Build(BuildTarget.StandaloneWindows64,"Builds/Windows-"+Version+"/Resonance MRI.exe","windows");}
 public static void BuildStore(){
  string id=System.Environment.GetEnvironmentVariable("RESONANCE_META_APP_ID");
  if(!ulong.TryParse(id,out var numeric)||numeric==0)throw new BuildFailedException("Set the real numeric RESONANCE_META_APP_ID from your Horizon dashboard. No placeholder Store build is permitted.");
  Configure();File.WriteAllText("Assets/Resources/ResonanceStoreAppId.txt",id);AssetDatabase.Refresh();
  Oculus.Platform.PlatformSettings.MobileAppID=id;AssetDatabase.SaveAssets();
  Build(BuildTarget.Android,"Builds/Resonance-MRI-Horizon-"+Version+"-unsigned.apk","horizon",true);
 }
 static void Build(BuildTarget target,string path,string tag,bool store=false){
 var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=path,target=target,options=BuildOptions.None,extraScriptingDefines=store?new[]{"RESONANCE_STORE"}:Array.Empty<string>()});Directory.CreateDirectory("validation");File.WriteAllText("validation/build-"+tag+".json",JsonUtility.ToJson(new Evidence{result=report.summary.result.ToString(),bytes=report.summary.totalSize,errors=report.summary.totalErrors,warnings=report.summary.totalWarnings},true));
 File.WriteAllLines("validation/packed-"+tag+".txt",report.packedAssets.SelectMany(a=>a.contents).Select(c=>c.sourceAssetPath).Distinct().OrderBy(s=>s));
 if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Native "+tag+" build failed");Debug.Log("RESONANCE_"+tag.ToUpper()+"_READY");
 }
 [Serializable]class Evidence{public string result;public ulong bytes;public int errors,warnings;}
}
