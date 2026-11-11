// SummitAnalytics.cs
//
// Drop-in Unity MonoBehaviour for sending analytics events to a Summit server.
//
// Usage:
//   1. Attach this script to a persistent GameObject in your scene.
//   2. Set apiUrl and apiKey in the Inspector (or via code).
//   3. Call Identify(), SendHardwareProfile(), StartSession(), Track(), EndSession().
//
// See the accompanying README.md for a full integration guide.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace _project.Scripts.Core
{
    public class SummitAnalytics : MonoBehaviour
    {
        // ─── Configuration ───────────────────────────────────────────────

        [Header("Summit Server")] [Tooltip("Base URL of the Summit dashboard (no trailing slash)")]
        public string apiUrl = "https://your-summit-domain.com";

        [Tooltip("App API key sent via x-api-key header")] [PasswordField]
        public string apiKey = "";

        [Tooltip("Allows real Summit requests while running in the Unity Editor. Disabled by default to protect production data.")]
        public bool enableEditorNetworking;

        [Header("Flush Settings")] [Tooltip("Auto-flush interval in seconds")]
        public float flushInterval = 30f;

        [Tooltip("Max queued events before auto-flush")]
        public int flushThreshold = 20;

        [Tooltip("Upload timeout in seconds")] public float uploadTimeout = 10f;

        [Tooltip("Max events held while Summit is unreachable or has not issued a session id; the oldest are dropped past this")]
        public int maxQueuedEvents = 2000;

        [Header("Performance Tracking")] [Tooltip("Automatically collect FPS and memory samples")]
        public bool autoCollectPerformance = true;

        [Tooltip("Performance sample interval in seconds")]
        public float perfSampleInterval = 10f;

        [Tooltip("Max performance samples held back waiting for an event to travel with")]
        public int maxRetainedPerfSamples = 360;

        // ─── Internal state ──────────────────────────────────────────────

        // Summit's /api/ingest accepts at most 100 events and 1000 performance
        // samples per request and answers a larger batch with 400.
        private const int MaxEventsPerBatch = 100;
        private const int MaxPerfSamplesPerBatch = 1000;

        // Retry backoff for session/start and flushes: 2, 4, 8 ... 60 seconds.
        private const float MinRetryDelay = 2f;
        private const float MaxRetryDelay = 60f;

        private readonly List<QueuedEvent> _eventQueue = new();
        private int _flushFailures;
        private int _frameTimeIndex;
        private readonly float[] _frameTimes = new float[60];
        private bool _isFlushing;
        private float _lastFlushTime;
        private float _lastPerfSampleTime;
        private float _nextFlushAllowedTime;
        private readonly List<PerfSample> _perfQueue = new();
        private int _sessionAttempt;

        // Session ids of ended sessions, by attempt, while their events are
        // still queued. Null when the session ended before Summit issued one.
        private readonly Dictionary<int, string> _endedSessionIds = new();

        // ─── Public state ────────────────────────────────────────────────

        private string SessionId { get; set; }
        public string UserId { get; private set; }
        public bool IsSessionActive { get; private set; }

        // ─── Unity lifecycle ─────────────────────────────────────────────

        private void Start()
        {
            _lastFlushTime = Time.realtimeSinceStartup;
            _lastPerfSampleTime = Time.realtimeSinceStartup;
        }

        private void Update()
        {
            // Frozen frames during a modal pause (first-clog tutorial) aren't gameplay —
            // keep them out of the FPS window and perf samples. Flushing below continues:
            // uploading queued events during a pause is fine.
            if (!GameSpeed.IsPaused)
            {
                // Track frame times for FPS calculation
                _frameTimes[_frameTimeIndex % _frameTimes.Length] = Time.unscaledDeltaTime;
                _frameTimeIndex++;

                // Auto-collect performance
                if (autoCollectPerformance && IsSessionActive &&
                    Time.realtimeSinceStartup - _lastPerfSampleTime >= perfSampleInterval)
                {
                    CollectPerformanceSample();
                    _lastPerfSampleTime = Time.realtimeSinceStartup;
                }
            }

            // Auto-flush, paused while backing off after a failed send
            if (IsSessionActive && !_isFlushing &&
                Time.realtimeSinceStartup >= _nextFlushAllowedTime &&
                (_eventQueue.Count >= flushThreshold ||
                 Time.realtimeSinceStartup - _lastFlushTime >= flushInterval))
                Flush();
        }

        // Best effort before the app may be suspended. This sends one batch of
        // at most 100 events, not the whole queue; the rest waits for Update
        // after the app resumes and is lost if it never does.
        private void OnApplicationPause(bool paused)
        {
            if (paused && IsSessionActive) Flush();
        }

        private void OnApplicationQuit()
        {
            if (IsSessionActive) EndSession();
        }

        /// <summary>
        ///     Fired when Summit returns a session ID. Use this to link Trailhead recordings.
        /// </summary>
        public event Action<string> OnSessionStarted;

        // ─── Public API ──────────────────────────────────────────────────

        /// <summary>
        ///     Set the user ID for this player. Call before StartSession.
        /// </summary>
        public void Identify(string userId)
        {
            UserId = userId;
        }

        /// <summary>
        ///     Send a durable hardware profile for this user. Call after Identify.
        ///     Summit stores this separately from sessions, so machine specs remain
        ///     available even before a replay or session has been linked.
        /// </summary>
        public void SendHardwareProfile(Dictionary<string, object> metadata = null,
            Dictionary<string, string> deviceInfo = null)
        {
            if (string.IsNullOrEmpty(UserId))
            {
                Debug.LogWarning("[SummitAnalytics] SendHardwareProfile requires Identify(userId) first.");
                return;
            }

#if !UNITY_EDITOR
        StartCoroutine(SendHardwareProfileCoroutine(metadata, deviceInfo));
#else
            if (enableEditorNetworking)
                StartCoroutine(SendHardwareProfileCoroutine(metadata, deviceInfo));
            else
                Debug.Log("[SummitAnalytics] Editor mode — hardware profile upload skipped.");
#endif
        }

        /// <summary>
        ///     Begin a new analytics session. Sends a request to the server
        ///     and receives a session_id.
        /// </summary>
        public void StartSession(string appVersion = null, Dictionary<string, string> deviceInfo = null,
            Dictionary<string, object> metadata = null)
        {
            if (IsSessionActive)
            {
                Debug.LogWarning("[SummitAnalytics] Session already active. Call EndSession first.");
                return;
            }

            // A previous session's id may still be set while its session/end is
            // in flight. Clear it so Flush holds this session's events until
            // Summit issues a new one.
            SessionId = null;
            IsSessionActive = true;
            _sessionAttempt++;

#if !UNITY_EDITOR
        StartCoroutine(StartSessionCoroutine(appVersion, deviceInfo, metadata, _sessionAttempt));
#else
            if (enableEditorNetworking)
                StartCoroutine(StartSessionCoroutine(appVersion, deviceInfo, metadata, _sessionAttempt));
            else
            {
                SessionId = Guid.NewGuid().ToString();
                Debug.Log($"[SummitAnalytics] Editor mode — fake session ID: {SessionId}");
                OnSessionStarted?.Invoke(SessionId);
            }
#endif
        }

        /// <summary>
        ///     Track an event with optional properties, numeric value, and tags.
        ///     Tags are free-form labels (e.g. "tutorial", "boss_fight") that Summit
        ///     groups and filters by — pass whatever is meaningful for your game.
        /// </summary>
        public void Track(string eventName, Dictionary<string, object> properties = null, float? numericValue = null,
            string[] tags = null, Dictionary<string, double> measurements = null)
        {
            if (!IsSessionActive) return;

            _eventQueue.Add(new QueuedEvent
            {
                Session = _sessionAttempt,
                Name = eventName,
                Timestamp = DateTime.UtcNow.ToString("o"),
                Properties = properties ?? new Dictionary<string, object>(),
                NumericValue = numericValue,
                Tags = tags,
                Measurements = measurements
            });
            TrimEventQueue();
        }

        /// <summary>
        ///     Record any number of numeric measurements on one event. Names are not
        ///     registered with Summit: a new stat is available as soon as a client
        ///     sends it. Dimensions can contain any JSON-compatible properties.
        /// </summary>
        private void RecordStats(string eventName, Dictionary<string, double> measurements,
            Dictionary<string, object> dimensions = null, string[] tags = null)
        {
            if (measurements == null || measurements.Count == 0) return;
            Track(eventName, dimensions, null, tags, measurements);
        }

        /// <summary>Convenience overload for a single schema-free measurement.</summary>
        public void RecordStat(string eventName, string statName, double value,
            Dictionary<string, object> dimensions = null, string[] tags = null)
        {
            RecordStats(eventName, new Dictionary<string, double> { { statName, value } }, dimensions, tags);
        }

        /// <summary>
        ///     Send the oldest queued events to the server immediately, at most
        ///     one ingest batch while the session runs and every batch once it has
        ///     ended.
        /// </summary>
        private void Flush()
        {
            if (_eventQueue.Count == 0 && _perfQueue.Count == 0) return;
            if (_isFlushing) return;

            // The ingest API requires a non-empty events array and answers a
            // performance-only batch with 400 "Array must contain at least 1
            // element(s)". Because the queues were cleared before the request,
            // every idle flush silently destroyed its samples. Hold them back
            // until an event can carry them instead.
            if (_eventQueue.Count == 0)
            {
                TrimRetainedPerfSamples();
                _lastFlushTime = Time.realtimeSinceStartup;
                return;
            }

            // Hold the running session's events until Summit has issued its id.
            // Summit rejects an id it never issued (foreign keys), and events sent
            // with no id are never attributed to the session. Events left over
            // from an ended session ahead of them still go out. EndSession clears
            // IsSessionActive first, so a session that never got an id still
            // sends its events, unattributed, instead of discarding them.
            if (IsWaitingForSessionId(_eventQueue[0].Session))
            {
                TrimEventQueue();
                TrimRetainedPerfSamples();
                // Keep Update from calling Flush every frame while the queue is
                // over flushThreshold. A new session id lifts this right away.
                _lastFlushTime = Time.realtimeSinceStartup;
                _nextFlushAllowedTime = Mathf.Max(_nextFlushAllowedTime, Time.realtimeSinceStartup + MinRetryDelay);
                return;
            }

#if UNITY_EDITOR
            if (!enableEditorNetworking)
            {
                Debug.Log($"[SummitAnalytics] Editor flush: {_eventQueue.Count} events, {_perfQueue.Count} perf samples");
                _eventQueue.Clear();
                _perfQueue.Clear();
                PruneEndedSessionIds();
                _lastFlushTime = Time.realtimeSinceStartup;
                return;
            }
#endif

            _lastFlushTime = Time.realtimeSinceStartup;
            StartCoroutine(FlushCoroutine());
        }

        /// <summary>True while the given session is running and Summit has not issued its id.</summary>
        private bool IsWaitingForSessionId(int session) =>
            IsSessionActive && session == _sessionAttempt && SessionId == null;

        /// <summary>True when the given session has ended, so no later Update flush will send its events.</summary>
        private bool HasEnded(int session) => !IsSessionActive || session != _sessionAttempt;

        /// <summary>The id Summit issued for the given session, or null if it never issued one.</summary>
        private string SessionIdFor(int session)
        {
            if (_endedSessionIds.TryGetValue(session, out var endedId)) return endedId;
            return session == _sessionAttempt ? SessionId : null;
        }

        /// <summary>Forgets ended session ids once none of their events or samples are queued.</summary>
        private void PruneEndedSessionIds()
        {
            if (_endedSessionIds.Count == 0) return;
            var oldest = int.MaxValue;
            if (_eventQueue.Count > 0) oldest = _eventQueue[0].Session;
            if (_perfQueue.Count > 0) oldest = Mathf.Min(oldest, _perfQueue[0].Session);

            var stale = new List<int>();
            foreach (var session in _endedSessionIds.Keys)
                if (session < oldest) stale.Add(session);
            foreach (var session in stale) _endedSessionIds.Remove(session);
        }

        /// <summary>
        ///     Removes the oldest events and samples that fit in one ingest request,
        ///     stopping at any that still wait for their session id. Each item keeps
        ///     its own session, so a batch can carry the end of one session and the
        ///     start of the next.
        /// </summary>
        private void TakeNextBatch(out List<QueuedEvent> events, out List<PerfSample> perfs)
        {
            var eventCount = 0;
            while (eventCount < _eventQueue.Count && eventCount < MaxEventsPerBatch &&
                   !IsWaitingForSessionId(_eventQueue[eventCount].Session))
                eventCount++;
            events = _eventQueue.GetRange(0, eventCount);
            _eventQueue.RemoveRange(0, eventCount);

            var perfCount = 0;
            while (perfCount < _perfQueue.Count && perfCount < MaxPerfSamplesPerBatch &&
                   !IsWaitingForSessionId(_perfQueue[perfCount].Session))
                perfCount++;
            perfs = _perfQueue.GetRange(0, perfCount);
            _perfQueue.RemoveRange(0, perfCount);
        }

        /// <summary>
        ///     Caps the event backlog while Summit is unreachable or has not issued
        ///     a session id yet, dropping the oldest first.
        /// </summary>
        private void TrimEventQueue()
        {
            var excess = _eventQueue.Count - Mathf.Max(1, maxQueuedEvents);
            if (excess > 0) _eventQueue.RemoveRange(0, excess);
        }

        /// <summary>
        ///     Caps the retained sample backlog for sessions that idle for a long
        ///     time without producing an event, dropping the oldest first.
        /// </summary>
        private void TrimRetainedPerfSamples()
        {
            var excess = _perfQueue.Count - Mathf.Max(1, maxRetainedPerfSamples);
            if (excess > 0) _perfQueue.RemoveRange(0, excess);
        }

        /// <summary>
        ///     End the current session. Flushes remaining events first.
        /// </summary>
        public void EndSession(Dictionary<string, object> finalMetadata = null)
        {
            if (!IsSessionActive) return;

            // Clear the flag before flushing: Flush then sends events even if
            // Summit never issued a session id, and FlushCoroutine keeps draining
            // batches because no later Update will flush them.
            IsSessionActive = false;
            _endedSessionIds[_sessionAttempt] = SessionId;
            Flush();

            var sessionId = SessionId;
            if (sessionId == null)
            {
                Debug.LogWarning("[SummitAnalytics] Session ended before Summit issued a session id; session/end skipped.");
                return;
            }

#if !UNITY_EDITOR
        StartCoroutine(EndSessionCoroutine(sessionId, finalMetadata));
#else
            if (enableEditorNetworking)
                StartCoroutine(EndSessionCoroutine(sessionId, finalMetadata));
            else
            {
                ReleaseSessionId(sessionId);
                Debug.Log("[SummitAnalytics] Editor mode — session ended.");
            }
#endif
        }

        // ─── Performance collection ──────────────────────────────────────

        private void CollectPerformanceSample()
        {
            var totalTime = 0f;
            var count = Mathf.Min(_frameTimeIndex, _frameTimes.Length);
            for (var i = 0; i < count; i++)
                totalTime += _frameTimes[i];

            var avgFps = count > 0 ? count / totalTime : 0f;

            _perfQueue.Add(new PerfSample
            {
                Session = _sessionAttempt,
                Timestamp = DateTime.UtcNow.ToString("o"),
                FPS = avgFps,
                MemoryMb = (float)GC.GetTotalMemory(false) / (1024 * 1024)
            });
        }

        // ─── Network coroutines ──────────────────────────────────────────

        private IEnumerator SendHardwareProfileCoroutine(Dictionary<string, object> metadata,
            Dictionary<string, string> deviceInfo)
        {
            var sb = new StringBuilder(1024);
            sb.Append('{');

            sb.Append("\"user_id\":");
            AppendJsonString(sb, UserId);
            sb.Append(",\"game_title\":");
            AppendJsonString(sb, Application.productName);
            sb.Append(",\"app_version\":");
            AppendJsonString(sb, Application.version);
            sb.Append(",\"reported_at\":");
            AppendJsonString(sb, DateTime.UtcNow.ToString("o"));

            sb.Append(",\"device\":{");
            sb.Append("\"platform\":");
            AppendJsonString(sb, Application.platform.ToString());
            sb.Append(",\"device_type\":");
            AppendJsonString(sb, SystemInfo.deviceType.ToString());
            sb.Append(",\"model\":");
            AppendJsonString(sb, SystemInfo.deviceModel);
            sb.Append(",\"device_name\":");
            AppendJsonString(sb, SystemInfo.deviceName);
            sb.Append(",\"os\":");
            AppendJsonString(sb, SystemInfo.operatingSystem);
            sb.Append(",\"gpu\":");
            AppendJsonString(sb, SystemInfo.graphicsDeviceName);
            sb.Append(",\"cpu\":");
            AppendJsonString(sb, SystemInfo.processorType);
            sb.Append(",\"ram_mb\":");
            sb.Append(SystemInfo.systemMemorySize);
            if (deviceInfo != null)
                foreach (var kvp in deviceInfo)
                {
                    sb.Append(',');
                    AppendJsonString(sb, kvp.Key);
                    sb.Append(':');
                    AppendJsonString(sb, kvp.Value);
                }

            sb.Append('}');

            if (metadata is { Count: > 0 })
            {
                sb.Append(",\"metadata\":");
                AppendObjectDict(sb, metadata);
            }

            sb.Append('}');

            var url = apiUrl.TrimEnd('/') + "/summit/api/ingest/hardware";
            var body = Encoding.UTF8.GetBytes(sb.ToString());

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("x-api-key", apiKey);
            request.timeout = Mathf.CeilToInt(uploadTimeout);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
                Debug.Log("[SummitAnalytics] Hardware profile sent.");
            else
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                var responseDetail = request.downloadHandler.text;
                if (responseDetail.Length > 1024)
                    responseDetail = responseDetail.Substring(0, 1024) + "…";
                Debug.LogWarning($"[SummitAnalytics] Hardware profile failed ({request.responseCode}): {request.error}. Response: {responseDetail}");
#else
                Debug.LogWarning($"[SummitAnalytics] Hardware profile failed ({request.responseCode}): {request.error}");
#endif
            }
        }

        private IEnumerator StartSessionCoroutine(string appVersion, Dictionary<string, string> deviceInfo,
            Dictionary<string, object> metadata, int attempt)
        {
            var sb = new StringBuilder(512);
            sb.Append('{');

            if (UserId != null)
            {
                sb.Append("\"user_id\":");
                AppendJsonString(sb, UserId);
                sb.Append(',');
            }

            if (appVersion != null)
            {
                sb.Append("\"app_version\":");
                AppendJsonString(sb, appVersion);
                sb.Append(',');
            }

            // Device info
            sb.Append("\"device\":{");
            sb.Append("\"platform\":");
            AppendJsonString(sb, Application.platform.ToString());
            sb.Append(",\"model\":");
            AppendJsonString(sb, SystemInfo.deviceModel);
            sb.Append(",\"os\":");
            AppendJsonString(sb, SystemInfo.operatingSystem);
            sb.Append(",\"gpu\":");
            AppendJsonString(sb, SystemInfo.graphicsDeviceName);
            sb.Append(",\"ram_mb\":");
            sb.Append(SystemInfo.systemMemorySize);
            if (deviceInfo != null)
                foreach (var kvp in deviceInfo)
                {
                    sb.Append(',');
                    AppendJsonString(sb, kvp.Key);
                    sb.Append(':');
                    AppendJsonString(sb, kvp.Value);
                }

            sb.Append('}');

            // Metadata
            if (metadata is { Count: > 0 })
            {
                sb.Append(",\"metadata\":");
                AppendObjectDict(sb, metadata);
            }

            sb.Append('}');

            var url = apiUrl.TrimEnd('/') + "/summit/api/ingest/session/start";
            var body = Encoding.UTF8.GetBytes(sb.ToString());
            var failures = 0;

            // Never make up a session id: Summit rejects any id it did not issue.
            // Retry with backoff until it answers; Flush holds events meanwhile.
            while (attempt == _sessionAttempt && IsSessionActive)
            {
                using (var request = new UnityWebRequest(url, "POST"))
                {
                    request.uploadHandler = new UploadHandlerRaw(body);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("x-api-key", apiKey);
                    request.timeout = Mathf.CeilToInt(uploadTimeout);

                    yield return request.SendWebRequest();

                    // EndSession or a newer StartSession ran while this was in flight.
                    if (attempt != _sessionAttempt || !IsSessionActive) yield break;

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var sessionId = ParseSessionId(request.downloadHandler.text);
                        if (sessionId != null)
                        {
                            SessionId = sessionId;
                            _nextFlushAllowedTime = 0f;
                            Debug.Log($"[SummitAnalytics] Session started: {SessionId}");
                            OnSessionStarted?.Invoke(SessionId);
                            yield break;
                        }

                        // A 2xx without a UUID id is not Summit's answer: a captive portal
                        // or school proxy that redirects to a login page returns 200 too.
                        // Treat it like a network failure and try again.
                        failures++;
                        Debug.LogWarning(
                            $"[SummitAnalytics] Session start response ({request.responseCode}) had no session id. Retrying in {RetryDelay(failures)}s.");
                    }
                    else if (!IsRetryableFailure(request.result, request.responseCode))
                    {
                        Debug.LogWarning(
                            $"[SummitAnalytics] Session start rejected ({request.responseCode}): {request.error}. Not retrying.");
                        yield break;
                    }
                    else
                    {
                        failures++;
                        Debug.LogWarning(
                            $"[SummitAnalytics] Session start failed ({request.responseCode}): {request.error}. Retrying in {RetryDelay(failures)}s.");
                    }
                }

                yield return new WaitForSecondsRealtime(RetryDelay(failures));
            }
        }

        private IEnumerator FlushCoroutine()
        {
            _isFlushing = true;
            TakeNextBatch(out var events, out var perfs);

            var sb = new StringBuilder(4096);
            sb.Append("{\"events\":[");
            for (var i = 0; i < events.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var e = events[i];
                sb.Append('{');
                sb.Append("\"name\":");
                AppendJsonString(sb, e.Name);
                sb.Append(",\"timestamp\":");
                AppendJsonString(sb, e.Timestamp);
                var eventSessionId = SessionIdFor(e.Session);
                if (eventSessionId != null)
                {
                    sb.Append(",\"session_id\":");
                    AppendJsonString(sb, eventSessionId);
                }

                if (UserId != null)
                {
                    sb.Append(",\"user_id\":");
                    AppendJsonString(sb, UserId);
                }

                sb.Append(",\"properties\":");
                AppendObjectDict(sb, e.Properties);
                if (e.NumericValue.HasValue)
                    AppendNumberField(sb, "numeric_value", e.NumericValue.Value);

                if (e.Tags is { Length: > 0 })
                {
                    sb.Append(",\"tags\":[");
                    for (var t = 0; t < e.Tags.Length; t++)
                    {
                        if (t > 0) sb.Append(',');
                        AppendJsonString(sb, e.Tags[t]);
                    }

                    sb.Append(']');
                }

                if (e.Measurements is { Count: > 0 })
                {
                    sb.Append(",\"measurements\":");
                    AppendNumberDict(sb, e.Measurements);
                }

                sb.Append('}');
            }

            sb.Append(']');

            if (perfs.Count > 0)
            {
                sb.Append(",\"performance_samples\":[");
                for (var i = 0; i < perfs.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    var p = perfs[i];
                    sb.Append('{');
                    sb.Append("\"timestamp\":");
                    AppendJsonString(sb, p.Timestamp);
                    var sampleSessionId = SessionIdFor(p.Session);
                    if (sampleSessionId != null)
                    {
                        sb.Append(",\"session_id\":");
                        AppendJsonString(sb, sampleSessionId);
                    }

                    AppendNumberField(sb, "fps", p.FPS);
                    AppendNumberField(sb, "memory_mb", p.MemoryMb);
                    sb.Append('}');
                }

                sb.Append(']');
            }

            sb.Append('}');

            var url = apiUrl.TrimEnd('/') + "/summit/api/ingest";
            var body = Encoding.UTF8.GetBytes(sb.ToString());

            using (var request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("x-api-key", apiKey);
                request.timeout = Mathf.CeilToInt(uploadTimeout);

                yield return request.SendWebRequest();

                // Summit answers a stored batch with a JSON object. A captive portal
                // or proxy login page also answers 2xx, with HTML, and the batch never
                // reached Summit, so retry it like a network failure.
                var delivered = request.result == UnityWebRequest.Result.Success &&
                                IsJsonObject(request.downloadHandler.text);

                if (delivered)
                {
                    Debug.Log($"[SummitAnalytics] Flushed {events.Count} events, {perfs.Count} perf samples");
                    _flushFailures = 0;
                    _nextFlushAllowedTime = 0f;
                }
                else if (request.result == UnityWebRequest.Result.Success ||
                         IsRetryableFailure(request.result, request.responseCode))
                {
                    _flushFailures++;
                    var delay = RetryDelay(_flushFailures);
                    _nextFlushAllowedTime = Time.realtimeSinceStartup + delay;
                    var reason = request.result == UnityWebRequest.Result.Success
                        ? "response was not JSON"
                        : request.error;
                    Debug.LogWarning(
                        $"[SummitAnalytics] Flush failed ({request.responseCode}): {reason}. Next attempt in at least {delay}s.");
                    // Re-queue failed events ahead of anything tracked since, within the caps
                    _eventQueue.InsertRange(0, events);
                    _perfQueue.InsertRange(0, perfs);
                    TrimEventQueue();
                    TrimRetainedPerfSamples();
                    _isFlushing = false;
                    yield break;
                }
                else
                {
                    // Summit will reject this batch every time (bad key, schema or
                    // size). Re-sending it would block every batch behind it.
                    Debug.LogWarning(
                        $"[SummitAnalytics] Flush rejected ({request.responseCode}): {request.error}. Dropped {events.Count} events, {perfs.Count} perf samples.");
                    _flushFailures = 0;
                    _nextFlushAllowedTime = 0f;
                }
            }

            _isFlushing = false;
            PruneEndedSessionIds();

            // While a session runs, Update sends its next batch. Nothing else
            // will send an ended session's events, even after a new session has
            // started, so keep draining those here.
            if (_eventQueue.Count > 0 && HasEnded(_eventQueue[0].Session))
                StartCoroutine(FlushCoroutine());
        }

        /// <summary>
        ///     True for failures worth sending again: no response (network error or
        ///     timeout), a server error, 408 or 429. Any other 4xx means Summit will
        ///     reject the same request every time.
        /// </summary>
        private static bool IsRetryableFailure(UnityWebRequest.Result result, long responseCode)
        {
            if (result == UnityWebRequest.Result.ConnectionError || responseCode == 0) return true;
            return responseCode >= 500 || responseCode == 408 || responseCode == 429;
        }

        /// <summary>Exponential backoff: 2, 4, 8 ... capped at 60 seconds.</summary>
        private static float RetryDelay(int failures)
        {
            return Mathf.Min(MaxRetryDelay, MinRetryDelay * Mathf.Pow(2f, Mathf.Max(0, failures - 1)));
        }

        private IEnumerator EndSessionCoroutine(string sessionId, Dictionary<string, object> finalMetadata)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"session_id\":");
            AppendJsonString(sb, sessionId);
            if (finalMetadata is { Count: > 0 })
            {
                sb.Append(",\"metadata\":");
                AppendObjectDict(sb, finalMetadata);
            }

            sb.Append('}');

            var url = apiUrl.TrimEnd('/') + "/summit/api/ingest/session/end";
            var body = Encoding.UTF8.GetBytes(sb.ToString());

            using (var request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("x-api-key", apiKey);
                request.timeout = Mathf.CeilToInt(uploadTimeout);

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                    Debug.Log($"[SummitAnalytics] Session ended: {sessionId}");
                else
                    Debug.LogWarning($"[SummitAnalytics] Session end failed: {request.error}");
            }

            ReleaseSessionId(sessionId);
        }

        /// <summary>
        ///     Clears the session id once its session has ended, unless a newer
        ///     session has replaced it in the meantime.
        /// </summary>
        private void ReleaseSessionId(string endedSessionId)
        {
            if (SessionId == endedSessionId) SessionId = null;
        }

        // ─── JSON helpers (no external deps) ─────────────────────────────

        private static void AppendJsonString(StringBuilder sb, string value)
        {
            sb.Append('"');
            if (value != null)
                foreach (var c in value)
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default: sb.Append(c); break;
                    }

            sb.Append('"');
        }

        private static void AppendObjectDict(StringBuilder sb, Dictionary<string, object> dict)
        {
            sb.Append('{');
            var first = true;
            foreach (var kvp in dict)
            {
                if (!first) sb.Append(',');
                first = false;
                AppendJsonString(sb, kvp.Key);
                sb.Append(':');
                AppendJsonValue(sb, kvp.Value);
            }

            sb.Append('}');
        }

        private static void AppendJsonValue(StringBuilder sb, object value)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }

            if (value is string text)
            {
                AppendJsonString(sb, text);
                return;
            }

            if (value is bool boolean)
            {
                sb.Append(boolean ? "true" : "false");
                return;
            }

            if (value is IDictionary dictionary)
            {
                sb.Append('{');
                var first = true;
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    AppendJsonString(sb, Convert.ToString(entry.Key, CultureInfo.InvariantCulture));
                    sb.Append(':');
                    AppendJsonValue(sb, entry.Value);
                }

                sb.Append('}');
                return;
            }

            if (value is IEnumerable enumerable)
            {
                sb.Append('[');
                var first = true;
                foreach (var item in enumerable)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    AppendJsonValue(sb, item);
                }

                sb.Append(']');
                return;
            }

            if (value is byte || value is sbyte || value is short || value is ushort ||
                value is int || value is uint || value is long || value is ulong ||
                value is float || value is double || value is decimal)
            {
                if ((value is float single && (float.IsNaN(single) || float.IsInfinity(single))) ||
                    (value is double number && (double.IsNaN(number) || double.IsInfinity(number))))
                {
                    sb.Append("null");
                    return;
                }

                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            AppendJsonString(sb, Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        /// <summary>
        ///     Appends ,"name":value in the invariant culture, so a comma-decimal
        ///     locale can't write invalid JSON such as 59,83. Summit takes these
        ///     fields as optional numbers, not nullable ones, so a NaN or infinite
        ///     value is left out rather than written as null.
        /// </summary>
        private static void AppendNumberField(StringBuilder sb, string name, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            sb.Append(",\"").Append(name).Append("\":");
            sb.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void AppendNumberDict(StringBuilder sb, Dictionary<string, double> dict)
        {
            sb.Append('{');
            var first = true;
            foreach (var kvp in dict)
            {
                if (double.IsNaN(kvp.Value) || double.IsInfinity(kvp.Value)) continue;
                if (!first) sb.Append(',');
                first = false;
                AppendJsonString(sb, kvp.Key);
                sb.Append(':');
                sb.Append(kvp.Value.ToString("R", CultureInfo.InvariantCulture));
            }

            sb.Append('}');
        }

        /// <summary>
        ///     The session id from a session/start response, or null unless it is
        ///     a UUID. ParseJsonValue takes the first "id" anywhere in the body, so
        ///     a captive portal page could otherwise produce a junk id.
        /// </summary>
        private static string ParseSessionId(string body)
        {
            if (!IsJsonObject(body)) return null;
            var id = ParseJsonValue(body, "id");
            return Guid.TryParse(id, out _) ? id : null;
        }

        /// <summary>True when the body looks like a JSON object, as Summit's answers are.</summary>
        private static bool IsJsonObject(string body)
        {
            if (string.IsNullOrEmpty(body)) return false;
            var text = body.Trim();
            return text.Length >= 2 && text[0] == '{' && text[text.Length - 1] == '}';
        }

        private static string ParseJsonValue(string json, string key)
        {
            var search = "\"" + key + "\"";
            var keyIndex = json.IndexOf(search, StringComparison.Ordinal);
            if (keyIndex < 0) return null;

            var colonIndex = json.IndexOf(':', keyIndex + search.Length);
            if (colonIndex < 0) return null;

            var openQuote = json.IndexOf('"', colonIndex + 1);
            if (openQuote < 0) return null;

            var closeQuote = json.IndexOf('"', openQuote + 1);
            if (closeQuote < 0) return null;

            return json.Substring(openQuote + 1, closeQuote - openQuote - 1);
        }

        // ─── Internal types ──────────────────────────────────────────────

        private class QueuedEvent
        {
            public Dictionary<string, double> Measurements;
            public int Session;
            public string Name;
            public float? NumericValue;
            public Dictionary<string, object> Properties;
            public string[] Tags;
            public string Timestamp;
        }

        private class PerfSample
        {
            public float FPS;
            public float MemoryMb;
            public int Session;
            public string Timestamp;
        }
    }
}
