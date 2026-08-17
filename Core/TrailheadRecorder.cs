// TrailheadRecorder.cs
//
// Drop-in Unity MonoBehaviour for recording 3D sessions and uploading
// them to a Trailhead server.
//
// Usage:
//   1. Attach this script to a persistent GameObject in your scene.
//   2. Set apiUrl and apiKey in the Inspector (or via code).
//   3. Call StartRecording(), register subjects, then FinishRecording().
//
// See the accompanying README.md for a full integration guide.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace _project.Scripts.Core
{
    public class TrailheadRecorder : MonoBehaviour
    {
        // ─── Configuration ───────────────────────────────────────────────

        [Header("Trailhead Server")] [Tooltip("Base URL of the Trailhead dashboard (no trailing slash)")]
        public string apiUrl = "https://your-trailhead-domain.com";

        [Tooltip("API key sent via x-api-key header")] [PasswordField]
        public string apiKey = "";

        [Tooltip("Allows real uploads while running in the Unity Editor. Disabled by default to protect production data.")]
        public bool enableEditorNetworking;

        [Header("Recording Settings")] [Tooltip("Target samples per second per subject")]
        public float sampleRate = 10f;

        [Tooltip("Minimum position delta to record a new frame")]
        public float minPositionDelta = 0.01f;

        [Tooltip("Upload timeout in seconds")] public float uploadTimeout = 5f;

        private readonly List<EventData> _recordingEvents = new();
        private readonly Dictionary<string, string> _recordingMetadata = new();

        // ─── Internal state ──────────────────────────────────────────────

        private string _recordingName;
        private float _recordingStartTime;
        private float _sampleInterval;
        private readonly List<SubjectData> _subjects = new();
        private int _activeUploads;

        // ─── Public state ────────────────────────────────────────────────

        /// <summary>True while a recording session is active.</summary>
        public bool IsRecording { get; private set; }

        /// <summary>True while a completed recording is being uploaded.</summary>
        public bool IsUploading { get; private set; }

        /// <summary>User ID shared with Summit for player-level aggregation.</summary>
        private string UserId { get; set; }

        /// <summary>Summit session ID this recording belongs to.</summary>
        private string SummitSessionId { get; set; }

        private string PendingUploadDirectory => Path.Combine(Application.persistentDataPath, "TrailheadPendingUploads");

        private string RecordingsEndpoint => apiUrl.TrimEnd('/') + "/api/recordings";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int SessionBeaconPost(string url, string apiKey, string json);
#endif

        private void Start()
        {
            if (CanUpload()) StartCoroutine(UploadPendingRecordings());
        }

        // ─── Frame sampling ──────────────────────────────────────────────

        private void Update()
        {
            if (!IsRecording) return;

            var elapsed = Time.time - _recordingStartTime;

            foreach (var subject in _subjects)
            {
                // Skip event-only subjects (no transform to track)
                if (!subject.TrackedTransform) continue;

                // Respect sample rate
                if (elapsed - subject.LastSampleTime < _sampleInterval) continue;

                // Respect minimum position delta
                var pos = subject.TrackedTransform.position;
                if (subject.T.Count > 0 &&
                    Vector3.Distance(pos, subject.LastRecordedPosition) < minPositionDelta)
                    continue;

                var rot = subject.TrackedTransform.rotation;

                subject.T.Add(elapsed);
                subject.X.Add(pos.x);
                subject.Y.Add(pos.y);
                subject.Z.Add(pos.z);
                subject.Rx.Add(rot.x);
                subject.Ry.Add(rot.y);
                subject.Rz.Add(rot.z);
                subject.RW.Add(rot.w);

                subject.LastRecordedPosition = pos;
                subject.LastSampleTime = elapsed;
            }
        }

        // ─── Events ──────────────────────────────────────────────────────

        /// <summary>
        ///     Fired when an upload completes. The string is the recording ID on
        ///     success, or null on failure.
        /// </summary>
        public event Action<string> OnUploadComplete;

        // ─── Public API ──────────────────────────────────────────────────

        /// <summary>
        ///     Begin a new recording session.
        /// </summary>
        /// <param name="sessionName">Display name, e.g. "Free Play - Noon"</param>
        public void StartRecording(string sessionName)
        {
            if (IsRecording)
            {
                Debug.LogWarning("[TrailheadRecorder] Already recording. Call FinishRecording first.");
                return;
            }

            _recordingName = sessionName;
            _recordingMetadata.Clear();
            _subjects.Clear();
            _recordingEvents.Clear();
            _recordingStartTime = Time.time;
            _sampleInterval = 1f / sampleRate;
            ApplySummitLinkMetadata();
            IsRecording = true;
        }

        /// <summary>
        ///     Set the user ID shared with Summit. Call before StartRecording when possible.
        /// </summary>
        public void Identify(string userId)
        {
            UserId = userId;
            if (IsRecording) ApplySummitLinkMetadata();
        }

        /// <summary>
        ///     Link this recording to a Summit analytics session.
        /// </summary>
        public void LinkSummitSession(string summitSessionId, string userId = null)
        {
            SummitSessionId = summitSessionId;
            if (!string.IsNullOrEmpty(userId)) UserId = userId;

            if (IsRecording) ApplySummitLinkMetadata();
        }

        /// <summary>
        ///     Clear the active Summit session link before starting an unrelated recording.
        /// </summary>
        public void ClearSummitSessionLink()
        {
            SummitSessionId = null;
            if (IsRecording) _recordingMetadata.Remove("summit_session_id");
        }

        /// <summary>
        ///     Set a metadata key/value pair on the recording.
        ///     Can be called at any point during the session.
        /// </summary>
        public void SetMetadata(string key, string value)
        {
            _recordingMetadata[key] = value;
        }

        private void ApplySummitLinkMetadata()
        {
            if (!string.IsNullOrEmpty(UserId)) _recordingMetadata["user_id"] = UserId;

            if (!string.IsNullOrEmpty(SummitSessionId)) _recordingMetadata["summit_session_id"] = SummitSessionId;
        }

        /// <summary>
        ///     Register a subject to track. Returns a handle (int) used to
        ///     reference this subject when adding events.
        /// </summary>
        /// <param name="subjectName">Subject type name, e.g. "Player", "Rabbit"</param>
        /// <param name="tracked">The Transform to sample each frame. Pass null for event-only subjects (e.g., FPS Tracker).</param>
        /// <param name="metadata">Optional metadata dictionary for this subject.</param>
        /// <returns>Subject index (handle) for use with AddSubjectEvent.</returns>
        public int AddSubject(string subjectName, Transform tracked, Dictionary<string, string> metadata = null)
        {
            var subject = new SubjectData
            {
                Name = subjectName,
                TrackedTransform = tracked,
                LastRecordedPosition = tracked?.position ?? Vector3.zero,
                LastSampleTime = -_sampleInterval // ensure first sample is recorded
            };

            if (metadata != null)
                foreach (var kvp in metadata)
                    subject.Metadata[kvp.Key] = kvp.Value;

            _subjects.Add(subject);
            return _subjects.Count - 1;
        }

        /// <summary>
        ///     Add a global event to the recording (not tied to a subject).
        /// </summary>
        public void AddEvent(string eventName, Dictionary<string, string> data = null)
        {
            if (!IsRecording) return;

            _recordingEvents.Add(new EventData
            {
                T = Time.time - _recordingStartTime,
                Name = eventName,
                Data = data ?? new Dictionary<string, string>()
            });
        }

        /// <summary>
        ///     Add an event to a specific subject.
        /// </summary>
        /// <param name="subjectIndex">Handle returned by AddSubject.</param>
        /// <param name="eventName">Name of the event to record.</param>
        /// <param name="data">Optional metadata for this event.</param>
        public void AddSubjectEvent(int subjectIndex, string eventName, Dictionary<string, string> data = null)
        {
            if (!IsRecording) return;
            if (subjectIndex < 0 || subjectIndex >= _subjects.Count) return;

            _subjects[subjectIndex].Events.Add(new EventData
            {
                T = Time.time - _recordingStartTime,
                Name = eventName,
                Data = data ?? new Dictionary<string, string>()
            });
        }

        /// <summary>
        ///     Remove all subjects. Call this between sessions (e.g., when
        ///     returning to the menu) so the next recording starts fresh.
        /// </summary>
        public void ClearSubjects()
        {
            _subjects.Clear();
        }

        /// <summary>
        ///     End the recording and upload the payload. The upload runs as a
        ///     coroutine and does not block the game. Check OnUploadComplete or
        ///     poll LastRecordingId for the result.
        /// </summary>
        /// <param name="viaBeacon">
        ///     Upload with a request that outlives page unload. Required on WebGL,
        ///     where the browser tears the player down long before a coroutine
        ///     upload could finish. Falls back to the coroutine when the beacon is
        ///     unavailable or the payload is too large for it.
        /// </param>
        public void FinishRecording(bool viaBeacon = false)
        {
            if (!IsRecording)
            {
                Debug.LogWarning("[TrailheadRecorder] Not recording.");
                return;
            }

            IsRecording = false;
            var json = BuildPayloadJson();

            if (!CanUpload())
            {
                Debug.Log("[TrailheadRecorder] Upload skipped in Editor. Payload length: " + json.Length);
                return;
            }

            // The beacon is fire-and-forget, so it deliberately skips the retry
            // queue: a queued copy would be re-uploaded on the next launch and
            // duplicate the recording every time the beacon succeeded.
            if (viaBeacon && TryBeaconUpload(json)) return;

            // Persist before starting the coroutine. Unity can terminate a play-mode
            // run or scene unexpectedly, which would otherwise discard the payload.
            var pendingPath = SavePendingRecording(json);
            BeginUpload(json, pendingPath);
        }

        /// <summary>
        ///     Hands the payload to a browser request that survives page unload.
        ///     Returns false off WebGL, or when the payload exceeds the browser's
        ///     keepalive budget, so the caller can fall back to a normal upload.
        /// </summary>
        private bool TryBeaconUpload(string json)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                if (SessionBeaconPost(RecordingsEndpoint, apiKey, json) == 1)
                {
                    Debug.Log($"[TrailheadRecorder] Recording sent via unload beacon. Payload length: {json.Length}");
                    return true;
                }

                Debug.LogWarning("[TrailheadRecorder] Beacon declined the payload; falling back to coroutine upload.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[TrailheadRecorder] Beacon upload unavailable: {exception.Message}");
            }
#endif
            return false;
        }

        /// <summary>
        ///     Convenience: builds the replay URL for a given recording ID.
        /// </summary>
        private string GetReplayUrl(string recordingId)
        {
            return $"{apiUrl}/recording?id={recordingId}";
        }

        // ─── Payload serialization ───────────────────────────────────────
        //
        // Builds JSON matching the Trailhead RawRecording format:
        //
        //   {
        //     "name": "...",
        //     "metadata": { ... },
        //     "subjects": [
        //       {
        //         "name": "...",
        //         "metadata": { ... },
        //         "frames": { "t": [...], "x": [...], ... },
        //         "events": [{ "t": 0, "name": "...", "data": { ... } }]
        //       }
        //     ],
        //     "events": [...]
        //   }
        //
        // We build JSON manually to avoid pulling in a JSON library dependency.

        private string BuildPayloadJson()
        {
            var sb = new StringBuilder(4096);
            sb.Append('{');

            // name
            sb.Append("\"name\":");
            AppendJsonString(sb, _recordingName);

            // metadata
            sb.Append(",\"metadata\":");
            AppendJsonDict(sb, _recordingMetadata);

            // subjects
            sb.Append(",\"subjects\":[");
            for (var i = 0; i < _subjects.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendSubjectJson(sb, _subjects[i]);
            }

            sb.Append(']');

            // events
            sb.Append(",\"events\":");
            AppendEventsJson(sb, _recordingEvents);

            sb.Append('}');
            return sb.ToString();
        }

        private void AppendSubjectJson(StringBuilder sb, SubjectData subject)
        {
            sb.Append('{');

            sb.Append("\"name\":");
            AppendJsonString(sb, subject.Name);

            sb.Append(",\"metadata\":");
            AppendJsonDict(sb, subject.Metadata);

            // Columnar frames
            sb.Append(",\"frames\":{");
            AppendFloatArray(sb, "t", subject.T);
            sb.Append(',');
            AppendFloatArray(sb, "x", subject.X);
            sb.Append(',');
            AppendFloatArray(sb, "y", subject.Y);
            sb.Append(',');
            AppendFloatArray(sb, "z", subject.Z);
            sb.Append(',');
            AppendFloatArray(sb, "rx", subject.Rx);
            sb.Append(',');
            AppendFloatArray(sb, "ry", subject.Ry);
            sb.Append(',');
            AppendFloatArray(sb, "rz", subject.Rz);
            sb.Append(',');
            AppendFloatArray(sb, "rw", subject.RW);
            sb.Append('}');

            sb.Append(",\"events\":");
            AppendEventsJson(sb, subject.Events);

            sb.Append('}');
        }

        private void AppendEventsJson(StringBuilder sb, List<EventData> events)
        {
            sb.Append('[');
            for (var i = 0; i < events.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('{');
                sb.Append("\"t\":");
                sb.Append(events[i].T.ToString("G", CultureInfo.InvariantCulture));
                sb.Append(",\"name\":");
                AppendJsonString(sb, events[i].Name);
                sb.Append(",\"data\":");
                AppendJsonDict(sb, events[i].Data);
                sb.Append('}');
            }

            sb.Append(']');
        }

        private static void AppendFloatArray(StringBuilder sb, string key, List<float> values)
        {
            sb.Append('"');
            sb.Append(key);
            sb.Append("\":[");
            for (var i = 0; i < values.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(values[i].ToString("G", CultureInfo.InvariantCulture));
            }

            sb.Append(']');
        }

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

        private static void AppendJsonDict(StringBuilder sb, Dictionary<string, string> dict)
        {
            sb.Append('{');
            var first = true;
            foreach (var kvp in dict)
            {
                if (!first) sb.Append(',');
                first = false;
                AppendJsonString(sb, kvp.Key);
                sb.Append(':');
                AppendJsonString(sb, kvp.Value);
            }

            sb.Append('}');
        }

        // ─── Upload ──────────────────────────────────────────────────────

        private bool CanUpload()
        {
#if UNITY_EDITOR
            return enableEditorNetworking;
#else
            return true;
#endif
        }

        private string SavePendingRecording(string json)
        {
            try
            {
                Directory.CreateDirectory(PendingUploadDirectory);
                var path = Path.Combine(PendingUploadDirectory, $"{Guid.NewGuid():N}.json");
                File.WriteAllText(path, json, Encoding.UTF8);
                return path;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[TrailheadRecorder] Could not persist recording for retry: {exception.Message}");
                return null;
            }
        }

        private void BeginUpload(string json, string pendingPath)
        {
            _activeUploads++;
            IsUploading = true;
            StartCoroutine(UploadRecording(json, pendingPath));
        }

        private IEnumerator UploadPendingRecordings()
        {
            if (!Directory.Exists(PendingUploadDirectory)) yield break;

            string[] pendingPaths;
            try
            {
                pendingPaths = Directory.GetFiles(PendingUploadDirectory, "*.json");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[TrailheadRecorder] Could not inspect pending recordings: {exception.Message}");
                yield break;
            }

            foreach (var pendingPath in pendingPaths)
            {
                string json;
                try
                {
                    json = File.ReadAllText(pendingPath, Encoding.UTF8);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[TrailheadRecorder] Could not read pending recording: {exception.Message}");
                    continue;
                }

                BeginUpload(json, pendingPath);
                while (IsUploading) yield return null;
            }
        }

        private IEnumerator UploadRecording(string json, string pendingPath)
        {
            var body = Encoding.UTF8.GetBytes(json);

            using var request = new UnityWebRequest(RecordingsEndpoint, "POST");
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("x-api-key", apiKey);
            request.timeout = Mathf.CeilToInt(uploadTimeout);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // Response: { "id": "<uuid>" }
                var responseBody = request.downloadHandler.text;
                var id = ParseIdFromResponse(responseBody);
                Debug.Log($"[TrailheadRecorder] Upload success. ID: {id}");
                Debug.Log($"[TrailheadRecorder] Replay URL: {GetReplayUrl(id)}");
                DeletePendingRecording(pendingPath);
                OnUploadComplete?.Invoke(id);
            }
            else
            {
                Debug.LogWarning($"[TrailheadRecorder] Upload failed: {request.error}");
                OnUploadComplete?.Invoke(null);
            }

            _activeUploads = Mathf.Max(0, _activeUploads - 1);
            IsUploading = _activeUploads > 0;
        }

        private static void DeletePendingRecording(string pendingPath)
        {
            if (string.IsNullOrEmpty(pendingPath)) return;

            try
            {
                if (File.Exists(pendingPath)) File.Delete(pendingPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[TrailheadRecorder] Uploaded recording could not be removed from retry queue: {exception.Message}");
            }
        }

        /// <summary>
        ///     Minimal JSON parse for { "id": "value" } without a JSON library.
        /// </summary>
        private static string ParseIdFromResponse(string json)
        {
            // Find "id" key then extract the string value after it
            var keyIndex = json.IndexOf("\"id\"", StringComparison.Ordinal);
            if (keyIndex < 0) return null;

            var colonIndex = json.IndexOf(':', keyIndex + 4);
            if (colonIndex < 0) return null;

            var openQuote = json.IndexOf('"', colonIndex + 1);
            if (openQuote < 0) return null;

            var closeQuote = json.IndexOf('"', openQuote + 1);
            if (closeQuote < 0) return null;

            return json.Substring(openQuote + 1, closeQuote - openQuote - 1);
        }

        // ─── Internal types ──────────────────────────────────────────────

        private class SubjectData
        {
            public readonly List<EventData> Events = new();
            public Vector3 LastRecordedPosition;
            public float LastSampleTime;
            public readonly Dictionary<string, string> Metadata = new();
            public string Name;
            public readonly List<float> RW = new();
            public readonly List<float> Rx = new();
            public readonly List<float> Ry = new();
            public readonly List<float> Rz = new();
            public readonly List<float> T = new();

            public Transform TrackedTransform;
            public readonly List<float> X = new();
            public readonly List<float> Y = new();
            public readonly List<float> Z = new();
        }

        private class EventData
        {
            public Dictionary<string, string> Data;
            public string Name;
            public float T;
        }
    }
}
