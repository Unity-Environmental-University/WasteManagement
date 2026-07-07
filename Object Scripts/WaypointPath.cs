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

        [Tooltip("Maximum complete branch routes cached for splitter utilities.")]
        [SerializeField] private int maxSplitterRoutes = 16;

        // Cells that ARE part of the final path. Cached for gizmo color-coding.
        private readonly List<Vector2Int> _pathCells = new();

        // Cells visited by BFS but NOT part of the final path. Used only for gizmo
        // visualization so the player can see which placed pieces were ignored.
        private readonly List<Vector2Int> _unusedCells = new();

        // The final ordered list of world-space positions enemies traverse.
        // Built by Rebuild() — do not modify directly.
        private readonly List<Vector3> _waypoints = new();
        private readonly List<List<Vector3>> _routes = new();
        private int _nextSplitRouteIndex;

        private static readonly Vector2Int[] Directions =
        {
            new(01, 00),
            new(-1, 00),
            new(00, 01),
            new(00, -1)
        };

        /// <summary>
        ///     The total number of waypoints in the current path. Used by IssueObject
        ///     to detect when it has reached the end of the route.
        /// </summary>
        public int Count => _waypoints.Count;
        public int RouteCount => _routes.Count;
        public Transform Destination => endPoint;

        public bool IsValid { get; private set; }
        public string InvalidReason { get; private set; }

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
            return TryGetRoute(routeIndex, out var route) ? route.Count : Count;
        }

        public Vector3 GetPosition(int routeIndex, int waypointIndex)
        {
            return TryGetRoute(routeIndex, out var route) ? route[waypointIndex] : GetPosition(waypointIndex);
        }

        public int GetNextSplitRouteIndex()
        {
            if (RouteCount <= 1)
                return 0;

            var routeIndex = _nextSplitRouteIndex % RouteCount;
            _nextSplitRouteIndex = (_nextSplitRouteIndex + 1) % RouteCount;
            return routeIndex;
        }

        public int FindClosestWaypointIndex(int routeIndex, Vector3 position, int minimumIndex = 0)
        {
            if (!TryGetRoute(routeIndex, out var route) || route.Count == 0)
                return Mathf.Max(0, minimumIndex);

            var startIndex = Mathf.Clamp(minimumIndex, 0, route.Count - 1);
            var closestIndex = startIndex;
            var closestDistance = float.PositiveInfinity;

            for (var i = startIndex; i < route.Count; i++)
            {
                var distance = Vector3.SqrMagnitude(position - route[i]);
                if (distance >= closestDistance) continue;

                closestDistance = distance;
                closestIndex = i;
            }

            return closestIndex;
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
            _pathCells.Clear();
            _unusedCells.Clear();
            IsValid = false;
            InvalidReason = null;
            _nextSplitRouteIndex = 0;

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

            var routeCellPaths = FindSplitterRoutes(starts, goals, cellPath);
            var defaultRoute = BuildWorldRoute(cellPath);
            _routes.Add(defaultRoute);

            // CONVERT CELL PATH TO WAYPOINTS
            _waypoints.AddRange(defaultRoute);
            foreach (var cell in cellPath)
                _pathCells.Add(cell);

            for (var i = 0; i < routeCellPaths.Count; i++)
            {
                var routeCells = routeCellPaths[i];
                if (HasSameCells(routeCells, cellPath)) continue;
                _routes.Add(BuildWorldRoute(routeCells));
            }

            // Bucket remaining occupied cells as "unused" for the gizmo
            RecordUnusedCells(cellPath);

            IsValid = true;
            InvalidReason = null;
            return true;
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
            IReadOnlyCollection<Vector2Int> goals
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

        private List<List<Vector2Int>> FindSplitterRoutes(
            IReadOnlyList<Vector2Int> starts,
            IReadOnlyCollection<Vector2Int> goals,
            List<Vector2Int> defaultRoute)
        {
            var routes = new List<List<Vector2Int>>();
            var goalSet = goals as HashSet<Vector2Int> ?? new HashSet<Vector2Int>(goals);
            var routeLimit = Mathf.Max(1, maxSplitterRoutes);
            var visited = new HashSet<Vector2Int>();
            var currentRoute = new List<Vector2Int>();

            foreach (var start in starts)
            {
                if (routes.Count >= routeLimit) break;
                if (!pathBuildBoard.IsOccupied(start)) continue;

                visited.Clear();
                currentRoute.Clear();
                SearchRoutes(start);
            }

            routes.Sort((a, b) => a.Count.CompareTo(b.Count));
            if (!routes.Exists(route => HasSameCells(route, defaultRoute)))
                routes.Insert(0, defaultRoute);

            return routes;

            void SearchRoutes(Vector2Int current)
            {
                if (routes.Count >= routeLimit) return;

                visited.Add(current);
                currentRoute.Add(current);

                if (goalSet.Contains(current))
                {
                    AddDistinctRoute(routes, currentRoute, routeLimit);
                }
                else
                {
                    foreach (var direction in Directions)
                    {
                        var next = current + direction;
                        if (visited.Contains(next)) continue;
                        if (!pathBuildBoard.IsOccupied(next)) continue;

                        SearchRoutes(next);
                    }
                }

                currentRoute.RemoveAt(currentRoute.Count - 1);
                visited.Remove(current);
            }
        }

        private List<Vector3> BuildWorldRoute(List<Vector2Int> cellRoute)
        {
            var route = new List<Vector3>(cellRoute.Count + 2) { startPoint.position };

            foreach (var cell in cellRoute)
                route.Add(pathBuildBoard.GetPathWaypointPosition(cell));

            route.Add(endPoint.position);
            return route;
        }

        private bool TryGetRoute(int routeIndex, out List<Vector3> route)
        {
            if (routeIndex >= 0 && routeIndex < _routes.Count)
            {
                route = _routes[routeIndex];
                return true;
            }

            route = null;
            return false;
        }

        private static void AddDistinctRoute(List<List<Vector2Int>> routes, List<Vector2Int> route, int routeLimit)
        {
            if (routes.Count >= routeLimit) return;
            if (routes.Exists(existing => HasSameCells(existing, route))) return;

            routes.Add(new List<Vector2Int>(route));
        }

        private static bool HasSameCells(IReadOnlyList<Vector2Int> a, IReadOnlyList<Vector2Int> b)
        {
            if (a == null || b == null || a.Count != b.Count) return false;

            for (var i = 0; i < a.Count; i++)
                if (a[i] != b[i])
                    return false;

            return true;
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
            var directions = new[]
            {
                new Vector2Int(01, 00),
                new Vector2Int(-1, 00),
                new Vector2Int(00, 01),
                new Vector2Int(00, -1)
            };

            foreach (var direction in directions)
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
