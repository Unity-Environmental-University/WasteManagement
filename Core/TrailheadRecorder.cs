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
// Uploads are incremental: every deltaUploadInterval seconds (and on every
// checkpoint/finish), only the frames and events recorded since the last
// successful upload are sent as a "segment" to POST /api/recordings/{id}/segments,
// which appends them to the stored recording server-side. A segment whose
// request fails simply isn't marked synced, so the next segment naturally
// includes its range too -- no separate retry queue is needed. This bounds
// data loss from an abrupt teardown (a WebGL tab closing, a crash) to at most
// one interval's worth of samples, instead of the whole session, which is
// what a single end-of-session upload risked losing.
//
// See the accompanying README.md for a full integration guide.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using CompressionLevel = System.IO.Compression.CompressionLevel;

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

        [Tooltip("How often new frames/events are flushed to the server during a session. Bounds how much a hard teardown (tab close, crash) can lose.")]
        public float deltaUploadInterval = 15f;

        private readonly List<EventData> _recordingEvents = new();
        private readonly Dictionary<string, string> _recordingMetadata = new();

        // ─── Internal state ──────────────────────────────────────────────

        private string _recordingId;
        private string _recordingName;
        private float _recordingStartTime;
        private float _sampleInterval;
        private readonly List<SubjectData> _subjects = new();
        private int _activeUploads;
        private int _syncedEventCount;
        private int _segmentSequence;
        private Coroutine _deltaUploadCoroutine;

        // ─── Public state ────────────────────────────────────────────────

        /// <summary>True while a recording session is active.</summary>
        public bool IsRecording { get; private set; }

        /// <summary>True while a segment upload is in flight.</summary>
        public bool IsUploading { get; private set; }

        /// <summary>
        ///     The recording's ID, known up front since the client generates it.
        ///     Only meaningful once the first segment has landed server-side.
        /// </summary>
        public string RecordingId => _recordingId;

        /// <summary>User ID shared with Summit for player-level aggregation.</summary>
        private string UserId { get; set; }

        /// <summary>Summit session ID this recording belongs to.</summary>
        private string SummitSessionId { get; set; }

        private string SegmentsEndpoint => apiUrl.TrimEnd('/') + "/api/recordings/" + _recordingId + "/segments";

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern int SessionBeaconPost(string url, string apiKey, string payloadBase64);
#endif

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
        ///     Fired when the recording's final segment finishes uploading. The
        ///     string is the recording ID on success, or null on failure.
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

            _recordingId = Guid.NewGuid().ToString();
            _recordingName = sessionName;
            _recordingMetadata.Clear();
            _subjects.Clear();
            _recordingEvents.Clear();
            _syncedEventCount = 0;
            _segmentSequence = 0;
            _recordingStartTime = Time.time;
            _sampleInterval = 1f / sampleRate;
            ApplySummitLinkMetadata();
            IsRecording = true;

            if (CanUpload()) _deltaUploadCoroutine = StartCoroutine(DeltaUploadLoop());
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
        ///     End the recording and upload whatever hasn't been synced yet as the
        ///     final segment. Runs as a coroutine and does not block the game.
        ///     Check OnUploadComplete for the result.
        /// </summary>
        /// <param name="viaBeacon">
        ///     Upload with a request that outlives page unload. Required on WebGL,
        ///     where the browser tears the player down long before a coroutine
        ///     upload could finish. Falls back to the coroutine when the beacon is
        ///     unavailable or the payload is too large for it -- by this point that
        ///     payload is just the last deltaUploadInterval seconds, not the whole
        ///     session, since everything earlier already landed via prior segments.
        /// </param>
        public void FinishRecording(bool viaBeacon = false)
        {
            if (!IsRecording)
            {
                Debug.LogWarning("[TrailheadRecorder] Not recording.");
                return;
            }

            IsRecording = false;
            if (_deltaUploadCoroutine != null)
            {
                StopCoroutine(_deltaUploadCoroutine);
                _deltaUploadCoroutine = null;
            }

            if (!CanUpload())
            {
                Debug.Log("[TrailheadRecorder] Upload skipped in Editor.");
                return;
            }

            UploadDelta(true, viaBeacon);
        }

        /// <summary>
        ///     Sends whatever hasn't been synced yet, without stopping the active
        ///     recording. Called when the browser tab is hidden but may still
        ///     resume (e.g., ordinary tab-switching) -- a safety net in case the tab
        ///     is later killed in the background before a real page-unload fires.
        /// </summary>
        public void SendCheckpoint()
        {
            if (!IsRecording || !CanUpload()) return;
            UploadDelta(false, true);
        }

        // ─── Incremental upload ─────────────────────────────────────────

        private IEnumerator DeltaUploadLoop()
        {
            while (IsRecording)
            {
                yield return new WaitForSecondsRealtime(deltaUploadInterval);
                if (IsRecording) UploadDelta(false, false);
            }
        }

        /// <summary>
        ///     Builds and sends everything recorded since the last successful
        ///     sync. On the coroutine path, sync markers only advance once the
        ///     server has confirmed the segment, so the next call 
        ///     simply includes a failed request's range again -- no separate retry queue.
        ///     On the beacon path there is no response to confirm, so markers
        ///     advance optimistically (matching the existing best-effort beacon
        ///     behavior: a dropped beacon just waits for the next checkpoint).
        /// </summary>
        private void UploadDelta(bool final, bool viaBeacon)
        {
            var segment = BuildSegment(final);
            if (segment == null) return; // nothing new, and not the terminal call

            if (viaBeacon)
            {
                if (TryBeaconUpload(segment.Json))
                {
                    CommitSyncTargets(segment);
                    return;
                }

                // Only the terminal call falls back to a normal upload; a dropped
                // checkpoint just waits for the next one.
                if (!final) return;
            }

            StartCoroutine(UploadSegment(segment));
        }

        private bool TryBeaconUpload(string json)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                var payload = GzipCompressToBase64(json);
                if (SessionBeaconPost(SegmentsEndpoint, apiKey, payload) == 1)
                {
                    Debug.Log($"[TrailheadRecorder] Segment sent via unload beacon. Payload length: {json.Length} (gzip: {payload.Length}).");
                    return true;
                }

                Debug.LogWarning("[TrailheadRecorder] Beacon declined the segment; falling back to coroutine upload.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[TrailheadRecorder] Beacon upload unavailable: {exception.Message}");
            }
