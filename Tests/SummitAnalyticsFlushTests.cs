using System;
using System.Collections;
using System.Reflection;
using _project.Scripts.Core;
using NUnit.Framework;

namespace _project.Scripts.Tests
{
    /// <summary>
    ///     Guards the ingest contract: Summit answers a batch whose events array is
    ///     empty with 400 "Array must contain at least 1 element(s)". Flush used to
    ///     clear both queues before sending, so every performance-only flush
    ///     destroyed its samples, and no web session ever reported a framerate.
    /// </summary>
    public class SummitAnalyticsFlushTests : PlayModeTestBase
    {
        private SummitAnalytics _summit;

        [SetUp]
        public void SetUp()
        {
            _summit = CreateGameObject("Summit").AddComponent<SummitAnalytics>();
        }

        [Test]
        public void Flush_PerfSamplesWithoutEvents_AreRetainedRatherThanDropped()
        {
            QueuePerfSamples(3);

            Flush();

            Assert.AreEqual(3, PerfQueue.Count,
                "Performance-only batches must be held back, not cleared into a request the server rejects.");
        }

        [Test]
        public void Flush_PerfSamplesWithoutEvents_DoNotSpinTheFlushTimer()
        {
            QueuePerfSamples(1);
            SetField(_summit, "_lastFlushTime", 0f);

            Flush();

            Assert.AreNotEqual(0f, GetField<float>(_summit, "_lastFlushTime"),
                "Retaining samples must still reset the flush clock, or Update retries every frame.");
        }

        [Test]
        public void Flush_RetainedSamples_AreTrimmedToTheConfiguredCap()
        {
            _summit.maxRetainedPerfSamples = 5;
            QueuePerfSamples(12);

            Flush();

            Assert.AreEqual(5, PerfQueue.Count, "A long idle session must not grow the backlog without bound.");
        }

        [Test]
        public void Flush_EmptyQueues_RemainsANoOp()
        {
            Flush();

            Assert.AreEqual(0, PerfQueue.Count);
        }

        // ─── Helpers ─────────────────────────────────────────────────────

        private IList PerfQueue => GetField<IList>(_summit, "_perfQueue");

        private void QueuePerfSamples(int count)
        {
            var sampleType = typeof(SummitAnalytics).GetNestedType("PerfSample", BindingFlags.NonPublic);
            Assert.IsNotNull(sampleType, "PerfSample type moved; update this test.");

            var queue = PerfQueue;
            for (var i = 0; i < count; i++)
            {
                var sample = Activator.CreateInstance(sampleType);
                sampleType.GetField("FPS").SetValue(sample, 60f + i);
                sampleType.GetField("MemoryMb").SetValue(sample, 4f);
                sampleType.GetField("Timestamp").SetValue(sample, "2026-08-17T00:00:00Z");
                queue.Add(sample);
            }
        }

        private void Flush()
        {
            var flush = typeof(SummitAnalytics).GetMethod("Flush", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(flush);
            flush.Invoke(_summit, null);
        }
    }
}
