using System.Collections.Generic;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    /// Defines a single traversal route for <see cref="IssueObject"/> enemies to follow.
    /// The path is dynamically built from pieces placed on a <see cref="PathBuildBoard"/>,
    /// with optional fixed start/end transforms bookending the player-built section.
    /// 
    /// Call <see cref="Rebuild"/> before enemies spawn (e.g., at wave start) to construct
    /// the waypoint list from currently placed pieces.
    /// 
    /// Internally uses BREADTH-FIRST SEARCH through occupied grid cells, treating the
    /// whole placed-piece network as a graph. This means:
    ///   - T-junctions and branches work correctly
    ///   - Corners/turns work as long as pieces are orthogonally adjacent (share an edge)
    ///   - The SHORTEST route from start to end (measured in cell count) is always chosen
    ///   - Disconnected pieces are simply not part of the path
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

        // The final ordered list of world-space positions enemies traverse.
        // Built by Rebuild() — do not modify directly.
        private readonly List<Vector3> _waypoints = new();

        // Cells visited by BFS but NOT part of the final path. Used only for gizmo
        // visualization so the player can see which placed pieces were ignored.
        private readonly List<Vector2Int> _unusedCells = new();

        // Cells that ARE part of the final path. Cached for gizmo color-coding.
        private readonly List<Vector2Int> _pathCells = new();

        /// <summary>
        /// The total number of waypoints in the current path. Used by IssueObject
        /// to detect when it has reached the end of the route.
        /// </summary>
        public int Count => _waypoints.Count;

        /// <summary>
        /// Draws the route as gizmos in the Scene view:
        ///   - YELLOW lines between consecutive waypoints (the route)
        ///   - GREEN cubes for cells ON the path
        ///   - RED cubes for placed cells that are NOT reachable / not on the shortest path
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
        /// Returns the world-space position of the waypoint at the given index.
        /// Called by IssueObject each frame to get its current movement target.
        /// </summary>
        public Vector3 GetPosition(int index)
        {
            return _waypoints[index];
        }

        /// <summary>
        ///     Rebuilds the waypoint list using BFS through occupied grid cells.
        ///
        ///     Algorithm:
        ///       1. Determine START cell (nearest occupied cell to <see cref="startPoint"/>)
        ///          and GOAL cell (nearest occupied cell to <see cref="endPoint"/>).
        ///       2. BFS outward from START through 4-way-adjacent occupied cells,
        ///          recording the parent of each visited cell so we can reconstruct the path.
        ///       3. If GOAL was reached, walk parents back to build the cell sequence.
        ///       4. Convert cells to world positions and bookend with start/end Transforms.
        ///
        ///     Entities therefore follow the SHORTEST chain of orthogonally adjacent occupied
        ///     cells from start to goal. Diagonal adjacency is not allowed — pieces must share
        ///     an edge. T-junctions and branches work naturally because BFS considers every
        ///     occupied cell, not just piece endpoints.
        /// </summary>
        public void Rebuild()
        {
            _waypoints.Clear();
            _pathCells.Clear();
            _unusedCells.Clear();

            // Always emit the startPoint as the first waypoint if assigned
            if (startPoint) _waypoints.Add(startPoint.position);

            if (!pathBuildBoard)
            {
                if (endPoint) _waypoints.Add(endPoint.position);
                return;
            }

            // RESOLVE START AND GOAL CELLS
            // We need concrete grid cells for BFS. If the caller's Transform sits directly
            // on an occupied cell, use that. Otherwise, find the nearest OCCUPIED cell by
            // expanding search outward from the Transform's grid position.
            var startCell = ResolveAnchorCell(startPoint);
            var goalCell = ResolveAnchorCell(endPoint);

            // If we can't resolve both endpoints, there's nothing to pathfind through.
            // Fall back to just the start/end Transforms as a direct line.
            if (!startCell.HasValue || !goalCell.HasValue)
            {
                RecordAllOccupiedAsUnused();
                if (endPoint) _waypoints.Add(endPoint.position);
                return;
            }

            // RUN BFS: returns the ordered list of cells from start → goal, or null if unreachable.
            var cellPath = BreadthFirstSearch(startCell.Value, goalCell.Value);

            if (cellPath == null)
            {
                // No connected route exists. All occupied cells go to the "unused" bucket.
                RecordAllOccupiedAsUnused();
                if (endPoint) _waypoints.Add(endPoint.position);
                return;
            }

            // CONVERT CELL PATH TO WAYPOINTS
            foreach (var cell in cellPath)
            {
                _pathCells.Add(cell);
                _waypoints.Add(pathBuildBoard.GetPathWaypointPosition(cell));
            }

            // Bucket remaining occupied cells as "unused" for the gizmo
            RecordUnusedCells(cellPath);

            // Bookend with endPoint
            if (endPoint) _waypoints.Add(endPoint.position);
        }

        // ============================================================
        // BFS IMPLEMENTATION
        // ============================================================

        /// <summary>
        /// Runs breadth-first search over occupied cells. Returns the cell sequence
        /// from <paramref name="start"/> to <paramref name="goal"/> (inclusive) along
        /// the shortest orthogonally-connected route, or null if goal is unreachable.
        /// </summary>
        private List<Vector2Int> BreadthFirstSearch(Vector2Int start, Vector2Int goal)
        {
            // Guard: start and goal must both be occupied cells to be valid graph nodes
            if (!pathBuildBoard.IsOccupied(start) || !pathBuildBoard.IsOccupied(goal))
                return null;

            // Trivial case: start == goal
            if (start == goal)
                return new List<Vector2Int> { start };

            // FRONTIER: cells to explore next (FIFO queue gives shortest-path guarantee in BFS)
            var frontier = new Queue<Vector2Int>();
            frontier.Enqueue(start);

            // PARENT MAP: for each visited cell, remember which cell we came FROM.
            // This lets us reconstruct the path by walking backward from goal → start.
            var cameFrom = new Dictionary<Vector2Int, Vector2Int> { [start] = start };

            // 4-way neighbor offsets (no diagonals): right, left, up, down
            var directions = new[]
            {
                new Vector2Int(01, 00),
                new Vector2Int(-1, 00),
                new Vector2Int(00, 01),
                new Vector2Int(00, -1)
            };

            var found = false;

            // MAIN BFS LOOP: expand outward layer by layer
            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();

                // Early exit: we reached the goal — no need to explore further
                if (current == goal)
                {
                    found = true;
                    break;
                }

                // Check all 4 neighbors
                foreach (var dir in directions)
                {
                    var next = current + dir;

                    // Skip if: already visited, out of bounds, or not occupied
                    if (cameFrom.ContainsKey(next)) continue;
                    if (!pathBuildBoard.IsOccupied(next)) continue;

                    cameFrom[next] = current;
                    frontier.Enqueue(next);
                }
            }

            if (!found) return null;

            // RECONSTRUCT PATH: walk the parent chain from goal back to start
            var path = new List<Vector2Int>();
            var node = goal;
            while (node != start)
            {
                path.Add(node);
                node = cameFrom[node];
            }
            path.Add(start);

            // We built the path goal → start; reverse to get start → goal
            path.Reverse();
            return path;
        }

        // ============================================================
        // ANCHOR RESOLUTION
        // ============================================================

        /// <summary>
        /// Maps an anchor Transform (startPoint or endPoint) onto a specific occupied grid cell.
        ///   1. Convert the Transform's world position to a grid cell.
        ///   2. If that cell is occupied, use it.
        ///   3. Otherwise, expand outward in rings (1-cell, 2-cell, …) looking for the nearest
        ///      occupied cell. This lets the player place the start/end Transforms just outside
        ///      the path without precisely aligning them.
        ///   4. If no occupied cell is found within the search radius, return null.
        /// </summary>
        private Vector2Int? ResolveAnchorCell(Transform anchor)
        {
            if (!anchor) return null;

            var anchorCell = pathBuildBoard.WorldToCell(anchor.position);

            // Fast path: the anchor sits directly on an occupied cell
            if (pathBuildBoard.IsOccupied(anchorCell))
                return anchorCell;

            // Expand outward in increasing Chebyshev rings (radius 1, 2, 3, …)
            // Cap the radius so a stray Transform doesn't cause us to scan the entire board.
            const int maxSearchRadius = 5;
            for (var radius = 1; radius <= maxSearchRadius; radius++)
            {
                // Walk the perimeter of the square ring at this radius
                for (var dx = -radius; dx <= radius; dx++)
                for (var dy = -radius; dy <= radius; dy++)
                {
                    // Only cells ON the ring's edge (skip the interior we already checked)
                    if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius) continue;

                    var candidate = new Vector2Int(anchorCell.x + dx, anchorCell.y + dy);
                    if (pathBuildBoard.IsOccupied(candidate))
                        return candidate;
                }
            }

            return null;
        }

        // ============================================================
        // GIZMO BOOKKEEPING
        // ============================================================

        /// <summary>
        /// After BFS succeeds, mark every occupied cell NOT on the path as "unused"
        /// so the gizmo can color them red (visible feedback for the player).
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
        /// Mark every occupied cell as unused — called when BFS fails entirely
        /// (no startPoint, no endPoint, or start/goal unreachable from each other).
        /// </summary>
        private void RecordAllOccupiedAsUnused()
        {
            if (!pathBuildBoard) return;
            foreach (var piece in pathBuildBoard.PlacedPieces)
            foreach (var cell in piece.cells)
                _unusedCells.Add(cell);
        }
    }
}
