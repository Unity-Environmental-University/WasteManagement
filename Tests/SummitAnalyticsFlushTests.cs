using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using _project.Scripts.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;

namespace _project.Scripts.Tests
{
    /// <summary>
    ///     Guards the ingest contract: Summit answers a batch whose events array is
    ///     empty with 400 "Array must contain at least 1 element(s)". Flush used to
    ///     clear both queues before sending, so every performance-only flush
    ///     destroyed its samples, and no web session ever reported a framerate.
    ///     Summit also rejects batches over 100 events, session ids it never
    ///     issued, and comma-decimal numbers; a rejected batch used to be
    ///     re-sent forever and block every batch behind it.
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

        [Test]
        public void Flush_WithoutASessionId_HoldsEventsUntilSummitIssuesOne()
        {
            ActivateWithoutSessionId();
            TrackEvents(3);

            Flush();

            Assert.AreEqual(3, EventQueue.Count,
                "Events must wait for a real session id instead of being sent under an invented one.");
        }

        [Test]
        public void Flush_WithoutASessionId_DoesNotRunEveryFrame()
        {
            ActivateWithoutSessionId();
            TrackEvents(_summit.flushThreshold + 5);
            SetField(_summit, "_lastFlushTime", 0f);
            SetField(_summit, "_nextFlushAllowedTime", 0f);

            Flush();

            Assert.Greater(GetField<float>(_summit, "_nextFlushAllowedTime"), Time.realtimeSinceStartup,
                "A held queue over flushThreshold must not make Update call Flush on every frame.");
            Assert.AreNotEqual(0f, GetField<float>(_summit, "_lastFlushTime"));
        }

        [Test]
        public void ReleaseSessionId_KeepsTheIdOfANewerSession()
        {
            SetSessionId("new-session");

            InvokeInstance("ReleaseSessionId", "old-session");

            Assert.AreEqual("new-session", GetSessionId(),
                "A late session/end must not wipe the id of the session that replaced it.");
        }

        [Test]
        public void ReleaseSessionId_ClearsTheEndedSessionsId()
        {
            SetSessionId("old-session");

            InvokeInstance("ReleaseSessionId", "old-session");

            Assert.IsNull(GetSessionId());
        }

        [Test]
        public void Track_PastTheQueueCap_DropsTheOldestEvents()
        {
            _summit.maxQueuedEvents = 5;
            ActivateWithoutSessionId();

            TrackEvents(8);

            Assert.AreEqual(5, EventQueue.Count, "An unreachable server must not grow the backlog without bound.");
            Assert.AreEqual("Event 3", EventName(EventQueue[0]), "The oldest events are dropped first.");
        }

        [Test]
        public void TakeNextBatch_StaysWithinSummitsPerRequestLimit()
        {
            ActivateWithoutSessionId();
            SetSessionId(OldSessionId);
            TrackEvents(150);
            QueuePerfSamples(3);

            var (events, perfs) = TakeNextBatch();

            Assert.AreEqual(100, events.Count, "Summit answers a batch over 100 events with 400.");
            Assert.AreEqual(3, perfs.Count);
            Assert.AreEqual(50, EventQueue.Count, "The rest waits for the next flush.");
            Assert.AreEqual("Event 100", EventName(EventQueue[0]));
        }

        [Test]
        public void TakeNextBatch_AfterARestart_SendsTheEndedSessionsEventsUnderItsOwnId()
        {
            ActivateWithoutSessionId();
            SetSessionId(OldSessionId);
            TrackEvents(150);
            EndWithoutFlushing();
            StartWithoutSessionId();
            _summit.Track("New session event");

            var (first, _) = TakeNextBatch();
            var (second, _) = TakeNextBatch();

            Assert.AreEqual(100, first.Count);
            Assert.AreEqual(50, second.Count, "The ended session's backlog drains without the new session's events.");
            Assert.AreEqual(OldSessionId, SessionIdFor(first[0]));
            Assert.AreEqual(OldSessionId, SessionIdFor(second[49]),
                "The ended session's events must not go out under the new session's id.");
            Assert.AreEqual(1, EventQueue.Count);
            Assert.AreEqual("New session event", EventName(EventQueue[0]),
                "The new session's events wait for its own id.");
            Assert.IsTrue((bool)InvokeInstance("IsWaitingForSessionId", EventSession(EventQueue[0])));
        }

