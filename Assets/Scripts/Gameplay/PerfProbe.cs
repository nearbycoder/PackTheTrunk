using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace PackTheTrunk
{
    /// <summary>
    /// Frame-time and allocation statistics for the self-test. The autopilot names the current
    /// phase; each phase reports average/percentile frame times, CPU and GPU time, GC allocations
    /// per frame and every hitch (a frame over 50 ms) with what was happening at the time.
    /// </summary>
    public class PerfProbe : MonoBehaviour
    {
        class Stats
        {
            public readonly List<float> Frames = new List<float>();
            public double Cpu, Gpu, Alloc;
            public int Timed, AllocFrames, Collections;
            public long MaxAlloc;
        }

        static PerfProbe instance;
        readonly Dictionary<string, Stats> phases = new Dictionary<string, Stats>();
        readonly List<string> order = new List<string>();
        readonly FrameTiming[] timing = new FrameTiming[1];
        ProfilerRecorder gcAlloc;
        string phase = "boot";
        int lastGcCount;
        int skip;

        public static void Begin(string name)
        {
            if (instance == null) return;
            instance.phase = name;
            instance.skip = 2;
        }

        /// <summary>Leave the next frames out (the self-test's own screenshots stall them).</summary>
        public static void Ignore()
        {
            if (instance != null) instance.skip = 2;
        }

        public static void Attach()
        {
            if (instance != null) return;
            instance = new GameObject("PerfProbe").AddComponent<PerfProbe>();
            DontDestroyOnLoad(instance.gameObject);
        }

        void OnEnable()
        {
            gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            lastGcCount = System.GC.CollectionCount(0);
        }

        void OnDisable() => gcAlloc.Dispose();

        void Update()
        {
            // Ignore the frame a phase starts on (it often carries the previous phase's work).
            if (skip-- > 0) return;
            if (!phases.TryGetValue(phase, out var s))
            {
                phases[phase] = s = new Stats();
                order.Add(phase);
            }
            float ms = Time.unscaledDeltaTime * 1000f;
            s.Frames.Add(ms);
            if (ms > 50f) Debug.Log($"[Perf] hitch {ms:0} ms in {phase} at frame {Time.frameCount}");

            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timing) > 0 && timing[0].cpuFrameTime > 0)
            {
                s.Cpu += timing[0].cpuMainThreadFrameTime;
                s.Gpu += timing[0].gpuFrameTime;
                s.Timed++;
            }
            if (gcAlloc.Valid)
            {
                long bytes = gcAlloc.LastValue;
                s.Alloc += bytes;
                s.AllocFrames++;
                if (bytes > s.MaxAlloc) s.MaxAlloc = bytes;
            }
            int gc = System.GC.CollectionCount(0);
            s.Collections += gc - lastGcCount;
            lastGcCount = gc;
        }

        public static void Report()
        {
            if (instance == null) return;
            var sb = new StringBuilder();
            sb.AppendLine("[Perf] phase                 frames   avg    p50    p95    p99    max  >33ms  cpu    gpu    alloc/frame  maxAlloc  GCs");
            foreach (var name in instance.order)
            {
                var s = instance.phases[name];
                if (s.Frames.Count == 0) continue;
                var sorted = new List<float>(s.Frames);
                sorted.Sort();
                float P(float q) => sorted[Mathf.Clamp(Mathf.RoundToInt(q * (sorted.Count - 1)), 0, sorted.Count - 1)];
                float avg = 0f;
                int slow = 0;
                foreach (var f in sorted) { avg += f; if (f > 33.4f) slow++; }
                avg /= sorted.Count;
                string cpu = s.Timed > 0 ? (s.Cpu / s.Timed).ToString("0.00") : "-";
                string gpu = s.Timed > 0 ? (s.Gpu / s.Timed).ToString("0.00") : "-";
                string alloc = s.AllocFrames > 0 ? (s.Alloc / s.AllocFrames / 1024.0).ToString("0.0") + " KB" : "-";
                sb.AppendLine($"[Perf] {name,-20} {sorted.Count,7} {avg,6:0.0} {P(0.5f),6:0.0} {P(0.95f),6:0.0} {P(0.99f),6:0.0} {sorted[sorted.Count - 1],6:0.0} {slow,5}  {cpu,-6} {gpu,-6} {alloc,-12} {s.MaxAlloc / 1024,6} KB {s.Collections,4}");
            }
            Debug.Log(sb.ToString());
            Debug.Log(MasterBus.Report());
        }
    }
}
