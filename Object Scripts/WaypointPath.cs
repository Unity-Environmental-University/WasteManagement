using System.Collections.Generic;
using _project.Scripts.Core;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     Defines a single traversal route for <see cref="IssueObject" /> enemies to follow.
    ///     The path is dynamically built from pieces placed on a <see cref="PathBuildBoard" />,
    ///     with optional fixed start/end transforms bookending the player-built section.
    ///     Call <see cref="Rebuild" /> before enemies spawn (e.g., at wave start) to construct
    ///     the waypoint list from currently placed pieces.
    ///     Internally uses BREADTH-FIRST SEARCH through occupied grid cells, treating the
    ///     whole placed-piece network as a graph. This means:
    ///     - T-junctions and branches work correctly
    ///     - Corners/turns work as long as pieces are orthogonally adjacent (share an edge)
    ///     - The SHORTEST route from start to end (measured in cell count) is always chosen
    ///     - Disconnected pieces are simply not part of the path
    /// </summary>
    [ExecuteAlways]
    public class WaypointPath : MonoBehaviour
    {
        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.right,
            Vector2Int.left,
            Vector2Int.up,
            Vector2Int.down
        };

        [SerializeField] private bool recyclingDestination;

        // The board whose occupied cells form the graph that BFS traverses.
        [Tooltip("Source of placed path pieces. The path is rebuilt from these at wave start.")] [SerializeField]
        private PathBuildBoard pathBuildBoard;

        // Fixed spawn-side anchor. When set, it becomes the FIRST waypoint in the list.
        // It's nearest grid cell is the BFS START node.
        [Tooltip("Optional start point prepended before the first placed piece.")] [SerializeField]
        private Transform startPoint;

        [SerializeField] private Transform leftOrigin;

        [SerializeField] private Transform rightOrigin;

        // Fixed goal-side anchor. When set, it becomes the LAST waypoint in the list.
        // It's nearest grid cell is the BFS GOAL node.
        [Tooltip("Optional end point appended after the last placed piece.")] [SerializeField]
        private Transform endPoint;

        [Header("Live Build Preview")]
        [Tooltip("Draw the route the pathfinder can currently follow while the player builds.")]
        [SerializeField]
        private bool showLivePreview = true;

        [SerializeField] private Color completePreviewColor = new(0.35f, 0.9f, 1f, 0.9f);
        [SerializeField] private Color incompletePreviewColor = new(1f, 0.7f, 0.2f, 0.9f);

        // The water shader multiplies these into its blue, so they read darker and cooler on screen
        // than they look here; warm hues need a strong red to stay distinct.
        [Tooltip("Colors for the fork-to-rejoin branches splitters create, one per splitter in route order, " +
                 "cycling when there are more splitters than colors.")]
        [SerializeField]
        private Color[] branchPreviewColors =
        {
            new(1f, 0.60f, 0.16f, 0.9f),    // amber
            new(0.68f, 0.52f, 0.96f, 0.9f), // violet
            new(0.52f, 0.88f, 0.42f, 0.9f), // sage
            new(0.92f, 0.46f, 0.56f, 0.9f), // rose
            new(0.86f, 0.80f, 0.32f, 0.9f)  // gold
        };

        [Tooltip("Tube width when the board has no authored preview LineRenderer to copy width from.")]
        [SerializeField]
        [Min(0.01f)]
        private float previewWidth = 0.12f;

        [SerializeField] [Min(0f)] private float previewHeightOffset = 0.2f;

        [Tooltip("Lift the source-to-start streams above surrounding geometry so the Game camera can see them.")]
        [SerializeField]
        [Min(0f)]
        private float originPreviewHeightOffset = 1.5f;

        // Upper bound on routes, so a board crowded with splitters can't multiply them without limit.
        private const int MaxRoutes = 32;

        // Which route a splitter sends an issue down: keyed by the route the issue is on and the
        // splitter's cell. Only Rebuild() changes it, so it always matches _routes while issues travel.
        private readonly Dictionary<(int route, Vector2Int cell), int> _branchRoutes = new();

        // One water tube per distinct splitter branch the live preview draws.
        private readonly List<PathWaterTube> _branchLivePreviews = new();

        // The routes last shown by the live preview, main first. Unlike _routes they stay current
        // while the player edits the board, so placement can tell which way water flows through a cell.
        private readonly List<Route> _previewRoutes = new();

        // Every route issues can follow after the last Rebuild(): the main route first, then each
        // splitter branch. Empty while the path is invalid.
        private readonly List<Route> _routes = new();

        // Cells that ARE part of the final path. Cached for gizmo color-coding.
        private readonly List<Vector2Int> _pathCells = new();

        // Reused each refresh to hand the current route's world-space points to the tube builder
        // without allocating a fresh list every frame the preview updates.
        private readonly List<Vector3> _previewPoints = new();

        // Board cells holding an enabled splitter, as seen by the last route search.
        private readonly List<Vector2Int> _splitterCells = new();

        // Cells visited by BFS but NOT part of the final path. Used only for gizmo
        // visualization so the player can see which placed pieces were ignored.
        private readonly List<Vector2Int> _unusedCells = new();

        // The final ordered list of world-space positions enemies traverse.
        // Built by Rebuild() — do not modify directly.
        private readonly List<Vector3> _waypoints = new();
        private bool _hasOriginPreviewPositions;
        private Vector3 _lastLeftOriginPosition;
        private Vector3 _lastRightOriginPosition;
        private Vector3 _lastStartPosition;
        private PathWaterTube _leftOriginPreview;

        private PathWaterTube _livePreview;
        private PathWaterTube _rightOriginPreview;
        public bool RecyclingDestination => recyclingDestination;

        public PathKind PathKind => recyclingDestination
            ? PathKind.RecyclingBelt
            : PathKind.Pipe;

        /// <summary>
        ///     The total number of waypoints in the current path. Used by IssueObject
        ///     to detect when it has reached the end of the route.
        /// </summary>
        public int Count => _waypoints.Count;

        /// <summary>Routes issues can follow: the main route (index 0) plus one per splitter branch.</summary>
        public int RouteCount => _routes.Count;

        public bool HasAlternateRoute => _routes.Count > 1;
        public IReadOnlyList<Vector2Int> PathCells => _pathCells;

        /// <summary>The first branch's cells, or empty when the path has no branch.</summary>
        public IReadOnlyList<Vector2Int> AlternatePathCells => GetRouteCells(1);
        public Transform Destination => endPoint;

        public bool IsValid { get; private set; }
        public string InvalidReason { get; private set; }

        // Live-preview refreshes are driven by the board (pipe edits and splitter changes), which
        // also reaches the intentionally inactive waypoint containers, so nothing is polled here.
        private void OnEnable()
        {
            if (Application.isPlaying)
                RefreshLivePreview();

            // Also runs in Edit mode so the source streams are visible while authoring the scene.
            RefreshOriginPreviewsIfMoved();
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.ObjectChangeEvents.changesPublished += HandleEditorObjectChanges;
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.ObjectChangeEvents.changesPublished -= HandleEditorObjectChanges;
#endif
            if (pathBuildBoard && !recyclingDestination)
                pathBuildBoard.ClearPriorityVisualPath();
            if (_leftOriginPreview) _leftOriginPreview.Clear();
            if (_rightOriginPreview) _rightOriginPreview.Clear();
            _hasOriginPreviewPositions = false;
            _splitterCells.Clear();
            _previewRoutes.Clear();
        }

        /// <summary>
        ///     Draws the route as gizmos in the Scene view:
        ///     - YELLOW lines between consecutive waypoints (the route)
        ///     - GREEN cubes for cells ON the path
        ///     - RED cubes for placed cells that are NOT reachable / not on the shortest path
        /// </summary>
        private void OnDrawGizmos()
        {
            DrawOriginPreview(leftOrigin, new Color(0.25f, 0.8f, 1f, 0.9f));
            DrawOriginPreview(rightOrigin, new Color(0.35f, 1f, 0.55f, 0.9f));

            // Draw the actual route
            if (_waypoints.Count >= 2)
            {
                Gizmos.color = Color.yellow;
                for (var i = 0; i < _waypoints.Count - 1; i++)
                    Gizmos.DrawLine(_waypoints[i], _waypoints[i + 1]);
            }

            if (!pathBuildBoard) return;

            // Path cells — green markers confirm these cells are traversed
            Gizmos.color = Color.green;
            foreach (var cell in _pathCells)
                Gizmos.DrawWireCube(pathBuildBoard.GetPathWaypointPosition(cell, PathKind), Vector3.one * 0.3f);

            // Unused cells — red markers indicate placed pieces that were ignored
            // (either unreachable from the start or off the shortest route)
            Gizmos.color = Color.red;
            foreach (var cell in _unusedCells)
                Gizmos.DrawWireCube(pathBuildBoard.GetPathWaypointPosition(cell, PathKind), Vector3.one * 0.3f);
        }

        // Preview tuning fields (color/width/height) only take effect on the next RefreshLivePreview,
        // which otherwise only fires on board/splitter events — force one so Inspector tweaks during
        // Play mode are visible immediately instead of appearing to do nothing.
        // Deferred because OnValidate may not create GameObjects, add components or reparent.
        private void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= RefreshAfterValidate;
            UnityEditor.EditorApplication.delayCall += RefreshAfterValidate;
