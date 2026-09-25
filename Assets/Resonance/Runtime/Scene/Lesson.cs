using System;
using System.Collections.Generic;
using System.Globalization;
using Nebulytic.Resonance.Sim;
using UnityEngine;

namespace Nebulytic.Resonance
{
    [Serializable] public class LessonData { public int version; public float speed; public StepData[] steps; }
    [Serializable] public class StepData { public int step; public string title; public string strobe; public CueData[] cues; }
    [Serializable] public class CueData { public string id, text, generic, demo; public string[] refs; public SimData sim; public float dur, durGeneric; public string clip, clipGeneric; }
    [Serializable] public class SimData { public string hold, cut, speed; public string[] span; }

    public sealed class LessonCue
    {
        public CueData Data; public int Step, Index;
        public double Start, Lead, Len;        // lesson time within the step (s)
        public double T0, T1, D;               // physical span and display seconds per physical second (0 = hold)
        public bool Lab, Cut;
        public double Voice;                   // spoken duration (s)
        public string Text; public string Clip;
    }

    public sealed class LessonStep
    {
        public StepData Data; public readonly List<LessonCue> Cues = new List<LessonCue>();
        public double Len, EntryT, ExitT; public double[] StrobeT; public int[] StrobeBlock; public double StrobeP;
        public string Title => Data.title;
    }

    /// <summary>
    /// The guided lesson (SPEC 7) and its single time mapping (SPEC 4): each cue maps lesson time to physical time by
    /// hold, span (at one of the named speeds), cut or strobe. Physical time is the only time the simulation sees.
    /// </summary>
    public sealed class Lesson
    {
        public const double PulseSpeed = 1e4, RelaxSpeed = 100, CarrierTurnSeconds = 2.0, CutLead = 0.4, CueGap = 0.2, StepTail = 0.8;
        public readonly List<LessonStep> Steps = new List<LessonStep>();
        public LessonData Data { get; private set; }
        public string Voice { get; private set; }
        double fref;

        public static LessonData LoadData(string voice)
        {
            var asset = Resources.Load<TextAsset>("Lesson/Lesson-" + voice) ?? Resources.Load<TextAsset>("Lesson/Lesson");
            if (asset == null) throw new Exception("Lesson data missing");
            return JsonUtility.FromJson<LessonData>(asset.text);
        }

        public void Build(LessonData data, string voice, SeqProgram prog, bool isDefault, Func<string, bool> refKnown)
        {
            Data = data; Voice = voice; Steps.Clear(); fref = prog.P.Fref;
            double t = 0;
            foreach (var sd in data.steps)
            {
                var step = new LessonStep { Data = sd, EntryT = t };
                double tau = 0;
                for (int i = 0; i < sd.cues.Length; i++)
                {
                    var cd = sd.cues[i];
                    foreach (var r in cd.refs ?? new string[0]) if (refKnown != null && !refKnown(r)) throw new Exception($"Cue {cd.id}: unknown referent '{r}'");
                    var c = new LessonCue { Data = cd, Step = step.Data.step, Index = i };
                    bool generic = !isDefault && !string.IsNullOrEmpty(cd.generic);
                    c.Text = generic ? cd.generic : cd.text;
                    c.Clip = generic ? cd.clipGeneric : cd.clip;
                    c.Voice = generic ? (cd.durGeneric > 0 ? cd.durGeneric : Estimate(c.Text)) : (cd.dur > 0 ? cd.dur : Estimate(c.Text));
                    var sim = cd.sim;
                    if (sim != null && !string.IsNullOrEmpty(sim.cut))
                    {
                        c.Cut = true; c.Lead = CutLead; t = Resolve(prog, sim.cut); c.T0 = c.T1 = t; c.D = 0;
                    }
                    else if (sim != null && sim.span != null && sim.span.Length == 2)
                    {
                        c.Lab = sim.speed == "carrier";
                        double from = string.IsNullOrEmpty(sim.span[0]) ? t : Resolve(prog, sim.span[0]);
                        double to = Resolve(prog, sim.span[1]);
                        if (c.Lab) { from = Math.Ceiling(from * fref - 1e-6) / fref; to = Math.Max(to, from); to = Math.Round(to * fref) / fref; }
                        c.T0 = from; c.T1 = to; t = to;
                        c.D = sim.speed == "pulse" ? PulseSpeed : sim.speed == "relax" ? RelaxSpeed : sim.speed == "carrier" ? CarrierTurnSeconds * fref : throw new Exception("Unknown speed " + sim.speed);
                    }
                    else
                    {
                        if (sim != null && !string.IsNullOrEmpty(sim.hold)) t = Resolve(prog, sim.hold);
                        c.T0 = c.T1 = t; c.D = 0;
                    }
                    double span = c.D > 0 ? (c.T1 - c.T0) * c.D : 0;
                    c.Start = tau; c.Len = c.Lead + Math.Max(c.Voice, span) + CueGap;
                    tau += c.Len;
                    step.Cues.Add(c);
                }
                step.Len = tau + StepTail;
                if (!string.IsNullOrEmpty(sd.strobe)) BuildStrobe(step, prog, ref t);
                step.ExitT = t;
                Steps.Add(step);
            }
        }