#endif
            return false;
        }

        /// <summary>
        ///     Gzips JSON and base64-encodes it so it can cross the WebGL JS
        ///     interop boundary as a string. The JS side decodes it back to bytes
        ///     before sending, so the byte budget it checks is the gzip size, not
        ///     this (larger) base64 string length.
        /// </summary>
        private static string GzipCompressToBase64(string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal, true))
            {
                gzip.Write(bytes, 0, bytes.Length);
            }

            return Convert.ToBase64String(output.ToArray());
        }

        private IEnumerator UploadSegment(SegmentSnapshot segment)
        {
            _activeUploads++;
            IsUploading = true;

            var body = Encoding.UTF8.GetBytes(segment.Json);

            using var request = new UnityWebRequest(SegmentsEndpoint, "POST");
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("x-api-key", apiKey);
            request.timeout = Mathf.CeilToInt(uploadTimeout);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                CommitSyncTargets(segment);
                if (segment.Final)
                {
                    Debug.Log($"[TrailheadRecorder] Recording complete. ID: {_recordingId}");
                    Debug.Log($"[TrailheadRecorder] Replay URL: {GetReplayUrl(_recordingId)}");
                    OnUploadComplete?.Invoke(_recordingId);
                }
            }
            else
            {
                Debug.LogWarning($"[TrailheadRecorder] Segment upload failed: {request.error}");
                if (segment.Final) OnUploadComplete?.Invoke(null);
            }

            _activeUploads = Mathf.Max(0, _activeUploads - 1);
            IsUploading = _activeUploads > 0;
        }

        private void CommitSyncTargets(SegmentSnapshot segment)
        {
            foreach (var target in segment.Subjects)
            {
                target.Subject.Introduced = true;
                target.Subject.SyncedFrameCount = target.FrameTarget;
                target.Subject.SyncedEventCount = target.EventTarget;
            }

            _syncedEventCount = segment.GlobalEventTarget;
        }

        /// <summary>
        ///     Convenience: builds the replay URL for a given recording ID.
        /// </summary>
        private string GetReplayUrl(string recordingId)
        {
            return $"{apiUrl}/recording?id={recordingId}";
        }

        // ─── Segment serialization ───────────────────────────────────────
        //
        // Builds a JSON delta matching Trailhead's segment format:
        //
        //   {
        //     "segmentId": "<recording id>-<n>",   // required; the server's idempotency key
        //     "name": "...",
        //     "metadata": { ... },
        //     "subjects": [
        //       {
        //         "index": 0,
        //         "isNew": true,
        //         "name": "...",
        //         "metadata": { ... },
        //         "frames": { "t": [...], "x": [...], ... },   // only new samples
        //         "events": [{ "t": 0, "name": "...", "data": { ... } }]  // only new events
        //       }
        //     ],
        //     "events": [...],   // only new global events
        //     "final": false
        //   }
        //
        // The server appends each subject's frames/events onto what it already
        // has for that index, rather than replacing them. We build JSON manually
        // to avoid pulling in a JSON library dependency.

        private SegmentSnapshot BuildSegment(bool final)
        {
            var subjectTargets = new List<SubjectSyncTarget>();
            var sb = new StringBuilder(1024);
            sb.Append('{');

            // The server rejects a segment without this and uses it to make a
            // retry idempotent. Numbered per recording rather than a fresh GUID,
            // so the ordering of a session's segments stays readable server-side.
            var segmentId = _recordingId + "-" + _segmentSequence.ToString(CultureInfo.InvariantCulture);
            sb.Append("\"segmentId\":");
            AppendJsonString(sb, segmentId);

            sb.Append(",\"name\":");
            AppendJsonString(sb, _recordingName);

            sb.Append(",\"metadata\":");
            AppendJsonDict(sb, _recordingMetadata);

            sb.Append(",\"subjects\":[");
            var firstSubject = true;
            for (var i = 0; i < _subjects.Count; i++)
            {
                var subject = _subjects[i];
                var frameTarget = subject.T.Count;
                var eventTarget = subject.Events.Count;
                var introduce = !subject.Introduced;

                if (!introduce && frameTarget == subject.SyncedFrameCount && eventTarget == subject.SyncedEventCount)
                    continue;

                if (!firstSubject) sb.Append(',');
                firstSubject = false;
                AppendSegmentSubjectJson(sb, subject, introduce, i, frameTarget, eventTarget);

                subjectTargets.Add(new SubjectSyncTarget
                {
                    Subject = subject,
                    FrameTarget = frameTarget,
                    EventTarget = eventTarget
                });
            }

            sb.Append(']');

            var globalEventTarget = _recordingEvents.Count;
            sb.Append(",\"events\":");
            AppendEventsJsonRange(sb, _recordingEvents, _syncedEventCount, globalEventTarget);

            sb.Append(",\"final\":");
            sb.Append(final ? "true" : "false");
            sb.Append('}');

            if (subjectTargets.Count == 0 && globalEventTarget == _syncedEventCount && !final)
                return null;

            _segmentSequence++;

            return new SegmentSnapshot
            {
                Json = sb.ToString(),
                Subjects = subjectTargets,
                GlobalEventTarget = globalEventTarget,
                Final = final
            };
        }

        private static void AppendSegmentSubjectJson(StringBuilder sb, SubjectData subject, bool introduce, int index,
            int frameTarget, int eventTarget)
        {
            sb.Append('{');

            sb.Append("\"index\":");
            sb.Append(index.ToString(CultureInfo.InvariantCulture));

            sb.Append(",\"isNew\":");
            sb.Append(introduce ? "true" : "false");

            sb.Append(",\"name\":");
            AppendJsonString(sb, subject.Name);

            sb.Append(",\"metadata\":");
            AppendJsonDict(sb, subject.Metadata);

            sb.Append(",\"frames\":{");
            AppendFloatArrayRange(sb, "t", subject.T, subject.SyncedFrameCount, frameTarget);
            sb.Append(',');
            AppendFloatArrayRange(sb, "x", subject.X, subject.SyncedFrameCount, frameTarget);
            sb.Append(',');
            AppendFloatArrayRange(sb, "y", subject.Y, subject.SyncedFrameCount, frameTarget);
            sb.Append(',');
            AppendFloatArrayRange(sb, "z", subject.Z, subject.SyncedFrameCount, frameTarget);
            sb.Append(',');
            AppendFloatArrayRange(sb, "rx", subject.Rx, subject.SyncedFrameCount, frameTarget);
            sb.Append(',');
            AppendFloatArrayRange(sb, "ry", subject.Ry, subject.SyncedFrameCount, frameTarget);
            sb.Append(',');
            AppendFloatArrayRange(sb, "rz", subject.Rz, subject.SyncedFrameCount, frameTarget);
            sb.Append(',');
            AppendFloatArrayRange(sb, "rw", subject.RW, subject.SyncedFrameCount, frameTarget);
            sb.Append('}');

            sb.Append(",\"events\":");
            AppendEventsJsonRange(sb, subject.Events, subject.SyncedEventCount, eventTarget);

            sb.Append('}');
        }

        private static void AppendEventsJsonRange(StringBuilder sb, List<EventData> events, int from, int to)
        {
            sb.Append('[');
            for (var i = from; i < to; i++)
            {
                if (i > from) sb.Append(',');
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

        private static void AppendFloatArrayRange(StringBuilder sb, string key, List<float> values, int from, int to)
        {
            sb.Append('"');
            sb.Append(key);
            sb.Append("\":[");
            for (var i = from; i < to; i++)
            {
                if (i > from) sb.Append(',');
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

        // ─── Upload gating ──────────────────────────────────────────────

        private bool CanUpload()
        {
#if UNITY_EDITOR
            return enableEditorNetworking;
#else
            return true;
#endif
        }

        // ─── Internal types ──────────────────────────────────────────────

        private class SubjectData
        {
            public readonly List<EventData> Events = new();
            public bool Introduced;
            public Vector3 LastRecordedPosition;
            public float LastSampleTime;
            public readonly Dictionary<string, string> Metadata = new();
            public string Name;
            public readonly List<float> RW = new();
            public readonly List<float> Rx = new();
            public readonly List<float> Ry = new();
            public readonly List<float> Rz = new();
            public int SyncedEventCount;
            public int SyncedFrameCount;
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

        private class SubjectSyncTarget
        {
            public int EventTarget;
            public int FrameTarget;
            public SubjectData Subject;
        }

        private class SegmentSnapshot
        {
            public bool Final;
            public int GlobalEventTarget;
            public string Json;
            public List<SubjectSyncTarget> Subjects;
        }
    }
}
