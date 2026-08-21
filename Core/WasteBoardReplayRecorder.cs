using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using _project.Scripts.Object_Scripts;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace _project.Scripts.Core
{
    /// <summary>
    /// Bridges SludgeTower gameplay into Summit analytics and Trailhead replay.
    /// The board is recorded as metadata plus path mutations; only moving or
    /// placed gameplay objects become sampled Trailhead subjects.
    /// </summary>
    [RequireComponent(typeof(TrailheadRecorder), typeof(SummitAnalytics))]
    public sealed class WasteBoardReplayRecorder : MonoBehaviour
    {
        [Header("Session")]
        [SerializeField] private bool startOnLaunch = true;
        [SerializeField] private string recordingName = "SludgeTower Session";
        [SerializeField, Min(0.05f)] private float subjectDiscoveryInterval = 0.25f;

        [Header("Linked Clients")]
        [SerializeField] private TrailheadRecorder trailhead;
        [SerializeField] private SummitAnalytics summit;

        private readonly Dictionary<int, PathSnapshot> _paths = new();
        private readonly Dictionary<EntityId, TrackedSubject> _subjects = new();
        private readonly Dictionary<EntityId, SquareContentSnapshot> _squareContents = new();
        private readonly HashSet<EntityId> _summitSquareContents = new();
        private PathBuildBoard _board;
        private float _nextSubjectDiscovery;
        private float _sessionStartedAt;
        private bool _finishing;
        private bool _quitting;
        private int _frameCount;
        private float _frameElapsed;

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private const string AnonymousIdKey = "SludgeTowerAnonymousID";

        /// <summary>Name of the final (page-unload) callback invoked from SessionBeacon.jslib.</summary>
        private const string UnloadCallbackName = nameof(HandleBrowserUnload);

        /// <summary>Name of the non-terminal (tab-hidden) checkpoint callback invoked from SessionBeacon.jslib.</summary>
        private const string CheckpointCallbackName = nameof(HandleBrowserHidden);

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void SessionBeaconRegisterUnload(string gameObjectName, string finalMethodName, string checkpointMethodName);
#endif

        private sealed class PathSnapshot
        {
            public int Id;
            public int Length;
            public string Orientation;
            public int InfraValue;
            public List<Vector2Int> Cells;
        }

        private sealed class TrackedSubject
        {
            public Component Target;
            public int Handle;
            public string Category;
        }

        private sealed class SquareContentSnapshot
        {
            public EntityId Id;
            public Component Target;
            public Vector2Int Cell;
            public string Kind;
            public string Label;
            public string Color;
            public string State;
            public string Effect;
            public int? RangeCells;
            public float? FullnessPercent;
            public float? HealthPercent;

            public string Signature =>
                $"{Cell.x},{Cell.y}|{Kind}|{Label}|{Color}|{State}|{Effect}|{RangeCells}|{FullnessPercent}|{HealthPercent}";
        }

        private void Awake()
        {
            if (!trailhead) trailhead = GetComponent<TrailheadRecorder>();
            if (!summit) summit = GetComponent<SummitAnalytics>();

            // An enabled replay coordinator requires Trailhead's Update loop for
            // transform sampling. Events can still upload when the component is
            // disabled, which otherwise produces a misleading event-only replay.
            if (trailhead && !trailhead.enabled) trailhead.enabled = true;
        }

        private void Start()
        {
            if (startOnLaunch) StartSession();
        }

#if UNITY_EDITOR
        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        private void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                FinishSession("Editor Play Mode Stopped");
        }
#endif

        public void StartSession()
        {
            if (_finishing || (trailhead && trailhead.IsRecording)) return;

            var trailheadReady = trailhead && (Application.isEditor || IsConfigured(trailhead.apiUrl, trailhead.apiKey));
            var summitReady = summit && (Application.isEditor || IsConfigured(summit.apiUrl, summit.apiKey));
            if (!trailheadReady && !summitReady)
            {
                Debug.LogWarning("[WasteBoardReplay] Summit and Trailhead are not configured; session capture is disabled.", this);
                return;
            }

            var anonymousId = GetOrCreateAnonymousId();
            _board = GameMaster.Instance ? GameMaster.Instance.pathBuildBoard : FindAnyObjectByType<PathBuildBoard>();
            _sessionStartedAt = Time.realtimeSinceStartup;

            if (summitReady)
            {
                summit.Identify(anonymousId);
                summit.OnSessionStarted += HandleSummitSessionStarted;
                summit.SendHardwareProfile(new Dictionary<string, object>
                {
                    { "source", "sludge_tower" },
                    { "replay_profile", "waste-board" }
                });
                summit.StartSession(Application.version, metadata: new Dictionary<string, object>
                {
                    { "game_title", Application.productName },
                    { "replay_profile", "waste-board" }
                });
            }

            if (!trailheadReady) return;

            RegisterBrowserUnloadHandler();

            trailhead.Identify(anonymousId);
            trailhead.StartRecording(recordingName);
            trailhead.SetMetadata("replay-profile", "waste-board");
            trailhead.SetMetadata("game-title", Application.productName);
            trailhead.SetMetadata("app-version", Application.version);
            RecordBoardMetadata();
            Subscribe();
            RegisterInitialSubjects();
            SyncPaths(true);
            SyncSquareContents(true);
            RecordEvent("Session Started");
        }

        /// <summary>
        ///     Asks the browser to call <see cref="HandleBrowserUnload" /> when the
        ///     page goes away, and <see cref="HandleBrowserHidden" /> when it's
        ///     merely backgrounded. Unity never raises OnApplicationQuit for a
        ///     closed or navigated-away tab. TrailheadRecorder uploads most of the
        ///     recording incrementally as the session runs, so a silent close only
        ///     risks losing the last deltaUploadInterval seconds -- these callbacks
        ///     close that remaining gap.
        /// </summary>
        private void RegisterBrowserUnloadHandler()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                SessionBeaconRegisterUnload(gameObject.name, UnloadCallbackName, CheckpointCallbackName);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[WasteBoardReplay] Could not register unload handler: {exception.Message}", this);
            }
