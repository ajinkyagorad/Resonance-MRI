using System.Diagnostics;

namespace Nebulytic.Resonance
{
    /// <summary>
    /// Main-thread cost of each part of App.Update (ms, exponentially smoothed over about 30 frames), the spin worker's
    /// cost, and managed allocation per frame. Read by the validation harness (headless frame-cost report) and by the
    /// strip's optional frame-time readout. No allocation.
    /// </summary>
    public sealed class FrameCost
    {
        public double Clock, Sim, Attention, Scanner, Cube, Console, Strip, Kick, Total, Worker, WorkerHand;
        public double AllocBytes; public long Frames;
        long allocStart;
        const double A = 1.0 / 30;
        static readonly double TickMs = 1000.0 / Stopwatch.Frequency;

        public long Start() { allocStart = System.GC.GetAllocatedBytesForCurrentThread(); return Stopwatch.GetTimestamp(); }

        public long Lap(long since, ref double field)
        {
            long now = Stopwatch.GetTimestamp(); double ms = (now - since) * TickMs;
            field += (ms - field) * (Frames == 0 ? 1 : A); return now;
        }

        public void End(long start, double workerMs, double workerHandMs)
        {
            double ms = (Stopwatch.GetTimestamp() - start) * TickMs, a = Frames == 0 ? 1 : A;
            Total += (ms - Total) * a; Worker += (workerMs - Worker) * a; WorkerHand += (workerHandMs - WorkerHand) * a;
            AllocBytes += ((System.GC.GetAllocatedBytesForCurrentThread() - allocStart) - AllocBytes) * a;
            Frames++;
        }
    }
}
