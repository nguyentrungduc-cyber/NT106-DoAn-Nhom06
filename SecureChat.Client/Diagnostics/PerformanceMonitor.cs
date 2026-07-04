using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace SecureChat.Client.Diagnostics
{
    /// <summary>
    /// Lightweight performance instrumentation for the scroll/paint pipeline.
    /// All measurements are Debug-only and have zero impact in Release builds.
    /// </summary>
    public static class PerformanceMonitor
    {
        static PerformanceMonitor()
        {
            // Auto-enable in DEBUG builds
            Enable();
        }

        private sealed class Metric
        {
            public long TotalTicks;
            public long Count;
            public long MaxTicks;
            public long MinTicks = long.MaxValue;
        }

        private static readonly ConcurrentDictionary<string, Metric> _metrics = new();
        private static readonly Stopwatch _sw = new();
        private static long _frameCount;
        private static long _lastReportTicks;
        private static long _totalPaintTimeTicks;
        private static long _totalLayoutTimeTicks;
        private static long _totalScrollTimeTicks;
        private static long _wmPaintCount;
        private static long _wmMouseWheelCount;
        private static long _wmVScrollCount;
        private static long _invalidates;
        private static long _gdiAllocs;
        private static int _enabled;

        private static readonly string[] _reportOrder =
        {
            "WM_MOUSEWHEEL", "WM_VSCROLL", "WM_PAINT", "WM_ERASEBKGND",
            "OnPaintBackground", "BubbleOnPaint", "DateSeparatorPaint",
            "Layout.Suspend", "Layout.Resume", "Layout.Perform",
            "Controls.Clear", "Controls.Add", "Controls.Remove",
            "BuildMessages", "AppendMessageBubble", "RemoveMessageBubble", "ReplaceMessageBubble",
            "AutoScroll.Relocate", "AutoScroll.Invalidate",
            "Alloc.Font", "Alloc.Brush", "Alloc.GraphicsPath", "Alloc.StringFormat",
            "TextRenderer.DrawText", "Graphics.DrawString",
            "MeasureString", "MeasureText",
            "ChatPanel.WndProc", "WndProc.Scroll",
            "Invalidate", "Invalidate (tracked)",
            "UpdateCachedBackground",
            "BeginInvoke",
        };

        [Conditional("DEBUG")]
        public static void Enable() => Interlocked.Exchange(ref _enabled, 1);

        [Conditional("DEBUG")]
        public static void Disable() => Interlocked.Exchange(ref _enabled, 0);

        public static bool IsEnabled => _enabled != 0;

        [Conditional("DEBUG")]
        public static void BeginFrame()
        {
            if (_enabled == 0) return;
            _sw.Restart();
        }

        [Conditional("DEBUG")]
        public static void EndFrame()
        {
            if (_enabled == 0) return;
            _sw.Stop();
            _frameCount++;

            // Report every 60 frames (~1 second at 60fps)
            if (_frameCount % 60 == 0)
                Report();
        }

        [Conditional("DEBUG")]
        public static void Record(string stage, long elapsedTicks = -1)
        {
            if (_enabled == 0) return;

            if (elapsedTicks < 0)
                elapsedTicks = _sw.ElapsedTicks;

            var metric = _metrics.GetOrAdd(stage, _ => new Metric());
            Interlocked.Add(ref metric.TotalTicks, elapsedTicks);
            Interlocked.Increment(ref metric.Count);

            // Update max/min (approximate, not lock-free perfect)
            long current;
            do
            {
                current = metric.MaxTicks;
                if (elapsedTicks <= current) break;
            }
            while (Interlocked.CompareExchange(ref metric.MaxTicks, elapsedTicks, current) != current);

            do
            {
                current = metric.MinTicks;
                if (elapsedTicks >= current) break;
            }
            while (Interlocked.CompareExchange(ref metric.MinTicks, elapsedTicks, current) != current);
        }

        [Conditional("DEBUG")]
        public static void RecordGDIFont() => Interlocked.Increment(ref _gdiAllocs);

        [Conditional("DEBUG")]
        public static void RecordInvalidate() => Interlocked.Increment(ref _invalidates);

        [Conditional("DEBUG")]
        public static void IncrementPaintCount() => Interlocked.Increment(ref _wmPaintCount);

        [Conditional("DEBUG")]
        public static void IncrementWheelCount() => Interlocked.Increment(ref _wmMouseWheelCount);

        [Conditional("DEBUG")]
        public static void IncrementVScrollCount() => Interlocked.Increment(ref _wmVScrollCount);

        [Conditional("DEBUG")]
        public static void Report()
        {
            if (_enabled == 0) return;

            var now = Stopwatch.GetTimestamp();
            var elapsedSec = (now - _lastReportTicks) / (double)Stopwatch.Frequency;
            if (elapsedSec < 0.5) return;
            _lastReportTicks = now;

            var freq = Stopwatch.Frequency;

            Debug.WriteLine("");
            Debug.WriteLine("╔══════════════════════════════════════════════════════════╗");
            Debug.WriteLine("║        CHAT PERFORMANCE MONITOR REPORT                  ║");
            Debug.WriteLine("╚══════════════════════════════════════════════════════════╝");
            Debug.WriteLine($"  Frames: {_frameCount}  |  Time: {elapsedSec:F2}s  |  Wheel: {_wmMouseWheelCount}  |  VScroll: {_wmVScrollCount}  |  WM_PAINT: {_wmPaintCount}");
            Debug.WriteLine($"  Invalidates: {_invalidates}  |  GDI Allocs: {_gdiAllocs}");
            Debug.WriteLine("");

            double totalMs = 0;
            foreach (var stage in _reportOrder)
            {
                if (_metrics.TryGetValue(stage, out var m) && m.Count > 0)
                {
                    double avgMs = (m.TotalTicks / (double)freq) * 1000.0 / m.Count;
                    double totalStageMs = (m.TotalTicks / (double)freq) * 1000.0;
                    totalMs += totalStageMs;
                    Debug.WriteLine($"  {stage,-28}  count={m.Count,-6}  avg={avgMs,8:F4}ms  total={totalStageMs,8:F4}ms  max={(m.MaxTicks / (double)freq) * 1000.0,8:F4}ms  min={(m.MinTicks / (double)freq) * 1000.0,8:F4}ms");
                }
            }

            Debug.WriteLine("");
            Debug.WriteLine($"  Total measured time: {totalMs:F2}ms across {_metrics.Sum(m => m.Value.Count)} calls");
            Debug.WriteLine("──────────────────────────────────────────────────────────");
        }

        [Conditional("DEBUG")]
        public static void Reset()
        {
            _metrics.Clear();
            _frameCount = 0;
            _wmPaintCount = 0;
            _wmMouseWheelCount = 0;
            _wmVScrollCount = 0;
            _invalidates = 0;
            _gdiAllocs = 0;
            _lastReportTicks = Stopwatch.GetTimestamp();
        }
    }
}
