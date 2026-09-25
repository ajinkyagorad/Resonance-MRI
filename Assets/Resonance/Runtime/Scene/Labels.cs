using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Nebulytic.Resonance
{
    /// <summary>
    /// Short text in the scene: titles, axis letters and live values beside the plots, the scanner, the proton block and the
    /// close-up. No legends: every label names or measures the thing it sits next to. Labels turn to face the eye every
    /// frame (a rotation only) and change their text only when it changes. 0.8.4: about twice the size of 0.8.3's, and every
    /// label has a dark outline (one shared material), so it reads over a bright room as well as on the dark backings.
    /// </summary>
    public static class Labels
    {
        static TMP_FontAsset font; static Material outlined; static readonly List<TextMeshPro> all = new List<TextMeshPro>(), facing = new List<TextMeshPro>();
        public static IReadOnlyList<TextMeshPro> All => all;

        /// <summary>The font and the shared outlined material for scene text.</summary>
        public static TMP_FontAsset Font { get { Init(); return font; } }
        public static Material Outlined { get { Init(); return outlined; } }

        static void Init()
        {
            if (font != null) return;
            font = Resources.Load<TMP_FontAsset>("AtlasSansSDF");
            outlined = new Material(font.material) { name = "Scene label (outlined)" };
            outlined.EnableKeyword("OUTLINE_ON");
            outlined.EnableKeyword("UNDERLAY_ON");
            outlined.SetColor("_UnderlayColor", new Color(0.02f, 0.025f, 0.035f, 0.85f));
            outlined.SetFloat("_UnderlayOffsetY", -0.25f);
            outlined.SetFloat("_UnderlayDilate", 0.12f);
            outlined.SetFloat("_UnderlaySoftness", 0.18f);
            outlined.SetFloat("_FaceDilate", 0.06f);
            outlined.SetFloat("_OutlineWidth", 0.18f);
            outlined.SetColor("_OutlineColor", Look.INK);
            outlined.renderQueue = 3100; // after the scene's transparent layers
        }

        /// <summary>
        /// A label. face: it turns to face the eye every frame (labels floating in 3-D: axis letters, values beside objects);
        /// false for text printed on a panel, which lies flat on the panel (the panel faces the viewer; a label turned about its
        /// own anchor would swing partly behind the panel).
        /// </summary>
        public static TextMeshPro Make(Transform parent, string text, Vector3 localPos, float size, Color c, TextAlignmentOptions align = TextAlignmentOptions.Center, float width = 0.3f, bool face = true)
        {
            Init();
            var go = new GameObject("Label"); go.transform.SetParent(parent, false); go.transform.localPosition = localPos;
            var t = go.AddComponent<TextMeshPro>(); t.font = font; t.fontSharedMaterial = outlined; t.fontSize = size; t.color = c; t.alignment = align;
            t.rectTransform.sizeDelta = new Vector2(width, size * 0.12f); t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Overflow; t.margin = Vector4.zero;
            // The given position is where the text starts (left-aligned), ends (right-aligned) or is centred.
            bool left = (align & (TextAlignmentOptions)0x1) != 0 && align != TextAlignmentOptions.Center, right = (align & (TextAlignmentOptions)0x4) != 0;
            t.rectTransform.pivot = new Vector2(left ? 0 : right ? 1 : 0.5f, 0.5f);
            t.text = text;
            all.Add(t); if (face) facing.Add(t);
            return t;
        }

        /// <summary>Sets the text only when it differs (TextMeshPro rebuilds its mesh on every assignment).</summary>
        public static void Set(TextMeshPro t, string s) { if (t.text != s) t.text = s; }

        public static void Face(Vector3 eye)
        {
            for (int i = 0; i < facing.Count; i++)
            {
                var t = facing[i]; if (t == null || !t.gameObject.activeInHierarchy) continue;
                var tr = t.transform; Vector3 d = tr.position - eye;
                if (d.sqrMagnitude > 1e-6f) tr.rotation = Quaternion.LookRotation(d, Vector3.up);
            }
        }
    }
}