#endif
        }

        /// <summary>
        ///     Invoked from SessionBeacon.jslib via SendMessage as the page unloads
        ///     (pagehide -- tab close, navigation, bfcache). Ends the session.
        ///     Must stay public and parameterless-by-string for SendMessage to bind.
        /// </summary>
        public void HandleBrowserUnload(string _)
        {
            FinishSession("Page Unloaded", viaBeacon: true);
        }

        /// <summary>
        ///     Invoked from SessionBeacon.jslib via SendMessage when the tab is
        ///     hidden. This also fires on ordinary tab-switching, not just
        ///     teardown, so it must NOT end the session -- it only sends a
        ///     best-effort snapshot in case the tab is later killed in the
        ///     background. Must stay public and parameterless-by-string for
        ///     SendMessage to bind.
        /// </summary>
        public void HandleBrowserHidden(string _)
        {
            trailhead?.SendCheckpoint();
        }

        /// <param name="reason">Named Reason String</param>
        /// <param name="viaBeacon">
        ///     Upload through a request that survives page unload. Set only from the
        ///     browser unload callback, where a coroutine upload would be killed
        ///     mid-flight.
        /// </param>
        public void FinishSession(string reason = "Session Finished", bool viaBeacon = false)
        {
            if (_finishing) return;
            _finishing = true;

            var controller = GameMaster.Instance ? GameMaster.Instance.turnController : null;
            var duration = Mathf.Max(0f, Time.realtimeSinceStartup - _sessionStartedAt);
            var summary = new Dictionary<string, string>
            {
                { "duration-seconds", F(duration) },
                { "reason", reason },
                { "round", controller ? controller.currentTurn.ToString(Invariant) : "0" },
                { "moves", controller ? controller.moveCount.ToString(Invariant) : "0" },
                { "level", controller ? controller.currentLevel.ToString(Invariant) : "1" },
                { "avg-fps", F(_frameElapsed > 0f ? _frameCount / _frameElapsed : 0f) }
            };

            SyncSquareContents(false);
            RecordEvent(reason, summary);
            if (trailhead && trailhead.IsRecording)
            {
                foreach (var pair in summary)
                    trailhead.SetMetadata(pair.Key, pair.Value);
                trailhead.FinishRecording(viaBeacon);
            }

            if (summit && summit.IsSessionActive)
                summit.EndSession(summary.ToDictionary(pair => pair.Key, pair => (object)pair.Value));

            Unsubscribe();
        }

        /// <summary>Finishes pending analytics/replay work before exiting, with a bounded wait.</summary>
        public static void RequestApplicationQuit()
        {
            var recorder = FindAnyObjectByType<WasteBoardReplayRecorder>();
            if (recorder)
                recorder.BeginGracefulQuit();
            else
                Application.Quit();
        }

        /// <summary>
        ///     Completes the active replay upload before replacing the current scene.
        ///     Scene loads otherwise destroy this component and abort its upload coroutine.
        /// </summary>
        public static void RequestRestartCurrentScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            var recorder = FindAnyObjectByType<WasteBoardReplayRecorder>();
            if (recorder)
                recorder.BeginFinishThenLoad(activeScene.name);
            else
                SceneManager.LoadScene(activeScene.name);
        }

        private void BeginGracefulQuit()
        {
            if (_quitting) return;
            _quitting = true;
            StartCoroutine(FinishThenQuit());
        }

        private void BeginFinishThenLoad(string sceneName)
        {
            if (_quitting) return;
            _quitting = true;
            StartCoroutine(FinishThenLoad(sceneName));
        }

        private IEnumerator FinishThenQuit()
        {
            FinishSession("Player Quit");
            yield return WaitForTrailheadUpload();
            Application.Quit();
        }

        private IEnumerator FinishThenLoad(string sceneName)
        {
            FinishSession("Scene Restarted");
            yield return WaitForTrailheadUpload();
            SceneManager.LoadScene(sceneName);
        }

        private IEnumerator WaitForTrailheadUpload()
        {
            var deadline = Time.realtimeSinceStartup + (trailhead ? trailhead.uploadTimeout + 0.5f : 0.5f);
            while (trailhead && trailhead.IsUploading && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        private void Update()
        {
            if (trailhead && trailhead.IsRecording)
            {
                _frameCount++;
                _frameElapsed += Time.unscaledDeltaTime;
            }

            if (!trailhead || !trailhead.IsRecording || Time.unscaledTime < _nextSubjectDiscovery) return;

            _nextSubjectDiscovery = Time.unscaledTime + subjectDiscoveryInterval;
            DiscoverSubjects();
            RetireDestroyedSubjects();
            SyncSquareContents(false);
        }

        private void OnApplicationQuit()
        {
            FinishSession("Application Quit");
        }

        private void OnDisable()
        {
            Unsubscribe();
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
#endif
        }

        private void Subscribe()
        {
            if (_board) _board.PathLayoutChanged += HandlePathLayoutChanged;
            GameMaster.PlacementCompleted += HandlePlacementCompleted;
            TurnController.OnCardPhaseEntered += HandleCardPhase;
            TurnController.OnTowerPhaseEntered += HandleTowerPhase;
            TurnController.OnLevelChanged += HandleLevelChanged;
            TurnController.OnGameLost += HandleGameLost;
            IssueObject.OnReachedEnd += HandleIssueReachedEnd;
            IssueObject.OnPipeBroken += HandlePipeBroken;
        }

        private void Unsubscribe()
        {
            if (_board) _board.PathLayoutChanged -= HandlePathLayoutChanged;
            GameMaster.PlacementCompleted -= HandlePlacementCompleted;
            TurnController.OnCardPhaseEntered -= HandleCardPhase;
            TurnController.OnTowerPhaseEntered -= HandleTowerPhase;
            TurnController.OnLevelChanged -= HandleLevelChanged;
            TurnController.OnGameLost -= HandleGameLost;
            IssueObject.OnReachedEnd -= HandleIssueReachedEnd;
            IssueObject.OnPipeBroken -= HandlePipeBroken;
            if (summit) summit.OnSessionStarted -= HandleSummitSessionStarted;
        }

        private void HandleSummitSessionStarted(string sessionId)
        {
            if (trailhead) trailhead.LinkSummitSession(sessionId, summit ? summit.UserId : null);

            // Summit session creation is asynchronous, so initial board content is
            // usually discovered before Summit can accept events. Re-sync and backfill
            // the current snapshot once the session id exists.
            SyncSquareContents(false);
            foreach (var content in _squareContents.Values)
            {
                if (_summitSquareContents.Contains(content.Id)) continue;
                TrackSquareContentInSummit("Square Content Added", content, true);
            }
        }

        private void RecordBoardMetadata()
        {
            if (!_board || !trailhead) return;

            var origin = _board.transform.position;
            var rotation = _board.transform.rotation;
            var pitch = _board.CellWorldPitch;
            var cell = _board.CellWorldSize;
            trailhead.SetMetadata("board-columns", _board.Columns.ToString(Invariant));
            trailhead.SetMetadata("board-rows", _board.Rows.ToString(Invariant));
            trailhead.SetMetadata("board-pitch-x", F(pitch.x));
            trailhead.SetMetadata("board-pitch-z", F(pitch.y));
            trailhead.SetMetadata("board-cell-x", F(cell.x));
            trailhead.SetMetadata("board-cell-y", F(cell.y));
            trailhead.SetMetadata("board-cell-z", F(cell.z));
            trailhead.SetMetadata("board-origin", V(origin));
            trailhead.SetMetadata("board-rotation", Q(rotation));
        }

        private void RegisterInitialSubjects()
        {
            var camera = Camera.main;
            if (camera) RegisterSubject(camera, "Player Camera", "camera", "box", "#ffffff", new Vector3(0.2f, 0.2f, 0.4f), "Present");
            DiscoverSubjects();
        }

        private void DiscoverSubjects()
        {
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude))
            {
                if (!behaviour || behaviour == this) continue;

                switch (behaviour)
                {
                    case IssueObject issue:
                        RegisterSubject(issue, $"Issue {issue.GetEntityId()}", "issue", "sphere", "#d97706",
                            Vector3.one * 0.35f, "Spawned");
                        break;
                    case TowerController tower:
                        RegisterPlacedSubject(tower, "Tower", "#38bdf8");
                        break;
                    case WasteSifter sifter:
                        RegisterPlacedSubject(sifter, "Waste Sifter", "#84cc16");
                        break;
                    case Cesspit cesspit:
                        RegisterPlacedSubject(cesspit, "Cesspit", "#a16207");
                        break;
                    case TreatmentTank tank:
                        RegisterPlacedSubject(tank, "Treatment Tank", "#14b8a6");
                        break;
                    case LimeSprinkler sprinkler:
                        RegisterPlacedSubject(sprinkler, "Lime Sprinkler", "#bef264");
                        break;
                    case PathSplitter splitter:
                        RegisterPlacedSubject(splitter, "Path Splitter", "#c084fc");
                        break;
                }
            }
        }

        private void RegisterPlacedSubject(Component component, string displayName, string color)
        {
            RegisterSubject(component, $"{displayName} {component.GetEntityId()}", "placement", "box", color,
                Measure(component), "Placed");
        }

        private void RegisterSubject(Component component, string subjectName, string category, string geometry,
            string color, Vector3 scale, string initialEvent)
        {
            if (!component || _subjects.ContainsKey(component.GetEntityId()) || !trailhead || !trailhead.IsRecording)
                return;

            var handle = trailhead.AddSubject(subjectName, component.transform, new Dictionary<string, string>
            {
                { "category", category },
                { "display-name", subjectName[..subjectName.LastIndexOf(' ')] },
                { "trailhead-geom", geometry },
                { "trailhead-color", color },
                { "trailhead-scale", V(scale) }
            });
            _subjects[component.GetEntityId()] = new TrackedSubject
            {
                Target = component,
                Handle = handle,
                Category = category
            };
            trailhead.AddSubjectEvent(handle, initialEvent, SubjectLocation(component));
        }

        private void RetireDestroyedSubjects()
        {
            foreach (var entry in _subjects.Where(pair => !pair.Value.Target).ToArray())
            {
                trailhead.AddSubjectEvent(entry.Value.Handle, "Removed", new Dictionary<string, string>
                {
                    { "category", entry.Value.Category }
                });
                _subjects.Remove(entry.Key);
            }
        }

        private void HandlePathLayoutChanged() => SyncPaths(false);

        private void SyncPaths(bool initial)
        {
            if (!_board || !trailhead || !trailhead.IsRecording) return;

            var current = _board.PlacedPieces.ToDictionary(piece => piece.id, Snapshot);
            foreach (var removed in _paths.Keys.Except(current.Keys).ToArray())
                RecordPathEvent("Path Removed", _paths[removed], initial);
            foreach (var added in current.Keys.Except(_paths.Keys))
                RecordPathEvent("Path Placed", current[added], initial);

            _paths.Clear();
            foreach (var pair in current) _paths[pair.Key] = pair.Value;
        }

        private void RecordPathEvent(string eventName, PathSnapshot path, bool initial)
        {
            var data = new Dictionary<string, string>
            {
                { "piece-id", path.Id.ToString(Invariant) },
                { "length", path.Length.ToString(Invariant) },
                { "orientation", path.Orientation },
                { "infra-value", path.InfraValue.ToString(Invariant) },
                { "cells", string.Join(";", path.Cells.Select(cell => $"{cell.x},{cell.y}")) },
                { "initial", initial ? "true" : "false" }
            };
            RecordEvent(eventName, data, new Dictionary<string, double>
            {
                { "piece_length", path.Length },
                { "infrastructure_value", path.InfraValue },
                { "cells_affected", path.Cells.Count }
            });
        }

        private void SyncSquareContents(bool initial)
        {
            if (!_board || !trailhead || !trailhead.IsRecording) return;

            var current = new Dictionary<EntityId, SquareContentSnapshot>();
            // Inactive PlaceSpot prefabs retain their scene transforms. Including
            // them projects those hidden slots onto unrelated board cells in the
            // replay, so only capture content currently present in the game.
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude))
            {
                if (!TrySnapshotSquareContent(behaviour, out var snapshot)) continue;
                current[snapshot.Id] = snapshot;
            }

            foreach (var removedId in _squareContents.Keys.Except(current.Keys).ToArray())
                RecordSquareContentEvent("Square Content Removed", _squareContents[removedId], initial);

            foreach (var pair in current)
            {
                if (!_squareContents.TryGetValue(pair.Key, out var previous))
                    RecordSquareContentEvent("Square Content Added", pair.Value, initial);
                else if (previous.Signature != pair.Value.Signature)
                    RecordSquareContentEvent("Square Content Changed", pair.Value, initial);
            }

            _squareContents.Clear();
            foreach (var pair in current) _squareContents[pair.Key] = pair.Value;
        }

        private bool TrySnapshotSquareContent(MonoBehaviour behaviour, out SquareContentSnapshot snapshot)
        {
            snapshot = null;
            if (!behaviour || !_board.TryWorldToCell(behaviour.transform.position, out var cell)) return false;

            string kind;
            string label;
            string color;
            string state = "active";
            string effect = "";
            int? rangeCells = null;
            float? fullnessPercent = null;
            float? healthPercent = null;

            switch (behaviour)
            {
                case SpecialInteractController slot:
                    kind = "utility-slot";
                    label = slot.AcceptedType == PlaceableType.Any ? "Utility Slot" : $"{slot.AcceptedType} Slot";
                    color = "#fbbf24";
                    state = slot.IsOccupied ? "occupied" : "available";
                    break;
                case BuffDebuffTileController tile:
                    kind = tile.Kind == BuffDebuffKind.Buff ? "buff" : "debuff";
                    label = tile.Kind == BuffDebuffKind.Buff ? "Buff Square" : "Debuff Square";
                    color = tile.Kind == BuffDebuffKind.Buff ? "#4ade80" : "#f87171";
                    effect = string.Join(", ", tile.Effects.Where(item => item).Select(item => item.name));
                    break;
                case TowerController:
                    kind = "tower";
                    label = "Tower";
                    color = "#38bdf8";
                    break;
                case WasteSifter sifter:
                    kind = "waste-sifter";
                    label = "Waste Sifter";
                    color = "#84cc16";
                    if (sifter.maxHealth > 0f)
                        healthPercent = Mathf.Clamp01(sifter.health / sifter.maxHealth) * 100f;
                    break;
                case Cesspit cesspit:
                    kind = "cesspit";
                    label = "Cesspit";
                    color = "#a16207";
                    state = cesspit.IsSealed ? "sealed" : "active";
                    if (cesspit.maxFullness > 0f)
                        fullnessPercent = Mathf.Clamp01(cesspit.fullness / cesspit.maxFullness) * 100f;
                    break;
                case TreatmentTank:
                    kind = "treatment-tank";
                    label = "Treatment Tank";
                    color = "#14b8a6";
                    break;
                case LimeSprinkler:
                    kind = "lime-sprinkler";
                    label = "Lime Sprinkler";
                    color = "#bef264";
                    rangeCells = 1;
                    break;
                case PathSplitter:
                    kind = "path-splitter";
                    label = "Path Splitter";
                    color = "#c084fc";
                    break;
                default:
                    return false;
            }

            snapshot = new SquareContentSnapshot
            {
                Id = behaviour.GetEntityId(),
                Target = behaviour,
                Cell = cell,
                Kind = kind,
                Label = label,
                Color = color,
                State = state,
                Effect = effect,
                RangeCells = rangeCells,
                FullnessPercent = fullnessPercent,
                HealthPercent = healthPercent
            };
            return true;
        }

        private void RecordSquareContentEvent(string eventName, SquareContentSnapshot content, bool initial)
        {
            var data = SquareContentData(content, initial);
            trailhead?.AddEvent(eventName, data);
            TrackSquareContentInSummit(eventName, content, initial);
        }

        private Dictionary<string, string> SquareContentData(SquareContentSnapshot content, bool initial)
        {
            var data = new Dictionary<string, string>
            {
                { "content-id", content.Id.ToString() },
                { "content-kind", content.Kind },
                { "item", content.Label },
                { "cell", $"{content.Cell.x},{content.Cell.y}" },
                { "color", content.Color },
                { "state", content.State },
                { "initial", initial ? "true" : "false" },
                { "board-columns", _board ? _board.Columns.ToString(Invariant) : "0" },
                { "board-rows", _board ? _board.Rows.ToString(Invariant) : "0" }
            };
            if (!string.IsNullOrWhiteSpace(content.Effect)) data["effect"] = content.Effect;
            if (content.RangeCells.HasValue)
                data["range-cells"] = content.RangeCells.Value.ToString(Invariant);
            if (content.FullnessPercent.HasValue)
                data["fullness-percent"] = F(content.FullnessPercent.Value);
            if (content.HealthPercent.HasValue)
                data["health-percent"] = F(content.HealthPercent.Value);
            return data;
        }

        private void TrackSquareContentInSummit(string eventName, SquareContentSnapshot content, bool initial)
        {
            if (!summit || !summit.IsSessionActive) return;

            var measurements = new Dictionary<string, double>
            {
                { "cell_column", content.Cell.x },
                { "cell_row", content.Cell.y }
            };
            if (content.RangeCells.HasValue)
                measurements["range_cells"] = content.RangeCells.Value;
            if (content.FullnessPercent.HasValue)
                measurements["fullness_percent"] = content.FullnessPercent.Value;
            if (content.HealthPercent.HasValue)
                measurements["health_percent"] = content.HealthPercent.Value;

            TrackSummitEvent(eventName, SquareContentData(content, initial), measurements);

            if (eventName == "Square Content Removed")
                _summitSquareContents.Remove(content.Id);
            else
                _summitSquareContents.Add(content.Id);
        }

        private void HandlePlacementCompleted(IPlaceable item, GameObject placedObject)
        {
            if (placedObject)
                DiscoverSubjects();

            var data = new Dictionary<string, string>
            {
                { "item", item.DisplayName },
                { "placeable-type", item.PlaceableType.ToString() },
                { "infra-value", item.InfraValue.ToString(Invariant) }
            };
            if (placedObject)
                foreach (var pair in SubjectLocation(placedObject.transform)) data[pair.Key] = pair.Value;

            RecordEvent(item.PlaceableType == PlaceableType.Targeted ? "Targeted Action" : "Item Placed", data,
                new Dictionary<string, double> { { "infrastructure_value", item.InfraValue } });
        }

        private void HandleCardPhase() => RecordEvent("Card Phase Entered");

        private void HandleTowerPhase()
        {
            RecordEvent("Tower Phase Entered");
            StartCoroutine(RecordCompletedPathPatterns());
        }

        private IEnumerator RecordCompletedPathPatterns()
        {
            // EntitySpawner rebuilds its WaypointPath synchronously after the tower-phase
            // event. Wait one frame so the ordered route cells are available here.
            yield return null;
            if (!_board) yield break;

            var paths = FindObjectsByType<WaypointPath>(FindObjectsInactive.Exclude)
                .Where(path => path.UsesBoard(_board))
                .Distinct()
                .ToArray();
            var pathIndex = 0;
            foreach (var path in paths)
            {
                if (!path.IsValid && !path.Rebuild()) continue;
                RecordCompletedRoute(path.PathCells, pathIndex, "main");
                if (path.AlternatePathCells.Count > 0)
                    RecordCompletedRoute(path.AlternatePathCells, pathIndex, "alternate");
                pathIndex++;
            }
        }

        private void RecordCompletedRoute(IReadOnlyList<Vector2Int> route, int pathIndex, string variant)
        {
            if (route == null || route.Count == 0 || !_board) return;

            var cells = string.Join(";", route.Select(cell => $"{cell.x},{cell.y}"));
            var occupied = string.Join(";", _board.PlacedPieces
                .SelectMany(piece => piece.cells)
                .Distinct()
                .OrderBy(cell => cell.y)
                .ThenBy(cell => cell.x)
                .Select(cell => $"{cell.x},{cell.y}"));
            var controller = GameMaster.Instance ? GameMaster.Instance.turnController : null;
            RecordEvent("Path Pattern Completed", new Dictionary<string, string>
            {
                { "route-signature", cells },
                { "cells", cells },
                { "occupied-cells", occupied },
                { "route-variant", variant },
                { "path-index", pathIndex.ToString(Invariant) },
                { "round", controller ? controller.currentTurn.ToString(Invariant) : "0" },
                { "board-columns", _board.Columns.ToString(Invariant) },
                { "board-rows", _board.Rows.ToString(Invariant) }
            }, new Dictionary<string, double>
            {
                { "route_length", route.Count },
                { "occupied_cells", _board.PlacedPieces.SelectMany(piece => piece.cells).Distinct().Count() }
            });
        }

        private void HandleLevelChanged(int level) => RecordEvent("Level Changed",
            new Dictionary<string, string> { { "level", level.ToString(Invariant) } },
            new Dictionary<string, double> { { "level", level } });

        private void HandleGameLost() => FinishSession("Game Lost");

        private void HandleIssueReachedEnd(IssueObject issue) => RecordEvent("Issue Reached End",
            SubjectLocation(issue), new Dictionary<string, double> { { "process_cost", issue.ProcessCost } });

        private void HandlePipeBroken(IssueObject issue) => RecordEvent("Pipe Broken",
            SubjectLocation(issue), new Dictionary<string, double> { { "process_cost", issue.ProcessCost } });

        private void RecordEvent(string eventName, Dictionary<string, string> data = null,
            Dictionary<string, double> measurements = null)
        {
            trailhead?.AddEvent(eventName, data);
            TrackSummitEvent(eventName, data, measurements);
        }

        private void TrackSummitEvent(string eventName, Dictionary<string, string> data = null,
            Dictionary<string, double> measurements = null)
        {
            if (!summit || !summit.IsSessionActive) return;

            var properties = data?.ToDictionary(pair => pair.Key, pair => (object)pair.Value);
            summit.Track(eventName, properties, tags: new[] { "waste-board", "replay" }, measurements: measurements);
        }

        private Dictionary<string, string> SubjectLocation(Component component) =>
            component ? SubjectLocation(component.transform) : new Dictionary<string, string>();

        private Dictionary<string, string> SubjectLocation(Transform target)
        {
            var data = new Dictionary<string, string>
            {
                { "position", V(target.position) },
                { "rotation", Q(target.rotation) }
            };
            if (_board && _board.TryWorldToCell(target.position, out var cell))
                data["cell"] = $"{cell.x},{cell.y}";
            return data;
        }

        private static PathSnapshot Snapshot(PathBuildBoard.PlacedPathPiece piece) => new()
        {
            Id = piece.id,
            Length = piece.length,
            Orientation = piece.orientation.ToString(),
            InfraValue = piece.infraValue,
            Cells = new List<Vector2Int>(piece.cells)
        };

        private static Vector3 Measure(Component component)
        {
            var renderer = component.GetComponentInChildren<Renderer>();
            if (!renderer) return Vector3.one * 0.75f;
            var size = renderer.bounds.size;
            return new Vector3(Mathf.Max(0.1f, size.x), Mathf.Max(0.1f, size.y), Mathf.Max(0.1f, size.z));
        }

        private static string GetOrCreateAnonymousId()
        {
            if (PlayerPrefs.HasKey(AnonymousIdKey)) return PlayerPrefs.GetString(AnonymousIdKey);
            var id = Guid.NewGuid().ToString();
            PlayerPrefs.SetString(AnonymousIdKey, id);
            PlayerPrefs.Save();
            return id;
        }

        private static bool IsConfigured(string url, string key) =>
            !string.IsNullOrWhiteSpace(key) &&
            !string.IsNullOrWhiteSpace(url) &&
            !url.Contains("your-", StringComparison.OrdinalIgnoreCase);

        private static string F(float value) => value.ToString("R", Invariant);
        private static string V(Vector3 value) => $"{F(value.x)},{F(value.y)},{F(value.z)}";
        private static string Q(Quaternion value) => $"{F(value.x)},{F(value.y)},{F(value.z)},{F(value.w)}";
    }
}