        void BuildStrobe(LessonStep step, SeqProgram prog, ref double t)
        {
            var times = new List<double>(); var blocks = new List<int>();
            int n = prog.P.Matrix;
            if (step.Data.strobe == "rows")
            {
                var list = prog.SliceBlocks[0];
                for (int k = 2; k < list.Count; k++) { times.Add(prog.Blocks[list[k]].Echo); blocks.Add(list[k]); }
                var last = prog.Blocks[list[list.Count - 1]]; times.Add(last.Adc[last.Adc.Length - 1] + prog.P.Dwell); blocks.Add(last.Index);
                step.StrobeP = Math.Min(1.0, 22.0 / Math.Max(1, n - 2));
            }
            else
            {
                // Each slice at the echo of its last repetition: its slab is excited and its image is (all but one row) complete.
                for (int s = 1; s < prog.SliceBlocks.Count; s++) { var list = prog.SliceBlocks[s]; var b = prog.Blocks[list[list.Count - 1]]; times.Add(b.Echo); blocks.Add(b.Index); }
                var lastList = prog.SliceBlocks[prog.SliceBlocks.Count - 1]; var last = prog.Blocks[lastList[lastList.Count - 1]];
                times.Add(last.Adc[last.Adc.Length - 1] + prog.P.Dwell); blocks.Add(last.Index);
                // Fit every acquired slice before the concluding cue, including expanded full-hand stacks.
                step.StrobeP = Math.Min(1.5, step.Cues[step.Cues.Count - 1].Start / Math.Max(1, times.Count));
            }
            step.StrobeT = times.ToArray(); step.StrobeBlock = blocks.ToArray();
            foreach (var c in step.Cues) { c.T0 = c.T1 = times[0]; c.D = 0; }
            step.Len = Math.Max(step.Len, times.Count * step.StrobeP + StepTail);
            t = times[times.Count - 1];
        }

        public static double Estimate(string text) => 0.9 + 0.33 * text.Split(' ').Length;

        public double Resolve(SeqProgram prog, string expr)
        {
            expr = expr.Trim();
            if (expr == "0") return 0;
            int plus = expr.LastIndexOf('+');
            if (plus > 0)
            {
                double baseT = Resolve(prog, expr.Substring(0, plus)); string off = expr.Substring(plus + 1);
                if (off.EndsWith("ms")) return baseT + double.Parse(off.Substring(0, off.Length - 2), CultureInfo.InvariantCulture) * 1e-3;
                if (off.EndsWith("us")) return baseT + double.Parse(off.Substring(0, off.Length - 2), CultureInfo.InvariantCulture) * 1e-6;
                if (off.EndsWith("c")) { double cyc = double.Parse(off.Substring(0, off.Length - 1), CultureInfo.InvariantCulture); return (Math.Ceiling(baseT * fref - 1e-6) + cyc) / fref; }
                throw new Exception("Bad time offset " + expr);
            }
            int at = expr.IndexOf('@');
            if (at > 0)
            {
                string seg = expr.Substring(0, at); double f = double.Parse(expr.Substring(at + 1), CultureInfo.InvariantCulture);
                double a = prog.Time(seg + ".start"), b = prog.Time(seg + ".end");
                return a + f * (b - a);
            }
            return prog.Time(expr);
        }

        /// <summary>Cue active at lesson time tau within a step (the last cue whose start has passed).</summary>
        public int CueAt(LessonStep s, double tau)
        {
            int k = 0;
            for (int i = 0; i < s.Cues.Count; i++) if (tau >= s.Cues[i].Start) k = i;
            return k;
        }

        /// <summary>Physical time and frame at lesson time tau of a step.</summary>
        public double PhysicalAt(LessonStep s, double tau, out LessonCue cue, out bool lab)
        {
            cue = s.Cues[CueAt(s, tau)]; lab = false;
            if (s.StrobeT != null)
            {
                int f = Math.Max(0, Math.Min(s.StrobeT.Length - 1, (int)Math.Floor(tau / s.StrobeP)));
                return s.StrobeT[f];
            }
            double local = tau - cue.Start;
            if (cue.Cut && local < 0.15) return cue.Index > 0 ? s.Cues[cue.Index - 1].T1 : s.EntryT;
            if (cue.D <= 0) return cue.T1;
            lab = cue.Lab;
            double run = Math.Max(0, local - cue.Lead);
            return Math.Min(cue.T1, cue.T0 + run / cue.D);
        }
    }
}
