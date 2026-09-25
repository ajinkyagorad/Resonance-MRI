using System.IO;using System.Xml;using UnityEditor.Android;
public sealed class ResonanceManifest:IPostGenerateGradleAndroidProject {
 public int callbackOrder=>int.MaxValue;
 public void OnPostGenerateGradleAndroidProject(string path){
 var file=Path.Combine(path,"src/main/AndroidManifest.xml");var doc=new XmlDocument();doc.Load(file);var root=doc.DocumentElement;const string ns="http://schemas.android.com/apk/res/android";
 foreach(XmlNode node in doc.SelectNodes("/manifest/uses-feature"))if(node.Attributes?["name",ns]?.Value=="com.oculus.feature.BOUNDARYLESS_APP")root.RemoveChild(node);
 bool exists=false;foreach(XmlNode node in doc.SelectNodes("/manifest/uses-permission"))if(node.Attributes?["name",ns]?.Value=="com.oculus.permission.BOUNDARY_VISIBILITY")exists=true;
 if(!exists){var item=doc.CreateElement("uses-permission");item.SetAttribute("name",ns,"com.oculus.permission.BOUNDARY_VISIBILITY");root.AppendChild(item);}doc.Save(file);
 }
}
