using System;
using System.IO;
using UnityEngine;

namespace Nebulytic.Resonance
{
    /// <summary>
    /// Device performance settings and frame-time evidence.
    /// On the headset: dynamic fixed-foveated rendering (high) and sustained-high CPU/GPU levels.
    /// Every frame: CPU and GPU frame times (FrameTimingManager; the Meta runtime's app GPU time when Unity reports none),
    /// smoothed over half a second for the optional strip readout, and appended as 30 s means to
    /// persistentDataPath/frame-timings.csv.
    /// </summary>
    public sealed class Performance : MonoBehaviour
    {
        readonly FrameTiming[] samples = new FrameTiming[1]; double cpuSum, gpuSum; int frames; float next, windowStart; int windowFrames;
        double wCpu, wGpu; int wN;
        /// <summary>Smoothed CPU and GPU frame time (ms) and frame rate over the last half second; GPU is 0 when unknown.</summary>
        public float CpuMs { get; private set; }
        public float GpuMs { get; private set; }
        public float Fps { get; private set; }

        void Start()
        {
            if (Application.isEditor || Application.platform != RuntimePlatform.Android) return;
            try
            {
                OVRManager.foveatedRenderingLevel = OVRManager.FoveatedRenderingLevel.High;
                OVRManager.useDynamicFoveatedRendering = true;
                OVRManager.suggestedCpuPerfLevel = OVRManager.ProcessorPerformanceLevel.SustainedHigh;
                OVRManager.suggestedGpuPerfLevel = OVRManager.ProcessorPerformanceLevel.SustainedHigh;
            }
            catch (Exception e) { Debug.LogWarning("Performance settings unavailable: " + e.Message); }
        }

        void Update()
        {
            FrameTimingManager.CaptureFrameTimings();
            double cpu = 0, gpu = 0;
            if (FrameTimingManager.GetLatestTimings(1, samples) > 0) { cpu = samples[0].cpuFrameTime; gpu = samples[0].gpuFrameTime; }
            if (gpu <= 0 && !Application.isEditor && Application.platform == RuntimePlatform.Android)
            {
                try { var g = OVRPlugin.GetPerfMetricsFloat(OVRPlugin.PerfMetrics.App_GpuTime_Float); if (g.HasValue && g.Value > 0) gpu = g.Value < 1 ? g.Value * 1000 : g.Value; } catch (Exception) { }
            }
            if (cpu <= 0) cpu = Time.unscaledDeltaTime * 1000;
            cpuSum += cpu; gpuSum += gpu; frames++;
            wCpu += cpu; wGpu += gpu; wN++; windowFrames++;
            float now = Time.unscaledTime;
            if (now - windowStart >= 0.5f)
            {
                CpuMs = (float)(wCpu / Math.Max(1, wN)); GpuMs = (float)(wGpu / Math.Max(1, wN)); Fps = windowFrames / Math.Max(1e-3f, now - windowStart);
                wCpu = wGpu = 0; wN = 0; windowFrames = 0; windowStart = now;
            }
            if (now > next)
            {
                next = now + 30;
                if (frames > 0)
                {
                    var line = DateTime.UtcNow.ToString("O") + "," + Application.version + "," + SystemInfo.graphicsDeviceName + "," + frames + "," + cpuSum / frames + "," + gpuSum / frames + "\n";
                    try { File.AppendAllText(Path.Combine(Application.persistentDataPath, "frame-timings.csv"), line); } catch (IOException) { }
                    cpuSum = gpuSum = 0; frames = 0;
                }
            }
        }
    }
}
