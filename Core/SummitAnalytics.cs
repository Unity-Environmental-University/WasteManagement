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

        [Header("Performance Tracking")] [Tooltip("Automatically collect FPS and memory samples")]
        public bool autoCollectPerformance = true;

        [Tooltip("Performance sample interval in seconds")]
        public float perfSampleInterval = 10f;

        // ─── Internal state ──────────────────────────────────────────────

        private readonly List<QueuedEvent> _eventQueue = new();
        private int _frameTimeIndex;
        private readonly float[] _frameTimes = new float[60];
        private bool _isFlushing;
        private float _lastFlushTime;
        private float _lastPerfSampleTime;
        private readonly List<PerfSample> _perfQueue = new();

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

            // Auto-flush
            if (IsSessionActive && !_isFlushing &&
                (_eventQueue.Count >= flushThreshold ||
                 Time.realtimeSinceStartup - _lastFlushTime >= flushInterval))
                Flush();
        }

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

            IsSessionActive = true;

#if !UNITY_EDITOR
        StartCoroutine(StartSessionCoroutine(appVersion, deviceInfo, metadata));
#else
            if (enableEditorNetworking)
                StartCoroutine(StartSessionCoroutine(appVersion, deviceInfo, metadata));
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
                Name = eventName,
                Timestamp = DateTime.UtcNow.ToString("o"),
                Properties = properties ?? new Dictionary<string, object>(),
                NumericValue = numericValue,
                Tags = tags,
                Measurements = measurements
            });
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
        ///     Flush all queued events to the server immediately.
        /// </summary>
        private void Flush()
        {
            if (_eventQueue.Count == 0 && _perfQueue.Count == 0) return;
            if (_isFlushing) return;

#if !UNITY_EDITOR
        var events = new List<QueuedEvent>(_eventQueue);
        var perfs = new List<PerfSample>(_perfQueue);
        _eventQueue.Clear();
        _perfQueue.Clear();
        _lastFlushTime = Time.realtimeSinceStartup;
        StartCoroutine(FlushCoroutine(events, perfs));
#else
            if (enableEditorNetworking)
            {
                var events = new List<QueuedEvent>(_eventQueue);
                var perfs = new List<PerfSample>(_perfQueue);
                _eventQueue.Clear();
                _perfQueue.Clear();
                _lastFlushTime = Time.realtimeSinceStartup;
                StartCoroutine(FlushCoroutine(events, perfs));
            }
            else
            {
                Debug.Log($"[SummitAnalytics] Editor flush: {_eventQueue.Count} events, {_perfQueue.Count} perf samples");
                _eventQueue.Clear();
                _perfQueue.Clear();
                _lastFlushTime = Time.realtimeSinceStartup;
            }
#endif
        }

        /// <summary>
        ///     End the current session. Flushes remaining events first.
        /// </summary>
        public void EndSession(Dictionary<string, object> finalMetadata = null)
        {
            if (!IsSessionActive) return;

            Flush();
            IsSessionActive = false;

#if !UNITY_EDITOR
        StartCoroutine(EndSessionCoroutine(finalMetadata));
#else
            if (enableEditorNetworking)
                StartCoroutine(EndSessionCoroutine(finalMetadata));
            else
                Debug.Log("[SummitAnalytics] Editor mode — session ended.");
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
            Dictionary<string, object> metadata)
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

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("x-api-key", apiKey);
            request.timeout = Mathf.CeilToInt(uploadTimeout);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                SessionId = ParseJsonValue(request.downloadHandler.text, "id");
                Debug.Log($"[SummitAnalytics] Session started: {SessionId}");
                OnSessionStarted?.Invoke(SessionId);
            }
            else
            {
                Debug.LogWarning($"[SummitAnalytics] Session start failed: {request.error}");
                SessionId = Guid.NewGuid().ToString();
                OnSessionStarted?.Invoke(SessionId);
            }
        }

        private IEnumerator FlushCoroutine(List<QueuedEvent> events, List<PerfSample> perfs)
        {
            _isFlushing = true;

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
                if (SessionId != null)
                {
                    sb.Append(",\"session_id\":");
                    AppendJsonString(sb, SessionId);
                }

                if (UserId != null)
                {
                    sb.Append(",\"user_id\":");
                    AppendJsonString(sb, UserId);
                }

                sb.Append(",\"properties\":");
                AppendObjectDict(sb, e.Properties);
                if (e.NumericValue.HasValue)
                {
                    sb.Append(",\"numeric_value\":");
                    sb.Append(e.NumericValue.Value.ToString("G"));
                }

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
                    if (SessionId != null)
                    {
                        sb.Append(",\"session_id\":");
                        AppendJsonString(sb, SessionId);
                    }

                    sb.Append(",\"fps\":");
                    sb.Append(p.FPS.ToString("G"));
                    sb.Append(",\"memory_mb\":");
                    sb.Append(p.MemoryMb.ToString("G"));
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

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[SummitAnalytics] Flushed {events.Count} events, {perfs.Count} perf samples");
                }
                else
                {
                    Debug.LogWarning($"[SummitAnalytics] Flush failed: {request.error}");
                    // Re-queue failed events
                    _eventQueue.InsertRange(0, events);
                    _perfQueue.InsertRange(0, perfs);
                }
            }

            _isFlushing = false;
        }

        private IEnumerator EndSessionCoroutine(Dictionary<string, object> finalMetadata)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"session_id\":");
            AppendJsonString(sb, SessionId);
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
                    Debug.Log($"[SummitAnalytics] Session ended: {SessionId}");
                else
                    Debug.LogWarning($"[SummitAnalytics] Session end failed: {request.error}");
            }

            SessionId = null;
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
            public string Timestamp;
        }
    }
}