        [Test]
        public void Flush_AfterARestart_HoldsOnlyOnceTheEndedBacklogIsGone()
        {
            ActivateWithoutSessionId();
            SetSessionId(OldSessionId);
            TrackEvents(2);
            EndWithoutFlushing();
            StartWithoutSessionId();
            _summit.Track("New session event");

            Assert.IsTrue((bool)InvokeInstance("HasEnded", EventSession(EventQueue[0])),
                "FlushCoroutine keeps draining while the queue starts with an ended session's events.");
            TakeNextBatch();
            Assert.IsFalse((bool)InvokeInstance("HasEnded", EventSession(EventQueue[0])));

            Flush();

            Assert.AreEqual(1, EventQueue.Count, "The new session's events are held until Summit issues its id.");
        }

        [TestCase(UnityWebRequest.Result.ConnectionError, 0L, true)]
        [TestCase(UnityWebRequest.Result.ProtocolError, 500L, true)]
        [TestCase(UnityWebRequest.Result.ProtocolError, 503L, true)]
        [TestCase(UnityWebRequest.Result.ProtocolError, 408L, true)]
        [TestCase(UnityWebRequest.Result.ProtocolError, 429L, true)]
        [TestCase(UnityWebRequest.Result.ProtocolError, 400L, false)]
        [TestCase(UnityWebRequest.Result.ProtocolError, 401L, false)]
        [TestCase(UnityWebRequest.Result.ProtocolError, 404L, false)]
        [TestCase(UnityWebRequest.Result.ProtocolError, 413L, false)]
        // The server answered; only the download handler failed. Re-sending
        // would duplicate a batch Summit may already have stored.
        [TestCase(UnityWebRequest.Result.DataProcessingError, 200L, false)]
        public void IsRetryableFailure_RetriesOnlyTransientFailures(UnityWebRequest.Result result, long responseCode,
            bool expected)
        {
            Assert.AreEqual(expected, (bool)InvokeStatic("IsRetryableFailure", result, responseCode));
        }

        [TestCase("{\"id\":\"3f2c8a52-6b1e-4c2f-9d7a-1b2c3d4e5f60\",\"started_at\":\"2026-11-10T00:00:00Z\"}",
            "3f2c8a52-6b1e-4c2f-9d7a-1b2c3d4e5f60")]
        [TestCase("{\"id\":\"login-form\"}", null)]
        [TestCase("<html><form id=\"login\" data-id=\"3f2c8a52-6b1e-4c2f-9d7a-1b2c3d4e5f60\"></form></html>", null)]
        [TestCase("", null)]
        [TestCase(null, null)]
        public void ParseSessionId_AcceptsOnlyAUuidFromAJsonObject(string body, string expected)
        {
            Assert.AreEqual(expected, (string)InvokeStatic("ParseSessionId", body));
        }

