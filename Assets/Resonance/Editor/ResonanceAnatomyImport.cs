using UnityEditor;using UnityEngine;
public sealed class ResonanceAnatomyImport:AssetPostprocessor {
 public override uint GetVersion()=>1;
 void OnPreprocessModel(){if(!assetPath.StartsWith("Assets/Resources/Anatomy/"))return;var importer=(ModelImporter)assetImporter;importer.importNormals=ModelImporterNormals.Import;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.importAnimation=false;}
 void OnPostprocessModel(GameObject root){
 if(!assetPath.StartsWith("Assets/Resources/Anatomy/")||!assetPath.EndsWith(".obj"))return;
 // Unity's OBJ importer reflects X. Restore the physical volume coordinates,
 // including handedness of normals and winding, once in the import pipeline.
 foreach(var filter in root.GetComponentsInChildren<MeshFilter>()){
 var mesh=filter.sharedMesh;var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i++)vertices[i].x=-vertices[i].x;mesh.vertices=vertices;
 var normals=mesh.normals;for(int i=0;i<normals.Length;i++)normals[i].x=-normals[i].x;mesh.normals=normals;
 for(int sub=0;sub<mesh.subMeshCount;sub++){var triangles=mesh.GetTriangles(sub);for(int i=0;i<triangles.Length;i+=3)(triangles[i+1],triangles[i+2])=(triangles[i+2],triangles[i+1]);mesh.SetTriangles(triangles,sub,false);}mesh.RecalculateBounds();
 }
 }
}
