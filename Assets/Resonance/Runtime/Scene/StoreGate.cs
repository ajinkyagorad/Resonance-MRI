using System;
using System.Collections;
using Oculus.Platform;
using Oculus.Platform.Models;
using TMPro;
using UnityEngine;
using Application = UnityEngine.Application;
using Vector3 = UnityEngine.Vector3;

namespace Nebulytic.Resonance
{
    /// <summary>Store builds only: verifies the Horizon entitlement before the experience starts; otherwise explains and quits.</summary>
    public sealed class StoreGate : MonoBehaviour
    {
        bool completed; Action accepted;

        public static void Check(App app, Action ready)
        {
            var gate = app.gameObject.AddComponent<StoreGate>(); gate.accepted = ready; gate.Begin(app);
        }

        void Begin(App app)
        {
            var asset = Resources.Load<TextAsset>("ResonanceStoreAppId");
            if (!asset || !ulong.TryParse(asset.text.Trim(), out var id) || id == 0) { Fail(app, "Store app configuration is missing."); return; }
            StartCoroutine(Timeout(app));
            try
            {
                Core.AsyncInitialize(asset.text.Trim()).OnComplete(message =>
                {
                    if (completed) return;
                    if (message.IsError || message.Data.Result != PlatformInitializeResult.Success) { Fail(app, "Unable to verify your Horizon licence."); return; }
                    Entitlements.IsUserEntitledToApplication().OnComplete(entitlement =>
                    {
                        if (completed) return;
                        if (entitlement.IsError) { Fail(app, "This account does not own this application."); return; }
                        completed = true; accepted(); Destroy(this);
                    });
                });
            }
            catch (Exception) { Fail(app, "Horizon services are unavailable."); }
        }

        IEnumerator Timeout(App app) { yield return new WaitForSecondsRealtime(8); if (!completed) Fail(app, "Licence verification timed out. Please reopen from your Horizon library."); }

        void Fail(App app, string message)
        {
            if (completed) return; completed = true;
            var root = new GameObject("Store licence status").transform; root.SetParent(app.HeadCamera.transform, false); root.localPosition = new Vector3(0, 0, 1);
            Mats.Object("Status", root, MeshKit.RoundedPanel(0.68f, 0.18f, 0.012f), Mats.Plate(Look.PANEL));
            var go = new GameObject("Message"); go.transform.SetParent(root, false); go.transform.localPosition = new Vector3(0, 0, -0.01f);
            var t = go.AddComponent<TextMeshPro>(); t.font = Resources.Load<TMP_FontAsset>("AtlasSansSDF"); t.fontSize = 0.2f; t.color = Look.TEXT; t.alignment = TextAlignmentOptions.Center;
            t.rectTransform.sizeDelta = new Vector2(0.62f, 0.15f); t.text = message + "\nReturning to Horizon…";
            StartCoroutine(Exit());
        }

        IEnumerator Exit() { yield return new WaitForSecondsRealtime(4); Application.Quit(); }
    }
}