        [TestCase("{\"ingested\":100}", true)]
        [TestCase("  {\"ingested\":1}\n", true)]
        [TestCase("<!DOCTYPE html><html><body>Sign in to continue</body></html>", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsJsonObject_RejectsCaptivePortalPages(string body, bool expected)
        {
            Assert.AreEqual(expected, (bool)InvokeStatic("IsJsonObject", body));
        }

        [TestCase(1, 2f)]
        [TestCase(2, 4f)]
        [TestCase(3, 8f)]
        [TestCase(6, 60f)]
        [TestCase(1000, 60f)]
        public void RetryDelay_BacksOffExponentiallyUpToAMinute(int failures, float expected)
        {
            Assert.AreEqual(expected, (float)InvokeStatic("RetryDelay", failures), 0.001f);
        }

        [Test]
        public void AppendNumberField_WritesAPointUnderACommaDecimalLocale()
        {
            var previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
                var sb = new StringBuilder();

                InvokeStatic("AppendNumberField", sb, "fps", 59.5f);

                Assert.AreEqual(",\"fps\":59.5", sb.ToString(), "59,5 is invalid JSON.");
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void AppendNumberField_LeavesOutNonFiniteValues(float value)
        {
            var sb = new StringBuilder();

            InvokeStatic("AppendNumberField", sb, "fps", value);

            Assert.AreEqual(string.Empty, sb.ToString(), "Summit's schema has no null for these fields.");
        }

        // ─── Helpers ─────────────────────────────────────────────────────

        private const string OldSessionId = "3f2c8a52-6b1e-4c2f-9d7a-1b2c3d4e5f60";

        private IList PerfQueue => GetField<IList>(_summit, "_perfQueue");

        private IList EventQueue => GetField<IList>(_summit, "_eventQueue");

        /// <summary>A session that has started but has no id from Summit yet.</summary>
        private void ActivateWithoutSessionId()
        {
            var setter = typeof(SummitAnalytics).GetProperty("IsSessionActive")?.GetSetMethod(true);
            Assert.IsNotNull(setter);
            setter.Invoke(_summit, new object[] { true });
        }

        /// <summary>What EndSession records, without the Editor flush clearing the queue.</summary>
        private void EndWithoutFlushing()
        {
            var attempt = GetField<int>(_summit, "_sessionAttempt");
            GetField<IDictionary>(_summit, "_endedSessionIds")[attempt] = GetSessionId();
            SetActive(false);
        }

        /// <summary>What StartSession does before Summit answers.</summary>
        private void StartWithoutSessionId()
        {
            SetSessionId(null);
            SetActive(true);
            SetField(_summit, "_sessionAttempt", GetField<int>(_summit, "_sessionAttempt") + 1);
        }

        private void SetActive(bool active)
        {
            var setter = typeof(SummitAnalytics).GetProperty("IsSessionActive")?.GetSetMethod(true);
            Assert.IsNotNull(setter);
            setter.Invoke(_summit, new object[] { active });
        }

        private (IList events, IList perfs) TakeNextBatch()
        {
            var args = new object[2];
            InvokeInstance("TakeNextBatch", args);
            return ((IList)args[0], (IList)args[1]);
        }

        private string SessionIdFor(object queuedEvent) =>
            (string)InvokeInstance("SessionIdFor", EventSession(queuedEvent));

        private static int EventSession(object queuedEvent) =>
            (int)queuedEvent.GetType().GetField("Session").GetValue(queuedEvent);

        private void SetSessionId(string id)
        {
            var setter = typeof(SummitAnalytics)
                .GetProperty("SessionId", BindingFlags.Instance | BindingFlags.NonPublic)?.GetSetMethod(true);
            Assert.IsNotNull(setter);
            setter.Invoke(_summit, new object[] { id });
        }

        private string GetSessionId()
        {
            var property = typeof(SummitAnalytics)
                .GetProperty("SessionId", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(property);
            return (string)property.GetValue(_summit);
        }

        private object InvokeInstance(string name, params object[] args)
        {
            var method = typeof(SummitAnalytics).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"{name} moved; update this test.");
            return method.Invoke(_summit, args);
        }

        private void TrackEvents(int count)
        {
            for (var i = 0; i < count; i++)
                _summit.Track($"Event {i}");
        }

        private static string EventName(object queuedEvent) =>
            (string)queuedEvent.GetType().GetField("Name").GetValue(queuedEvent);

        private static object InvokeStatic(string name, params object[] args)
        {
            var method = typeof(SummitAnalytics).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"{name} moved; update this test.");
            return method.Invoke(null, args);
        }

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