#endif
        }

        private void RefreshAfterValidate()
        {
            if (!this) return;

            if (Application.isPlaying)
                RefreshLivePreview();

            _hasOriginPreviewPositions = false;
            RefreshOriginPreviewsIfMoved();
        }

        private void CollectSplitterCells(List<Vector2Int> cells)
        {
            cells.Clear();
            if (!pathBuildBoard || PathKind != PathKind.Pipe) return;

            foreach (var splitter in PathSplitter.Live)
                if (splitter && splitter.isActiveAndEnabled &&
                    pathBuildBoard.TryWorldToCell(splitter.transform.position, out var cell))
                    cells.Add(cell);
        }

#if UNITY_EDITOR
        // Edit mode: redraw the source streams after an origin or the start point is moved.
        private void HandleEditorObjectChanges(ref UnityEditor.ObjectChangeEventStream stream)
        {
            if (this) RefreshOriginPreviewsIfMoved();
        }
#endif

        private void RefreshOriginPreviewsIfMoved()
        {
            if (!leftOrigin && !rightOrigin) return;
            var leftPosition = leftOrigin ? leftOrigin.position : Vector3.zero;
            var rightPosition = rightOrigin ? rightOrigin.position : Vector3.zero;
            var startPosition = startPoint ? startPoint.position : Vector3.zero;
            if (_hasOriginPreviewPositions && leftPosition == _lastLeftOriginPosition &&
                rightPosition == _lastRightOriginPosition && startPosition == _lastStartPosition) return;

            _lastLeftOriginPosition = leftPosition;
            _lastRightOriginPosition = rightPosition;
            _lastStartPosition = startPosition;
            _hasOriginPreviewPositions = true;

            RefreshOriginPreview(leftOrigin, ref _leftOriginPreview, completePreviewColor);
            RefreshOriginPreview(rightOrigin, ref _rightOriginPreview, completePreviewColor);
        }

        private void RefreshOriginPreview(Transform origin, ref PathWaterTube tube, Color color)
        {
            if (!origin || !startPoint || !isActiveAndEnabled)
            {
                if (tube) tube.Clear();
                return;
            }

            var host = pathBuildBoard ? pathBuildBoard.transform : transform;
            if (!tube)
            {
                var previewObject = FindOrCreateChild(host, origin.name + " Water Preview");
                if (!previewObject.TryGetComponent(out tube))
                    tube = previewObject.gameObject.AddComponent<PathWaterTube>();
                tube.Configure(null, null, previewWidth);
            }

            _previewPoints.Clear();
            _previewPoints.Add(GetOriginPreviewPosition(origin.position));
            _previewPoints.Add(GetOriginPreviewPosition(startPoint.position));
            tube.SetPath(_previewPoints, GetPreviewUp(), color);
        }

        /// <summary>
        ///     Shows where water enters the route, even before a live path has been built.
        ///     The arrow points from the origin toward the fixed start anchor (or first waypoint).
        /// </summary>
        private void DrawOriginPreview(Transform origin, Color color)
        {
            if (!origin) return;

            var previewOrigin = GetOriginPreviewPosition(origin.position);
            var target = GetOriginPreviewPosition(startPoint
                ? startPoint.position
                : _waypoints.Count > 0
                    ? _waypoints[0]
                    : origin.position);
            var direction = target - previewOrigin;
            if (direction.sqrMagnitude < 0.0001f) return;

            Gizmos.color = color;
            Gizmos.DrawLine(previewOrigin, target);
            Gizmos.DrawWireSphere(previewOrigin, 0.12f);

            var arrowLength = Mathf.Min(0.45f, direction.magnitude * 0.25f);
            var arrowDirection = direction.normalized;
            var arrowSide = Vector3.Cross(arrowDirection, Vector3.up);
            if (arrowSide.sqrMagnitude < 0.0001f)
                arrowSide = Vector3.Cross(arrowDirection, Vector3.right);
            arrowSide.Normalize();
            var arrowBase = target - arrowDirection * arrowLength;
            Gizmos.DrawLine(target, arrowBase + arrowSide * arrowLength * 0.45f);
            Gizmos.DrawLine(target, arrowBase - arrowSide * arrowLength * 0.45f);
        }

        /// <summary>
        ///     Returns the world-space position of the waypoint at the given index.
        ///     Called by IssueObject each frame to get its current movement target.
        /// </summary>
        public Vector3 GetPosition(int index)
        {
            return _waypoints[index];
        }

        public int GetWaypointCount(int routeIndex)
        {
            return GetRouteWaypoints(routeIndex).Count;
        }

        public Vector3 GetPosition(int routeIndex, int waypointIndex)
        {
            return GetRouteWaypoints(routeIndex)[waypointIndex];
        }

        /// <summary>A route's cells, or empty when there is no such route.</summary>
        public IReadOnlyList<Vector2Int> GetRouteCells(int routeIndex)
        {
            return routeIndex >= 0 && routeIndex < _routes.Count
                ? _routes[routeIndex].Cells
                : System.Array.Empty<Vector2Int>();
        }

        // An unknown route index falls back to the main route.
        private List<Vector3> GetRouteWaypoints(int routeIndex)
        {
            return routeIndex > 0 && IsRouteIndex(routeIndex) ? _routes[routeIndex].Waypoints : _waypoints;
        }

        /// <summary>Route 0 always counts, so an issue can sit on the main route before the path is built.</summary>
        public bool IsRouteIndex(int routeIndex)
        {
            return routeIndex == 0 || (routeIndex > 0 && routeIndex < _routes.Count);
        }

        /// <summary>
        ///     Returns whether two issues may merge at their current route progress. Issues on
        ///     different routes stay isolated until both are targeting the shared route suffix
        ///     where their branches have rejoined.
        /// </summary>
        public bool CanRoutesMergeAtProgress(int firstRouteIndex, int firstWaypointIndex,
            int secondRouteIndex, int secondWaypointIndex)
        {
            if (!IsRouteIndex(firstRouteIndex) || !IsRouteIndex(secondRouteIndex)) return false;
            if (firstRouteIndex == secondRouteIndex) return true;

            var first = GetRouteWaypoints(firstRouteIndex);
            var second = GetRouteWaypoints(secondRouteIndex);
            var sharedWaypointCount = GetSharedSuffixWaypointCount(first, second);
            if (sharedWaypointCount == 0) return false;

            return firstWaypointIndex >= first.Count - sharedWaypointCount &&
                   secondWaypointIndex >= second.Count - sharedWaypointCount;
        }

        private static int GetSharedSuffixWaypointCount(List<Vector3> first, List<Vector3> second)
        {
            var sharedCount = 0;
            var firstIndex = first.Count - 1;
            var secondIndex = second.Count - 1;
            while (firstIndex >= 0 && secondIndex >= 0 &&
                   Vector3.SqrMagnitude(first[firstIndex] - second[secondIndex]) < 0.0001f)
            {
                sharedCount++;
                firstIndex--;
                secondIndex--;
            }

            return sharedCount;
        }

        public int FindClosestWaypointIndex(int routeIndex, Vector3 position, int minimumIndex = 0)
        {
            var route = GetRouteWaypoints(routeIndex);
            if (route.Count == 0) return 0;

            var closestIndex = Mathf.Clamp(minimumIndex, 0, route.Count - 1);
            var closestDistance = float.PositiveInfinity;
            for (var i = closestIndex; i < route.Count; i++)
            {
                var distance = Vector3.SqrMagnitude(position - route[i]);
                if (distance >= closestDistance) continue;

                closestDistance = distance;
                closestIndex = i;
            }

            return closestIndex;
        }

        /// <summary>True when some route forks at the cell holding <paramref name="worldPosition" />.</summary>
        public bool IsSplitPoint(Vector3 worldPosition)
        {
            if (!pathBuildBoard || !pathBuildBoard.TryWorldToCell(worldPosition, out var cell)) return false;

            foreach (var key in _branchRoutes.Keys)
                if (key.cell == cell)
                    return true;
            return false;
        }

        /// <summary>
        ///     Finds the branch an issue on <paramref name="routeIndex" /> takes when a splitter at
        ///     <paramref name="worldPosition" /> diverts it. False when that route doesn't fork there.
        /// </summary>
        public bool TryGetBranchRoute(int routeIndex, Vector3 worldPosition, out int branchRouteIndex)
        {
            branchRouteIndex = -1;
            return pathBuildBoard && pathBuildBoard.TryWorldToCell(worldPosition, out var cell) &&
                   _branchRoutes.TryGetValue((routeIndex, cell), out branchRouteIndex);
        }

        /// <summary>
        ///     Returns +1 when water on the live route passes through the cell at
        ///     <paramref name="worldPosition" /> travelling along <paramref name="axis" />, or -1
        ///     when it travels against it. False when no live route crosses that cell along the axis.
        /// </summary>
        public bool TryGetFlowSign(Vector3 worldPosition, Vector3 axis, out float sign)
        {
            sign = 0f;
            if (!pathBuildBoard || !pathBuildBoard.TryWorldToCell(worldPosition, out var cell)) return false;

            foreach (var route in _previewRoutes)
                if (TryGetFlowSign(route.Cells, route.Complete, cell, axis, out sign))
                    return true;
            return false;
        }

        private bool TryGetFlowSign(List<Vector2Int> route, bool complete, Vector2Int cell, Vector3 axis,
            out float sign)
        {
            sign = 0f;
            var index = route.IndexOf(cell);
            if (index < 0) return false;

            var current = pathBuildBoard.GetPathWaypointPosition(cell, PathKind);
            var previous = index > 0
                ? pathBuildBoard.GetPathWaypointPosition(route[index - 1], PathKind)
                : startPoint
                    ? startPoint.position
                    : current;
            var next = index < route.Count - 1
                ? pathBuildBoard.GetPathWaypointPosition(route[index + 1], PathKind)
                : complete && endPoint
                    ? endPoint.position
                    : current;

            // Spanning previous→next keeps corner cells correct: the leg along the axis sets the sign.
            var along = Vector3.Dot(next - previous, axis);
            if (Mathf.Abs(along) < 0.0001f) return false;

            sign = Mathf.Sign(along);
            return true;
        }

        public bool UsesBoard(PathBuildBoard board)
        {
            return pathBuildBoard == board;
        }

        /// <summary>
        ///     Rebuilds the waypoint list using BFS through occupied grid cells.
        ///     Algorithm:
        ///     1. Determine START candidates from occupied cells edge-adjacent to
        ///     <see cref="startPoint" /> and GOAL candidates from occupied cells
        ///     edge-adjacent to <see cref="endPoint" />.
        ///     2. BFS outward from every START candidate through 4-way-adjacent occupied cells,
        ///     recording the parent of each visited cell so we can reconstruct the path.
        ///     3. If any GOAL was reached, walk parents back to build the cell sequence.
        ///     4. Convert cells to world positions and bookend with start/end Transforms.
        ///     Entities therefore follow the SHORTEST chain of orthogonally adjacent occupied
        ///     cells from start to goal. Diagonal adjacency is not allowed — pieces must share
        ///     an edge. T-junctions and branches work naturally because BFS considers every
        ///     occupied cell, not just piece endpoints.
        /// </summary>
        public bool Rebuild()
        {
            _waypoints.Clear();
            _routes.Clear();
            _branchRoutes.Clear();
            _pathCells.Clear();
            _unusedCells.Clear();
            IsValid = false;
            InvalidReason = null;

            if (!pathBuildBoard)
            {
                InvalidReason = "Missing path build board.";
                return FailRebuild();
            }

            if (!startPoint || !endPoint)
            {
                InvalidReason = "Missing lower or upper endpoint.";
                return FailRebuild();
            }

            // RESOLVE START AND GOAL CANDIDATES.
            // Endpoint markers are strict: a placed path cell must share an edge with each
            // marker square. The middle of the route remains normal occupied-cell BFS.
            var starts = GetOccupiedEndpointNeighbors(startPoint);
            if (starts.Count == 0)
            {
                InvalidReason = "No placed path cell touches the lower endpoint square.";
                return FailRebuild();
            }

            var goals = GetOccupiedEndpointNeighbors(endPoint);
            if (goals.Count == 0)
            {
                InvalidReason = "No placed path cell touches the upper endpoint square.";
                return FailRebuild();
            }

            // RUN BFS: returns the ordered list of cells from a lower candidate to an upper
            // candidate, or null if no connected occupied route exists.
            var cellPath = BreadthFirstSearch(starts, goals);

            if (cellPath == null)
            {
                InvalidReason = "Placed path does not connect lower endpoint to upper endpoint.";
                return FailRebuild();
            }

            _waypoints.Add(startPoint.position);

            // CONVERT CELL PATH TO WAYPOINTS
            foreach (var cell in cellPath)
            {
                _pathCells.Add(cell);
                _waypoints.Add(pathBuildBoard.GetPathWaypointPosition(cell, PathKind));
            }

            // Bucket remaining occupied cells as "unused" for the gizmo
            RecordUnusedCells(cellPath);

            // Bookend with endPoint
            _waypoints.Add(endPoint.position);

            _routes.Add(new Route(_pathCells, _waypoints) { Complete = true });
            CollectSplitterCells(_splitterCells);
            AddSplitterBranches(_routes, _branchRoutes, goals, false);
            if (_routes.Count == 1) AddFirstForkBranch(goals);

            for (var i = 1; i < _routes.Count; i++)
            {
                var route = _routes[i];
                route.Waypoints.Add(startPoint.position);
                foreach (var cell in route.Cells)
                    route.Waypoints.Add(pathBuildBoard.GetPathWaypointPosition(cell, PathKind));
                route.Waypoints.Add(endPoint.position);
            }

            IsValid = true;
            InvalidReason = null;
            RefreshLivePreview();
            return true;
        }

        /// <summary>
        ///     Draws the route available right now in the Game view. A complete route uses
        ///     the normal BFS result. An incomplete route uses the same search and ends at
        ///     the reachable cell with the smallest grid distance to the goal. Every splitter on a
        ///     shown route, branches included, also shows the branch leaving its cell, however far
        ///     that branch has been built.
        /// </summary>
        public void RefreshLivePreview()
        {
            CollectSplitterCells(_splitterCells);
            _previewRoutes.Clear();

            var tube = GetLivePreviewTube();
            if (tube) tube.Clear();
            foreach (var branchTube in _branchLivePreviews)
                if (branchTube)
                    branchTube.Clear();

            if (!pathBuildBoard || !startPoint || !endPoint)
            {
                if (pathBuildBoard && !recyclingDestination) pathBuildBoard.ClearPriorityVisualPath();
                return;
            }

            var starts = GetOccupiedEndpointNeighbors(startPoint);
            if (starts.Count == 0)
            {
                if (!recyclingDestination) pathBuildBoard.ClearPriorityVisualPath();
                return;
            }

            var goals = GetOccupiedEndpointNeighbors(endPoint);
            var previewCells = FindPreviewPath(starts, goals, out var complete);
            if (previewCells == null || previewCells.Count == 0)
            {
                if (!recyclingDestination) pathBuildBoard.ClearPriorityVisualPath();
                return;
            }

            var main = new Route { Complete = complete };
            main.Cells.AddRange(previewCells);
            _previewRoutes.Add(main);
            AddSplitterBranches(_previewRoutes, null, goals, true);

            if (!recyclingDestination)
                pathBuildBoard.SetPriorityVisualPath(previewCells, startPoint.position,
                    complete ? endPoint.position : null, GetBranchCells(_previewRoutes));

            if (!showLivePreview || !tube) return;

            _previewPoints.Clear();
            _previewPoints.Add(GetPreviewPosition(startPoint.position));
            foreach (var cell in previewCells)
                _previewPoints.Add(GetPreviewPosition(pathBuildBoard.GetPathWaypointPosition(cell, PathKind)));
            if (complete)
                _previewPoints.Add(GetPreviewPosition(endPoint.position));
            tube.SetPath(_previewPoints, GetPreviewUp(), complete ? completePreviewColor : incompletePreviewColor);

            // Routes that reach the same splitter from different directions share its branch once
            // they have rejoined, so each distinct stretch of pipe is drawn only once.
            var drawnBranches = new HashSet<(Vector2Int fork, Vector2Int exit, Vector2Int end)>();
            var splitterColors = new Dictionary<Vector2Int, int>();
            var branchTubeCount = 0;
            for (var i = 1; i < _previewRoutes.Count; i++)
            {
                var route = _previewRoutes[i];
                var lastIndex = route.JoinIndex >= 0 ? route.JoinIndex : route.Cells.Count - 1;
                if (lastIndex <= route.ForkIndex ||
                    !drawnBranches.Add((route.ForkCell, route.Cells[route.ForkIndex + 1], route.Cells[lastIndex])))
                    continue;

                var branchTube = GetBranchLivePreviewTube(branchTubeCount++);
                if (!branchTube) return;

                CollectBranchPoints(route, lastIndex, _previewPoints);

                // The thinner branch stream would sink below the pipe floor the main stream still
                // breaks through, so raise it until both water surfaces are level.
                var surfaceLift = GetPreviewUp() * Mathf.Max(0f, tube.StartRadius - branchTube.StartRadius);
                for (var p = 0; p < _previewPoints.Count; p++)
                    _previewPoints[p] += surfaceLift;

                if (!splitterColors.TryGetValue(route.ForkCell, out var colorIndex))
                {
                    colorIndex = splitterColors.Count;
                    splitterColors.Add(route.ForkCell, colorIndex);
                }

                branchTube.SetPath(_previewPoints, GetPreviewUp(), GetBranchPreviewColor(colorIndex));
            }
        }

        private static IEnumerable<IReadOnlyList<Vector2Int>> GetBranchCells(List<Route> routes)
        {
            for (var i = 1; i < routes.Count; i++)
                yield return routes[i].Cells;
        }

        private Color GetBranchPreviewColor(int colorIndex)
        {
            return branchPreviewColors is { Length: > 0 }
                ? branchPreviewColors[colorIndex % branchPreviewColors.Length]
                : incompletePreviewColor;
        }

        /// <summary>
        ///     Displays only the distinct portion of a branch: from its splitter to the cell where it
        ///     rejoins earlier pipe, so the stream visibly leaves and rejoins the route it splits from.
        ///     A branch that reaches the goal on its own runs on to the endpoint instead.
        /// </summary>
        private void CollectBranchPoints(Route route, int lastIndex, List<Vector3> points)
        {
            points.Clear();
            for (var i = route.ForkIndex; i <= lastIndex; i++)
                points.Add(GetPreviewPosition(pathBuildBoard.GetPathWaypointPosition(route.Cells[i], PathKind)));
            if (route.JoinIndex < 0 && route.Complete)
                points.Add(GetPreviewPosition(endPoint.position));
        }

        private Vector3 GetPreviewUp()
        {
            return pathBuildBoard ? pathBuildBoard.transform.up : Vector3.up;
        }

        private Vector3 GetPreviewPosition(Vector3 worldPosition)
        {
            return worldPosition + GetPreviewUp() * previewHeightOffset;
        }

        private Vector3 GetOriginPreviewPosition(Vector3 worldPosition)
        {
            return worldPosition + GetPreviewUp() * originPreviewHeightOffset;
        }

        private PathWaterTube GetLivePreviewTube()
        {
            return GetPreviewTube("Live Path Preview", ref _livePreview, previewWidth);
        }

        // The first branch keeps the original "Alternate Path Preview" name, so an authored
        // LineRenderer under it still styles the branches.
        private PathWaterTube GetBranchLivePreviewTube(int index)
        {
            while (_branchLivePreviews.Count <= index) _branchLivePreviews.Add(null);

            var branchTube = _branchLivePreviews[index];
            var objectName = index == 0 ? "Alternate Path Preview" : $"Alternate Path Preview {index + 1}";
            branchTube = GetPreviewTube(objectName, ref branchTube, previewWidth * 0.85f);
            _branchLivePreviews[index] = branchTube;
            return branchTube;
        }

        /// <summary>
        ///     Returns the water tube for a preview route. If the board already has a child with this
        ///     name carrying a LineRenderer you tuned in the scene/prefab, its width, width curve and
        ///     material carry over to the tube so the authored look is kept; that ribbon stays disabled
        ///     and the tube is built on a child of it (a GameObject can hold only one Renderer). Without
        ///     one, the tube gets the fallback code defaults.
        /// </summary>
        private PathWaterTube GetPreviewTube(string objectName, ref PathWaterTube cachedTube, float width)
        {
            if (cachedTube) return cachedTube;
            if (!showLivePreview || !pathBuildBoard) return null;

            var previewObject = FindOrCreateChild(pathBuildBoard.transform,
                recyclingDestination ? "Recycling " + objectName : objectName);
            var authoredLine = previewObject.GetComponent<LineRenderer>();
            if (authoredLine) authoredLine.enabled = false;
            var host = authoredLine ? FindOrCreateChild(previewObject, "Water Tube") : previewObject;

            if (!host.TryGetComponent(out cachedTube))
                cachedTube = host.gameObject.AddComponent<PathWaterTube>();

            if (authoredLine)
                cachedTube.Configure(authoredLine.sharedMaterial, authoredLine.widthCurve,
                    authoredLine.widthMultiplier);
            else
                cachedTube.Configure(null, null, width);
            return cachedTube;
        }

        private static Transform FindOrCreateChild(Transform parent, string childName)
        {
            var child = parent.Find(childName);
            if (child) return child;

            child = new GameObject(childName).transform;
            child.SetParent(parent, false);
            return child;
        }

        private List<Vector2Int> FindPreviewPath(
            IReadOnlyList<Vector2Int> starts,
            IReadOnlyCollection<Vector2Int> goals,
            out bool complete)
        {
            complete = false;
            var goalSet = goals as HashSet<Vector2Int> ?? new HashSet<Vector2Int>(goals);
            var targetCell = pathBuildBoard.ClampToOutsideRing(pathBuildBoard.WorldToCellUnclamped(endPoint.position));
            var frontier = new Queue<Vector2Int>();
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var best = starts[0];
            var bestDistance = GridDistance(best, targetCell);

            foreach (var start in starts)
            {
                if (cameFrom.ContainsKey(start)) continue;
                frontier.Enqueue(start);
                cameFrom[start] = start;
            }

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                var distance = GridDistance(current, targetCell);
                if (distance < bestDistance)
                {
                    best = current;
                    bestDistance = distance;
                }

                if (goalSet.Contains(current))
                {
                    best = current;
                    complete = true;
                    break;
                }

                foreach (var direction in Directions)
                {
                    var next = current + direction;
                    if (cameFrom.ContainsKey(next) || !pathBuildBoard.IsOccupied(next, PathKind)) continue;
                    cameFrom[next] = current;
                    frontier.Enqueue(next);
                }
            }

            var path = new List<Vector2Int>();
            var node = best;
            while (cameFrom[node] != node)
            {
                path.Add(node);
                node = cameFrom[node];
            }

            path.Add(node);
            path.Reverse();
            return path;
        }

        private static int GridDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        // ============================================================
        // BFS IMPLEMENTATION
        // ============================================================

        /// <summary>
        ///     Runs breadth-first search over occupied cells. Returns the cell sequence
        ///     from any start to any goal (inclusive) along the shortest orthogonally-connected
        ///     route, or null if every goal is unreachable. With <paramref name="allowPartial" />
        ///     an unreachable goal instead yields the route to the farthest cell reached, so an
        ///     unfinished branch can still be drawn.
        /// </summary>
        private List<Vector2Int> BreadthFirstSearch(
            IReadOnlyList<Vector2Int> starts,
            IReadOnlyCollection<Vector2Int> goals,
            ISet<Vector2Int> blocked = null,
            bool allowPartial = false
        )
        {
            if (starts == null || starts.Count == 0 || goals == null)
                return null;
            if (goals.Count == 0 && !allowPartial)
                return null;

            var goalSet = goals as HashSet<Vector2Int> ?? new HashSet<Vector2Int>(goals);

            // FRONTIER: cells to explore next (FIFO queue gives shortest-path guarantee in BFS)
            var frontier = new Queue<Vector2Int>();

            // PARENT MAP: for each visited cell, remember which cell we came FROM.
            // This lets us reconstruct the path by walking backward from goal → start.
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();

            foreach (var start in starts)
            {
                if (blocked != null && blocked.Contains(start)) continue;
                if (!pathBuildBoard.IsOccupied(start, PathKind) || cameFrom.ContainsKey(start)) continue;
                frontier.Enqueue(start);
                cameFrom[start] = start;
            }

            Vector2Int? foundGoal = null;
            Vector2Int? farthest = null;

            // MAIN BFS LOOP: expand outward layer by layer
            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();

                // BFS dequeues in distance order, so the last cell visited is the farthest one.
                farthest = current;

                // Early exit: we reached any goal — no need to explore further
                if (goalSet.Contains(current))
                {
                    foundGoal = current;
                    break;
                }

                // Check all 4 neighbors
                foreach (var dir in Directions)
                {
                    var next = current + dir;

                    // Skip if: already visited, out of bounds, or not occupied
                    if (blocked != null && blocked.Contains(next)) continue;
                    if (cameFrom.ContainsKey(next)) continue;
                    if (!pathBuildBoard.IsOccupied(next, PathKind)) continue;

                    cameFrom[next] = current;
                    frontier.Enqueue(next);
                }
            }

            if (!foundGoal.HasValue && allowPartial)
                foundGoal = farthest;
            if (!foundGoal.HasValue) return null;

            // RECONSTRUCT PATH: walk the parent chain from goal back to its start
            var path = new List<Vector2Int>();
            var node = foundGoal.Value;
            while (cameFrom[node] != node)
            {
                path.Add(node);
                node = cameFrom[node];
            }

            path.Add(node);

            // We built the path goal → start; reverse to get start → goal
            path.Reverse();
            return path;
        }

        /// <summary>
        ///     Grows the route tree: every splitter on a route, past the point where that route itself
        ///     forked, gets a branch, and new branches are searched in turn, so a splitter on a branch
        ///     splits it again. A branch always forks further along than its parent did, so the search
        ///     ends. With <paramref name="allowUnfinished" /> a splitter whose branch doesn't reach the
        ///     goal yet still gets that branch as far as it has been built.
        /// </summary>
        private void AddSplitterBranches(List<Route> routes,
            Dictionary<(int route, Vector2Int cell), int> branchLookup,
            List<Vector2Int> goals, bool allowUnfinished)
        {
            if (PathKind != PathKind.Pipe) return;

            for (var routeIndex = 0; routeIndex < routes.Count; routeIndex++)
            {
                var route = routes[routeIndex];
                for (var forkIndex = route.ForkIndex + 1; forkIndex < route.Cells.Count; forkIndex++)
                {
                    if (routes.Count >= MaxRoutes) return;

                    var cell = route.Cells[forkIndex];
                    if (!_splitterCells.Contains(cell)) continue;

                    var branch = FindBranchFrom(routes, routeIndex, forkIndex, goals, false);
                    if (branch == null && allowUnfinished)
                        branch = FindBranchFrom(routes, routeIndex, forkIndex, goals, true);
                    if (branch == null) continue;

                    if (branchLookup != null) branchLookup[(routeIndex, cell)] = routes.Count;
                    routes.Add(branch);
                }
            }
        }

        /// <summary>
        ///     With no splitter-made branch, the first fork on the main route still gets one, so a
        ///     splitter placed there before the next rebuild already has a route to send issues down.
        /// </summary>
        private void AddFirstForkBranch(List<Vector2Int> goals)
        {
            if (PathKind != PathKind.Pipe) return;

            var main = _routes[0];
            for (var forkIndex = 0; forkIndex < main.Cells.Count - 1; forkIndex++)
            {
                var branch = FindBranchFrom(_routes, 0, forkIndex, goals, false);
                if (branch == null) continue;

                _branchRoutes[(0, main.Cells[forkIndex])] = _routes.Count;
                _routes.Add(branch);
                return;
            }
        }

        /// <summary>
        ///     Tries each unused exit of one route cell and keeps the first that leads onward. The
        ///     route up to the fork becomes the branch's prefix and is blocked during the search, so
        ///     the branch can't turn back on itself. Where the branch runs into pipe an earlier route
        ///     already uses, it joins that route and carries on as part of it.
        /// </summary>
        private Route FindBranchFrom(List<Route> routes, int parentIndex, int forkIndex,
            List<Vector2Int> goals, bool allowPartial)
        {
            var parent = routes[parentIndex].Cells;
            var fork = parent[forkIndex];
            var parentExit = forkIndex < parent.Count - 1 ? parent[forkIndex + 1] : (Vector2Int?)null;

            // Only an unfinished route can still be growing out of its last cell.
            if (!parentExit.HasValue && !allowPartial) return null;

            var blocked = new HashSet<Vector2Int>();
            for (var i = 0; i <= forkIndex; i++)
                blocked.Add(parent[i]);

            foreach (var direction in Directions)
            {
                var exit = fork + direction;
                if (exit == parentExit || blocked.Contains(exit)) continue;
                if (!pathBuildBoard.IsOccupied(exit, PathKind)) continue;

                var exitOnRoute = IsOnAnyRoute(routes, exit);
                var continuation = exitOnRoute
                    ? new List<Vector2Int> { exit }
                    : BreadthFirstSearch(new[] { exit }, goals, blocked, allowPartial);
                if (continuation == null) continue;

                var branch = new Route { ForkIndex = forkIndex };
                for (var i = 0; i <= forkIndex; i++)
                    branch.Cells.Add(parent[i]);

                if (TryJoinEarlierRoute(routes, branch, continuation)) return branch;

                // Pipe an earlier route flows through that the branch could only enter against that flow.
                if (exitOnRoute) continue;

                branch.Cells.AddRange(continuation);
                branch.Complete = goals.Contains(continuation[^1]);
                return branch;
            }

            return null;
        }

        /// <summary>
        ///     Finds the first cell of <paramref name="continuation" /> that an earlier route passes
        ///     through and, unless following that route on would revisit the branch's own cells, ends
        ///     the branch's own pipe there and continues it as that route.
        /// </summary>
        private static bool TryJoinEarlierRoute(List<Route> routes, Route branch, List<Vector2Int> continuation)
        {
            var visited = new HashSet<Vector2Int>(branch.Cells);
            for (var i = 0; i < continuation.Count; i++)
            {
                var cell = continuation[i];
                foreach (var route in routes)
                {
                    var joinIndex = route.Cells.IndexOf(cell);
                    if (joinIndex < 0 || !CanFollowFrom(route, joinIndex, visited)) continue;

                    for (var c = 0; c < i; c++)
                        branch.Cells.Add(continuation[c]);
                    branch.JoinIndex = branch.Cells.Count;
                    for (var c = joinIndex; c < route.Cells.Count; c++)
                        branch.Cells.Add(route.Cells[c]);
                    branch.Complete = route.Complete;
                    return true;
                }

                visited.Add(cell);
            }

            return false;
        }

        private static bool CanFollowFrom(Route route, int fromIndex, HashSet<Vector2Int> visited)
        {
            for (var i = fromIndex; i < route.Cells.Count; i++)
                if (visited.Contains(route.Cells[i]))
                    return false;
            return true;
        }

        private static bool IsOnAnyRoute(List<Route> routes, Vector2Int cell)
        {
            foreach (var route in routes)
                if (route.Cells.Contains(cell))
                    return true;
            return false;
        }

        // ============================================================
        // ANCHOR RESOLUTION
        // ============================================================

        /// <summary>
        ///     Returns occupied cells that share an edge with the endpoint marker square.
        ///     Endpoint validation is strict: no radius search, no diagonal matching, and no
        ///     nearest occupied fallback.
        /// </summary>
        private List<Vector2Int> GetOccupiedEndpointNeighbors(Transform anchor)
        {
            var candidates = new List<Vector2Int>();
            if (!anchor || !pathBuildBoard) return candidates;

            var anchorCell = pathBuildBoard.ClampToOutsideRing(pathBuildBoard.WorldToCellUnclamped(anchor.position));
            foreach (var direction in Directions)
            {
                var candidate = anchorCell + direction;
                if (!pathBuildBoard.IsCellInBounds(candidate)) continue;
                if (!pathBuildBoard.IsOccupied(candidate, PathKind)) continue;
                candidates.Add(candidate);
            }

            return candidates;
        }

        // ============================================================
        // GIZMO BOOKKEEPING
        // ============================================================

        /// <summary>
        ///     After BFS succeeds, mark every occupied cell NOT on the path as "unused"
        ///     so the gizmo can color them red (visible feedback for the player).
        /// </summary>
        private void RecordUnusedCells(List<Vector2Int> pathCells)
        {
            var onPath = new HashSet<Vector2Int>(pathCells);
            foreach (var piece in pathBuildBoard.PlacedPieces)
                if (piece.pathKind == PathKind)
                    foreach (var cell in piece.cells)
                        if (!onPath.Contains(cell))
                            _unusedCells.Add(cell);
        }

        /// <summary>
        ///     Mark every occupied cell as unused — called when BFS fails entirely
        ///     (no startPoint, no endPoint, or start/goal unreachable from each other).
        /// </summary>
        private void RecordAllOccupiedAsUnused()
        {
            if (!pathBuildBoard) return;
            foreach (var piece in pathBuildBoard.PlacedPieces)
                if (piece.pathKind == PathKind)
                    foreach (var cell in piece.cells)
                        _unusedCells.Add(cell);
        }

        private bool FailRebuild()
        {
            RecordAllOccupiedAsUnused();
            return false;
        }

        /// <summary>One way through the pipe network: the main route, or a branch a splitter feeds.</summary>
        private sealed class Route
        {
            public readonly List<Vector2Int> Cells;
            public readonly List<Vector3> Waypoints;

            // Cell index where this route leaves the route it branched from; -1 for the main route.
            public int ForkIndex = -1;

            // Cell index where this route runs into an earlier route and continues as part of it;
            // -1 when it never does.
            public int JoinIndex = -1;

            public bool Complete;

            public Route() : this(new List<Vector2Int>(), new List<Vector3>())
            {
            }

            public Route(List<Vector2Int> cells, List<Vector3> waypoints)
            {
                Cells = cells;
                Waypoints = waypoints;
            }

            public Vector2Int ForkCell => Cells[ForkIndex];
        }
    }
}
