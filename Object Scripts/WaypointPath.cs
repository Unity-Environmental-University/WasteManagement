using System.Collections.Generic;
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
    public class WaypointPath : MonoBehaviour
    {
        // The board whose occupied cells form the graph that BFS traverses.
        [Tooltip("Source of placed path pieces. The path is rebuilt from these at wave start.")] [SerializeField]
        private PathBuildBoard pathBuildBoard;

        // Fixed spawn-side anchor. When set, it becomes the FIRST waypoint in the list.
        // It's nearest grid cell is the BFS START node.
        [Tooltip("Optional start point prepended before the first placed piece.")] [SerializeField]
        private Transform startPoint;

        // Fixed goal-side anchor. When set, it becomes the LAST waypoint in the list.
        // It's nearest grid cell is the BFS GOAL node.
        [Tooltip("Optional end point appended after the last placed piece.")] [SerializeField]
        private Transform endPoint;

        [Header("Live Build Preview")]
        [Tooltip("Draw the route the pathfinder can currently follow while the player builds.")]
        [SerializeField] private bool showLivePreview = true;

        [SerializeField] private Color completePreviewColor = new(0.35f, 0.9f, 1f, 0.9f);
        [SerializeField] private Color incompletePreviewColor = new(1f, 0.7f, 0.2f, 0.9f);
        [SerializeField, Min(0.01f)] private float previewWidth = 0.12f;
        [SerializeField, Min(0f)] private float previewHeightOffset = 0.7f;

        private LineRenderer _livePreview;
        private PathBuildBoard _subscribedBoard;

        // Cells that ARE part of the final path. Cached for gizmo color-coding.
        private readonly List<Vector2Int> _pathCells = new();

        // Cells visited by BFS but NOT part of the final path. Used only for gizmo
        // visualization so the player can see which placed pieces were ignored.
        private readonly List<Vector2Int> _unusedCells = new();

        // The final ordered list of world-space positions enemies traverse.
        // Built by Rebuild() — do not modify directly.
        private readonly List<Vector3> _waypoints = new();
        private readonly List<Vector3> _alternateWaypoints = new();
        private Vector2Int? _splitCell;

        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.right,
            Vector2Int.left,
            Vector2Int.up,
            Vector2Int.down
        };

        /// <summary>
        ///     The total number of waypoints in the current path. Used by IssueObject
        ///     to detect when it has reached the end of the route.
        /// </summary>
        public int Count => _waypoints.Count;
        public bool HasAlternateRoute => _alternateWaypoints.Count > 0;
        public Transform Destination => endPoint;

        public bool IsValid { get; private set; }
        public string InvalidReason { get; private set; }

        private void OnEnable()
        {
            BindBoardEvents();
            RefreshLivePreview();
        }

        private void Update()
        {
            // Also handles references assigned after this component is enabled.
            BindBoardEvents();
        }

        private void OnDisable()
        {
            if (_subscribedBoard)
                _subscribedBoard.PathLayoutChanged -= RefreshLivePreview;
            if (pathBuildBoard)
                pathBuildBoard.ClearPriorityVisualPath();
            _splitCell = null;
            _subscribedBoard = null;
        }

        private void BindBoardEvents()
        {
            if (_subscribedBoard == pathBuildBoard) return;

            if (_subscribedBoard)
                _subscribedBoard.PathLayoutChanged -= RefreshLivePreview;

            _subscribedBoard = pathBuildBoard;
            if (_subscribedBoard)
                _subscribedBoard.PathLayoutChanged += RefreshLivePreview;

            RefreshLivePreview();
        }

        /// <summary>
        ///     Draws the route as gizmos in the Scene view:
        ///     - YELLOW lines between consecutive waypoints (the route)
        ///     - GREEN cubes for cells ON the path
        ///     - RED cubes for placed cells that are NOT reachable / not on the shortest path
        /// </summary>
        private void OnDrawGizmos()
        {
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
                Gizmos.DrawWireCube(pathBuildBoard.GetPathWaypointPosition(cell), Vector3.one * 0.3f);

            // Unused cells — red markers indicate placed pieces that were ignored
            // (either unreachable from the start or off the shortest route)
            Gizmos.color = Color.red;
            foreach (var cell in _unusedCells)
                Gizmos.DrawWireCube(pathBuildBoard.GetPathWaypointPosition(cell), Vector3.one * 0.3f);
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
            return routeIndex == 1 && HasAlternateRoute ? _alternateWaypoints.Count : _waypoints.Count;
        }

        public Vector3 GetPosition(int routeIndex, int waypointIndex)
        {
            return routeIndex == 1 && HasAlternateRoute
                ? _alternateWaypoints[waypointIndex]
                : _waypoints[waypointIndex];
        }

        /// <summary>
        ///     Returns whether two issues may merge at their current route progress. Issues on
        ///     different branches stay isolated until both are targeting the shared route suffix
        ///     where the branches have rejoined.
        /// </summary>
        public bool CanRoutesMergeAtProgress(int firstRouteIndex, int firstWaypointIndex,
            int secondRouteIndex, int secondWaypointIndex)
        {
            if (firstRouteIndex is < 0 or > 1 || secondRouteIndex is < 0 or > 1)
                return false;
            if (firstRouteIndex == secondRouteIndex) return true;
            if (!HasAlternateRoute) return false;

            var defaultWaypointIndex = firstRouteIndex == 0 ? firstWaypointIndex : secondWaypointIndex;
            var alternateWaypointIndex = firstRouteIndex == 1 ? firstWaypointIndex : secondWaypointIndex;
            var sharedWaypointCount = GetSharedSuffixWaypointCount();
            if (sharedWaypointCount == 0) return false;

            return defaultWaypointIndex >= _waypoints.Count - sharedWaypointCount &&
                   alternateWaypointIndex >= _alternateWaypoints.Count - sharedWaypointCount;
        }

        private int GetSharedSuffixWaypointCount()
        {
            var sharedCount = 0;
            var defaultIndex = _waypoints.Count - 1;
            var alternateIndex = _alternateWaypoints.Count - 1;
            while (defaultIndex >= 0 && alternateIndex >= 0 &&
                   Vector3.SqrMagnitude(_waypoints[defaultIndex] - _alternateWaypoints[alternateIndex]) < 0.0001f)
            {
                sharedCount++;
                defaultIndex--;
                alternateIndex--;
            }

            return sharedCount;
        }

        public int FindClosestWaypointIndex(int routeIndex, Vector3 position, int minimumIndex = 0)
        {
            var route = routeIndex == 1 && HasAlternateRoute ? _alternateWaypoints : _waypoints;
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

        public bool IsSplitPoint(Vector3 worldPosition)
        {
            return _splitCell.HasValue && pathBuildBoard &&
                   pathBuildBoard.TryWorldToCell(worldPosition, out var cell) && cell == _splitCell.Value;
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
            _alternateWaypoints.Clear();
            _splitCell = null;
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
                _waypoints.Add(pathBuildBoard.GetPathWaypointPosition(cell));
            }

            // Bucket remaining occupied cells as "unused" for the gizmo
            RecordUnusedCells(cellPath);

            // Bookend with endPoint
            _waypoints.Add(endPoint.position);

            var alternateCellPath = FindAlternateRoute(cellPath, goals, out _splitCell);
            if (alternateCellPath != null)
            {
                _alternateWaypoints.Add(startPoint.position);
                foreach (var cell in alternateCellPath)
                    _alternateWaypoints.Add(pathBuildBoard.GetPathWaypointPosition(cell));
                _alternateWaypoints.Add(endPoint.position);
            }

            IsValid = true;
            InvalidReason = null;
            RefreshLivePreview();
            return true;
        }

        /// <summary>
        ///     Draws the route available right now in the Game view. A complete route uses
        ///     the normal BFS result. An incomplete route uses the same search and ends at
        ///     the reachable cell with the smallest grid distance to the goal.
        /// </summary>
        public void RefreshLivePreview()
        {
            // Never let placement validation retain a fork from a previous board layout.
            _splitCell = null;

            var renderer = GetLivePreviewRenderer();
            if (renderer)
            {
                renderer.enabled = false;
                renderer.positionCount = 0;
            }

            if (!pathBuildBoard || !startPoint || !endPoint)
            {
                if (pathBuildBoard) pathBuildBoard.ClearPriorityVisualPath();
                return;
            }

            var starts = GetOccupiedEndpointNeighbors(startPoint);
            if (starts.Count == 0)
            {
                pathBuildBoard.ClearPriorityVisualPath();
                return;
            }

            var goals = GetOccupiedEndpointNeighbors(endPoint);
            var previewCells = FindPreviewPath(starts, goals, out var complete);
            if (previewCells == null || previewCells.Count == 0)
            {
                pathBuildBoard.ClearPriorityVisualPath();
                return;
            }

            var alternatePreviewCells = complete
                ? FindAlternateRoute(previewCells, goals, out _splitCell)
                : null;
            if (!complete)
                _splitCell = null;

            pathBuildBoard.SetPriorityVisualPath(previewCells, startPoint.position,
                complete ? endPoint.position : null, alternatePreviewCells);

            if (!showLivePreview || !renderer) return;

            var pointCount = previewCells.Count + 1 + (complete ? 1 : 0);
            renderer.positionCount = pointCount;
            renderer.SetPosition(0, GetPreviewPosition(startPoint.position));
            for (var i = 0; i < previewCells.Count; i++)
                renderer.SetPosition(i + 1,
                    GetPreviewPosition(pathBuildBoard.GetPathWaypointPosition(previewCells[i])));
            if (complete)
                renderer.SetPosition(pointCount - 1, GetPreviewPosition(endPoint.position));

            var color = complete ? completePreviewColor : incompletePreviewColor;
            renderer.startColor = color;
            renderer.endColor = color;
            renderer.enabled = true;
        }

        private Vector3 GetPreviewPosition(Vector3 worldPosition)
        {
            var up = pathBuildBoard ? pathBuildBoard.transform.up : Vector3.up;
            return worldPosition + up * previewHeightOffset;
        }

        private LineRenderer GetLivePreviewRenderer()
        {
            if (_livePreview) return _livePreview;
            if (!showLivePreview) return null;

            var previewObject = pathBuildBoard
                ? pathBuildBoard.transform.Find("Live Path Preview")
                : null;
            if (!previewObject && pathBuildBoard)
            {
                var child = new GameObject("Live Path Preview");
                child.transform.SetParent(pathBuildBoard.transform, false);
                previewObject = child.transform;
            }

            if (!previewObject) return null;
            _livePreview = previewObject.GetComponent<LineRenderer>();
            if (!_livePreview) _livePreview = previewObject.gameObject.AddComponent<LineRenderer>();
            _livePreview.useWorldSpace = true;
            _livePreview.loop = false;
            _livePreview.startWidth = previewWidth;
            _livePreview.endWidth = previewWidth;
            _livePreview.numCapVertices = 4;
            _livePreview.numCornerVertices = 4;
            _livePreview.textureMode = LineTextureMode.Stretch;
            if (!_livePreview.sharedMaterial)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader) _livePreview.sharedMaterial = new Material(shader);
            }
            return _livePreview;
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
                    if (cameFrom.ContainsKey(next) || !pathBuildBoard.IsOccupied(next)) continue;
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
        ///     route, or null if every goal is unreachable.
        /// </summary>
        private List<Vector2Int> BreadthFirstSearch(
            IReadOnlyList<Vector2Int> starts,
            IReadOnlyCollection<Vector2Int> goals,
            ISet<Vector2Int> blocked = null
        )
        {
            if (starts == null || starts.Count == 0 || goals == null || goals.Count == 0)
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
                if (!pathBuildBoard.IsOccupied(start) || cameFrom.ContainsKey(start)) continue;
                frontier.Enqueue(start);
                cameFrom[start] = start;
            }

            Vector2Int? foundGoal = null;

            // MAIN BFS LOOP: expand outward layer by layer
            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();

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
                    if (!pathBuildBoard.IsOccupied(next)) continue;

                    cameFrom[next] = current;
                    frontier.Enqueue(next);
                }
            }

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
        ///     Finds one genuine second branch without enumerating every possible route. At the
        ///     first fork on the normal shortest path, try each unused exit and keep the first one
        ///     that can still reach the goal. The shared prefix is retained and the fork cell is
        ///     blocked during the second BFS so the alternate cannot immediately turn around.
        /// </summary>
        private List<Vector2Int> FindAlternateRoute(
            IReadOnlyList<Vector2Int> defaultRoute,
            IReadOnlyCollection<Vector2Int> goals,
            out Vector2Int? splitCell)
        {
            splitCell = null;
            for (var forkIndex = 0; forkIndex < defaultRoute.Count - 1; forkIndex++)
            {
                var fork = defaultRoute[forkIndex];
                var defaultExit = defaultRoute[forkIndex + 1];
                var previous = forkIndex > 0 ? defaultRoute[forkIndex - 1] : (Vector2Int?)null;

                foreach (var direction in Directions)
                {
                    var alternateExit = fork + direction;
                    if (alternateExit == defaultExit || previous.HasValue && alternateExit == previous.Value)
                        continue;
                    if (!pathBuildBoard.IsOccupied(alternateExit)) continue;

                    var blocked = new HashSet<Vector2Int>();
                    for (var i = 0; i <= forkIndex; i++)
                        blocked.Add(defaultRoute[i]);

                    var continuation = BreadthFirstSearch(new[] { alternateExit }, goals, blocked);
                    if (continuation == null) continue;

                    var alternate = new List<Vector2Int>(forkIndex + 1 + continuation.Count);
                    for (var i = 0; i <= forkIndex; i++)
                        alternate.Add(defaultRoute[i]);
                    alternate.AddRange(continuation);
                    splitCell = fork;
                    return alternate;
                }
            }

            return null;
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
                if (!pathBuildBoard.IsOccupied(candidate)) continue;
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
            foreach (var cell in piece.cells)
                _unusedCells.Add(cell);
        }

        private bool FailRebuild()
        {
            RecordAllOccupiedAsUnused();
            return false;
        }
    }
}
