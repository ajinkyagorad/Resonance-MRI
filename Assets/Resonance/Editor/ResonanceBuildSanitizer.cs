using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Keeps credentials out of every player build (0.8.4). The Meta XR SDK's DevAgentBuildProcessor writes this machine's
/// network address and an AgentBridge access token into Resources/DevAgentSettings.asset before each build, whether or
/// not the DevAgent is enabled (the APKs of 0.7.0-0.8.3 carry them). This processor runs after it (callback order 100),
/// clears the address and both tokens, and keeps the DevAgent disabled. Tools/verify_apk.py checks the result.
/// </summary>
public sealed class ResonanceBuildSanitizer : IPreprocessBuildWithReport
{
    public int callbackOrder => 100;

    public void OnPreprocessBuild(BuildReport report) => Scrub();

    /// <summary>Clears the DevAgent's address and tokens in memory (the build packs the in-memory state). Returns the fields cleared.</summary>
    public static int Scrub()
    {
        int cleared = 0;
        foreach (var guid in AssetDatabase.FindAssets("DevAgentSettings"))
        {
            var obj = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            if (obj == null) continue;
            var so = new SerializedObject(obj);
            foreach (var name in new[] { "serverAddress", "accessToken", "witClientAccessToken" })
            {
                var p = so.FindProperty(name);
                if (p != null && p.propertyType == SerializedPropertyType.String && !string.IsNullOrEmpty(p.stringValue)) { p.stringValue = ""; cleared++; }
            }
            var enabled = so.FindProperty("enabled");
            if (enabled != null && enabled.propertyType == SerializedPropertyType.Boolean) enabled.boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(obj);
        }
        Debug.Log("RESONANCE_SANITIZED cleared " + cleared + " DevAgent address and token fields");
        return cleared;
    }
}
