using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Nebulytic.Resonance
{
    /// <summary>
    /// The narration strip (SPEC 5.3; 0.8.4: an opaque dark pill with a rim, about 1.5 times the 0.8.3 text size, subtitles
    /// on up to two lines). The spoken sentence is shown
    /// centred as subtitles (the CC chip or C toggles them; on by default), a thin progress line below it, the step and the
    /// physical clock small and quiet, transport icons (previous, play/pause, next) on one side and toggle chips (CC,
    /// MR/VR, voice, frame times, credits) on the other; an active chip is lit. The narration runs on by itself.
    /// It faces the eye (billboard, yaw and pitch) with a dead band so it does not jitter.
    /// </summary>
    public sealed class Strip
    {
        public const float W = 0.72f, H = 0.206f;
        readonly App app; public readonly Transform Root;
        readonly TextMeshPro title, caption, clock, voice, mode, credits, perf, perfIcon, cc;
        const float TrackX0 = -0.272f, TrackW = 0.544f, RowY = -0.079f, TrackY = -0.039f;
        // Cached text (strings are rebuilt only when what they show changes; the clock at most ten times a second).
        int shownStep = -2, shownCount = -1; LessonStep shownStepRef; string shownCaption; float nextClock, nextPerf; string shownVoice, shownMode;
        readonly Transform fill, thumb; readonly TextMeshPro speedLabel; LessonStep ticksFor; readonly List<GameObject> ticks = new List<GameObject>();
        readonly GameObject playIcon, pauseIcon, creditsCard;
        readonly GameObject soundWaves, soundSlash;
        readonly Renderer ccChip, perfChip; bool shownCc, shownPerf = true;
        readonly TMP_FontAsset font;
        string readout; float readoutUntil;

        public Strip(App app, Transform parent)
        {
            this.app = app;
            font = Resources.Load<TMP_FontAsset>("AtlasSansSDF");
            Root = new GameObject("Narration strip").transform; Root.SetParent(parent, false); Root.localScale = Vector3.one * Look.StripScale;
            // Opaque dark pill with a rim; every other strip element draws in front of it.
            var panelMat = Mats.Plate(Look.PANEL);
            float inner = W / 2 - H / 2 + 0.006f; // the straight part of the pill
            title = Text("Title", new Vector3(-inner + 0.13f, 0.078f, -0.002f), 0.3f, 0.02f, 0.13f, TextAlignmentOptions.Left);
            title.color = Look.TEXT2;
            clock = Text("Clock", new Vector3(inner - 0.13f, 0.078f, -0.002f), 0.3f, 0.02f, 0.12f, TextAlignmentOptions.Right);
            clock.color = Look.TEXT3;
            caption = Text("Subtitles", new Vector3(0, 0.022f, -0.002f), W - 0.12f, 0.062f, 0.2f, TextAlignmentOptions.Center);
            caption.color = Look.TEXT;
            caption.textWrappingMode = TextWrappingModes.Normal; caption.overflowMode = TextOverflowModes.Overflow;
            // Progress: one line.
            Mats.Object("Track", Root, MeshKit.RoundedPanel(TrackW, 0.004f, 0.002f, 0.001f, 3), Mats.Flat(Look.TRACK * 1.6f), new Vector3(TrackX0 + TrackW / 2, TrackY, -0.002f));
            fill = Mats.Object("Fill", Root, MeshKit.Quad(1, 0.004f), Mats.Flat(Look.TEXT), new Vector3(TrackX0, TrackY, -0.003f)).transform;
            thumb = Mats.Object("Timeline thumb", Root, MeshKit.Sphere(0.007f, 16, 10), Mats.Flat(Look.TEXT),
                new Vector3(TrackX0, TrackY, -0.006f)).transform;
            var seekTarget = new GameObject("Timeline seek"); seekTarget.transform.SetParent(Root, false);
            seekTarget.transform.localPosition = new Vector3(TrackX0 + TrackW / 2, TrackY, -0.008f);
            AddHit(seekTarget, "seek", new Vector3(TrackW + 0.02f, 0.028f, 0.015f));
            // Transport icons (one side) and toggle chips (the other), about 1.7 times 0.8.3's.
            var icon = Mats.Flat(Look.TEXT);
            float ix = -0.272f; const float iconScale = 1.7f;
            Button("prev", new Vector3(ix, RowY, -0.003f), TriangleMesh(false, true), icon).transform.localScale = Vector3.one * iconScale;
            playIcon = Button("play", new Vector3(ix + 0.04f, RowY, -0.003f), TriangleMesh(true), icon); playIcon.transform.localScale = Vector3.one * iconScale;
            pauseIcon = new GameObject("Pause icon"); pauseIcon.transform.SetParent(playIcon.transform.parent, false); pauseIcon.transform.localPosition = new Vector3(ix + 0.04f, RowY, -0.003f); pauseIcon.transform.localScale = Vector3.one * iconScale;
            foreach (float x in new[] { -0.0028f, 0.0028f }) Mats.Object("Bar", pauseIcon.transform, MeshKit.Quad(0.0032f, 0.010f), icon, new Vector3(x, 0, 0));
            Button("next", new Vector3(ix + 0.08f, RowY, -0.003f), TriangleMesh(true, true), icon).transform.localScale = Vector3.one * iconScale;
            speedLabel = Chip("speed", "1×", new Vector3(-0.132f, RowY, -0.003f), out _);
            Button("section-prev", new Vector3(-0.072f, RowY, -0.003f), TriangleMesh(false, false), icon).transform.localScale = Vector3.one * iconScale;
            Button("section-next", new Vector3(-0.032f, RowY, -0.003f), TriangleMesh(true, false), icon).transform.localScale = Vector3.one * iconScale;
            var sectionLabel=Text("Section navigation",new Vector3(-0.052f,RowY-0.024f,-0.003f),0.09f,0.014f,0.07f,TextAlignmentOptions.Center); sectionLabel.text="Section";
            float cx = 0.040f; const float dx = 0.052f;
            cc = Chip("subtitles", "CC", new Vector3(cx, RowY, -0.003f), out ccChip);
            mode = Chip("mode", "MR", new Vector3(cx + dx, RowY, -0.003f), out _);
            voice = Chip("voice", "M", new Vector3(cx + 2 * dx, RowY, -0.003f), out _);
            perfIcon = Chip("mute", "", new Vector3(cx + 3 * dx, RowY, -0.003f), out perfChip);
            var speaker = new LineBuilder();
            speaker.Segment(new Vector3(-0.009f,-0.003f,0),new Vector3(-0.009f,0.003f,0),0.003f,Color.white);
            speaker.Segment(new Vector3(-0.009f,0.003f,0),new Vector3(-0.003f,0.007f,0),0.002f,Color.white);
            speaker.Segment(new Vector3(-0.003f,0.007f,0),new Vector3(-0.003f,-0.007f,0),0.002f,Color.white);
            speaker.Segment(new Vector3(-0.003f,-0.007f,0),new Vector3(-0.009f,-0.003f,0),0.002f,Color.white);
            Mats.Object("Speaker", perfIcon.transform, speaker.Commit(new Mesh { name = "Speaker icon" }), Mats.Line(Look.TEXT));
            var waves = new LineBuilder();
            for (int r = 0; r < 2; r++) for (int j = 0; j < 8; j++)
            { float a = -0.8f + j * 0.2f, c = a + 0.2f, rad = 0.006f + 0.005f * r;
              waves.Segment(new Vector3(rad*Mathf.Cos(a),rad*Mathf.Sin(a),0),new Vector3(rad*Mathf.Cos(c),rad*Mathf.Sin(c),0),0.0014f,Color.white); }
            soundWaves = Mats.Object("Sound waves", perfIcon.transform, waves.Commit(new Mesh { name = "Audio waves" }), Mats.Line(Look.TEXT));
            var slash = new LineBuilder(); slash.Segment(new Vector3(-0.012f,-0.009f,0),new Vector3(0.013f,0.010f,0),0.002f,Color.white);
            soundSlash = Mats.Object("Muted", perfIcon.transform, slash.Commit(new Mesh { name = "Mute slash" }), Mats.Line(Look.RF)); soundSlash.SetActive(false);
            Chip("credits", "i", new Vector3(cx + 4 * dx, RowY, -0.003f), out _);
            perf = Text("Frame times", new Vector3(W / 2 - 0.2f, H / 2 + 0.022f, -0.002f), 0.4f, 0.02f, 0.11f, TextAlignmentOptions.Right); perf.color = Look.TEXT; perf.text = ""; perf.fontSharedMaterial = Labels.Outlined;
            // Credits card (only while open).
            creditsCard = new GameObject("Credits"); creditsCard.transform.SetParent(Root, false); creditsCard.transform.localPosition = new Vector3(0, 0.25f, 0);
            Mats.Object("Card", creditsCard.transform, MeshKit.RoundedPanel(0.78f, 0.38f, 0.024f, 0.004f), panelMat, new Vector3(0, 0, 0.004f));
            credits = Text("Credits text", new Vector3(0, 0.034f, -0.002f), 0.74f, 0.27f, 0.11f, TextAlignmentOptions.TopLeft, creditsCard.transform);
            credits.textWrappingMode = TextWrappingModes.Normal; credits.color = Look.TEXT2;
            credits.text = "Anatomy: BodyParts3D, © The Database Center for Life Science, CC BY 4.0 (modified: isolated right hand, segmented tissues, decimated surfaces).\n" +
                           "Receiver architecture: MaRCoS, Negnevitsky et al., J. Magn. Reson. 2023 (arXiv 2208.01616).\n" +
                           "Voices: Kokoro-82M (Apache 2.0), voices am_michael and bm_george. Font: DejaVu Sans.\n" +
                           "Scanner, windings, fields, spins and signals are this app's own simulation (1 T class extremity scanner, own winding designs). Educational; not a medical device.";
            var source=Text("Source repository",new Vector3(0,-0.15f,-0.005f),0.40f,0.036f,0.13f,TextAlignmentOptions.Center,creditsCard.transform);
            source.text="GitHub · source"; source.color=Look.GX;
            Mats.Object("Source button",creditsCard.transform,MeshKit.RoundedPanel(0.42f,0.046f,0.018f,0.002f),Mats.Flat(Look.CHIP),new Vector3(0,-0.15f,0));
            AddHit(source.gameObject,"repository",new Vector3(0.43f,0.05f,0.012f));
            creditsCard.SetActive(false);
            var col = Root.gameObject.AddComponent<BoxCollider>(); col.size = new Vector3(W, H + 0.035f, 0.02f); col.isTrigger = true;
            Root.gameObject.AddComponent<Grabbable>().Kind = "strip";
        }

        /// <summary>A toggle chip: a small rounded background with a short label; the whole chip is the hit area.</summary>
        TextMeshPro Chip(string action, string label, Vector3 pos, out Renderer background)
        {
            var bg = Mats.Object("Chip " + action, Root, MeshKit.RoundedPanel(0.046f, 0.027f, 0.0135f, 0.001f, 6), Mats.Flat(Look.CHIP), pos + new Vector3(0, 0, 0.0005f));
            bg.GetComponent<Renderer>().sharedMaterial.renderQueue = 2995; background = bg.GetComponent<Renderer>();
            var t = Text("Chip label " + action, pos, 0.046f, 0.02f, 0.09f, TextAlignmentOptions.Center); t.text = label; t.color = Look.TEXT2;
            AddHit(t.gameObject, action, new Vector3(0.05f, 0.032f, 0.012f));
            return t;
        }

        TextMeshPro Text(string name, Vector3 pos, float w, float h, float size, TextAlignmentOptions align, Transform parent = null)
        {
            var go = new GameObject(name); go.transform.SetParent(parent ?? Root, false); go.transform.localPosition = pos;
            var t = go.AddComponent<TextMeshPro>(); t.font = font; t.fontSize = size; t.color = Look.TEXT * 0.85f; t.alignment = align;
            t.fontSharedMaterial = Labels.Outlined; t.fontSharedMaterial.renderQueue = 3100; // after the dark panels (Transparent), so text is never covered
            t.rectTransform.sizeDelta = new Vector2(w, h); t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis; t.margin = Vector4.zero;
            return t;
        }

        GameObject Button(string action, Vector3 pos, Mesh mesh, Material mat)
        {
            var go = Mats.Object("Icon " + action, Root, mesh, mat, pos);
            AddHit(go, action, new Vector3(0.024f, 0.022f, 0.012f)); // scaled with the icon
            return go;
        }

        static void AddHit(GameObject go, string action, Vector3 size)
        {
            var c = go.AddComponent<BoxCollider>(); c.size = size; c.isTrigger = true;
            go.AddComponent<StripButton>().Action = action;
        }

        static Mesh TriangleMesh(bool right, bool withBar = false)
        {
            var m = new Mesh { name = "Triangle" }; float s = right ? 1 : -1;
            var v = new List<Vector3> { new Vector3(-0.005f * s, -0.007f, 0), new Vector3(-0.005f * s, 0.007f, 0), new Vector3(0.006f * s, 0, 0) };
            var t = new List<int> { 0, 1, 2 };
            if (withBar)
            {
                float x = 0.0075f * s; int k = v.Count;
                v.AddRange(new[] { new Vector3(x - 0.0012f, -0.007f, 0), new Vector3(x + 0.0012f, -0.007f, 0), new Vector3(x + 0.0012f, 0.007f, 0), new Vector3(x - 0.0012f, 0.007f, 0) });
                t.AddRange(new[] { k, k + 2, k + 1, k, k + 3, k + 2 });
            }
            m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        public void ShowReadout(string text) { readout = text; readoutUntil = Time.unscaledTime + 1.5f; }
        public void ToggleCredits() => creditsCard.SetActive(!creditsCard.activeSelf);
        public bool CreditsOpen => creditsCard.activeSelf;
        public string CaptionText => caption.text;
        public string TitleText => title.text;

        /// <summary>One seekable timeline, with unobtrusive sentence boundaries.</summary>
        public void SetTicks(LessonStep s)
        {
            if (ticksFor == s) return;
            ticksFor=s; foreach(var tick in ticks) Object.Destroy(tick); ticks.Clear();
            if(s==null || s.Len<=0)return;
            foreach(var cue in s.Cues)
            {
                float x=TrackX0+TrackW*(float)(cue.Start/s.Len);
                ticks.Add(Mats.Object("Section boundary",Root,MeshKit.Quad(0.001f,0.007f),Mats.Flat(Look.TEXT3),new Vector3(x,TrackY,-0.004f)));
            }
        }

        public bool SeekFraction(Ray ray, out float fraction)
        {
            fraction=0;
            var plane=new Plane(Root.forward,Root.TransformPoint(new Vector3(0,TrackY,-0.008f)));
            if(!plane.Raycast(ray,out float distance) || distance<0)return false;
            fraction=Mathf.Clamp01((Root.InverseTransformPoint(ray.GetPoint(distance)).x-TrackX0)/TrackW);
            return true;
        }
        public Vector3 TimelinePoint(float fraction) => Root.TransformPoint(new Vector3(TrackX0+TrackW*Mathf.Clamp01(fraction),TrackY,-0.008f));

        public void Tick(int stepIndex, int stepCount, LessonStep step, LessonCue cue, double tau, bool playing, bool overview, string status, double t, double speed, bool lab)
        {
            if (stepIndex != shownStep || stepCount != shownCount || step != shownStepRef)
            {
                shownStep = stepIndex; shownCount = stepCount; shownStepRef = step;
                title.text = step != null ? $"{stepIndex + 1} / {stepCount}  ·  {step.Title}" : "Resonance";
            }
            string cap = status ?? (overview ? (stepIndex == 0 && tau <= 0 ? "Press ▶ to begin." : step != null && tau >= step.Len - 0.01 ? (stepIndex == stepCount - 1 ? "That is the whole scan. Press ▶ to start again." : cue?.Text) : cue?.Text) : cue?.Text) ?? "";
            if (!app.Subtitles && status == null) cap = ""; // subtitles off: the spoken sentence is not shown
            if (!ReferenceEquals(cap, shownCaption)) { shownCaption = cap; caption.text = cap; }
            float now = Time.unscaledTime;
            if (readout != null && now < readoutUntil) { if (clock.text != readout) clock.text = readout; }
            else if (now >= nextClock || app.FixedStep) { nextClock = now + 0.1f; clock.text = app.Demonstrating ? "Slowed model" : ClockText(t, speed, lab); }
            float f = step != null && step.Len > 0 ? Mathf.Clamp01((float)(tau / step.Len)) : 0;
            fill.localScale = new Vector3(TrackW * f, 1, 1); fill.localPosition = new Vector3(TrackX0 + TrackW * 0.5f * f, TrackY, -0.003f);
            thumb.localPosition=new Vector3(TrackX0+TrackW*f,TrackY,-0.006f);
            string rate=app.PlaybackRate+"×"; if(speedLabel.text!=rate)speedLabel.text=rate;
            var pr = playIcon.GetComponent<Renderer>(); if (pr.enabled == playing) pr.enabled = !playing;
            if (pauseIcon.activeSelf != playing) pauseIcon.SetActive(playing);
            string vo = app.VoiceName == "bm_george" ? "G" : "M"; if (!ReferenceEquals(vo, shownVoice)) { shownVoice = vo; voice.text = vo; }
            string mo = app.Environment != null && app.Environment.VirtualReality ? "VR" : "MR"; if (!ReferenceEquals(mo, shownMode)) { shownMode = mo; mode.text = mo; }
            if (app.Subtitles != shownCc) { shownCc = app.Subtitles; ccChip.sharedMaterial.SetColor("_Color", shownCc ? Look.CHIP_ON : Look.CHIP); cc.color = shownCc ? Look.TEXT : Look.TEXT3; }
            if (soundSlash.activeSelf != app.Muted) { soundSlash.SetActive(app.Muted); soundWaves.SetActive(!app.Muted); }
            // Optional frame-time readout (device evidence; off by default).
            if (app.ShowFrameTimes)
            {
                if (now >= nextPerf && app.Perf != null)
                {
                    nextPerf = now + 0.5f; var p = app.Perf;
                    perf.text = p.GpuMs > 0 ? $"CPU {p.CpuMs:0.0} ms · GPU {p.GpuMs:0.0} ms · {p.Fps:0} Hz" : $"CPU {p.CpuMs:0.0} ms · {p.Fps:0} Hz";
                }
            }
            else if (perf.text.Length > 0) perf.text = "";
            perfIcon.color = app.ShowFrameTimes ? Look.TEXT : Look.TEXT2;
        }

        static string ClockText(double t, double speed, bool lab)
        {
            string tt = t < 1e-3 ? $"t {t * 1e6:0.000} µs" : t < 1 ? $"t {t * 1e3:0.000} ms" : $"t {t:0.000} s";
            if (speed <= 0) return tt;
            double perSecond = 1 / speed;
            string sp = perSecond < 1e-6 ? $"{perSecond * 1e9:0.#} ns" : perSecond < 1e-3 ? $"{perSecond * 1e6:0.#} µs" : $"{perSecond * 1e3:0.#} ms";
            return $"{tt}  ·  1 s = {sp}";
        }
    }
}
