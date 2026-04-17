using System.Collections.Generic;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    public class WaypointPath : MonoBehaviour
    {
        [Tooltip("Source of placed path pieces. The path is rebuilt from these at wave start.")] [SerializeField]
        private PathBuildBoard pathBuildBoard;

        [Tooltip("Optional start point prepended before the first placed piece.")] [SerializeField]
        private Transform startPoint;

        [Tooltip("Optional end point appended after the last placed piece.")] [SerializeField]
        private Transform endPoint;

        private readonly List<Vector3> _waypoints = new();

        public int Count => _waypoints.Count;

        private void OnDrawGizmos()
        {
            if (_waypoints.Count < 2) return;

            Gizmos.color = Color.yellow;
            for (var i = 0; i < _waypoints.Count - 1; i++)
                Gizmos.DrawLine(_waypoints[i], _waypoints[i + 1]);
        }

        public Vector3 GetPosition(int index)
        {
            return _waypoints[index];
        }

        /// <summary>
        ///     Rebuilds the waypoint list from the placed pieces on <see cref="pathBuildBoard" />.
        ///     Pieces are chained in placement order; each piece's cells are reversed if that keeps
        ///     the chain shorter between pieces. Optional start/end transforms bookend the route.
        /// </summary>
        public void Rebuild()
        {
            _waypoints.Clear();

            if (startPoint) _waypoints.Add(startPoint.position);

            if (pathBuildBoard)
            {
                var remaining = new List<PathBuildBoard.PlacedPathPiece>(pathBuildBoard.PlacedPieces);
                var hasAnchor = _waypoints.Count > 0;
                var current = hasAnchor ? _waypoints[^1] : Vector3.zero;

                while (remaining.Count > 0)
                {
                    var bestIdx = -1;
                    var bestReverse = false;
                    var bestDist = float.PositiveInfinity;

                    for (var i = 0; i < remaining.Count; i++)
                    {
                        var piece = remaining[i];
                        if (piece.cells.Count == 0) continue;

                        var firstPos = pathBuildBoard.GetPathWaypointPosition(piece.cells[0]);
                        var lastPos = pathBuildBoard.GetPathWaypointPosition(piece.cells[^1]);

                        float dFirst, dLast;
                        if (hasAnchor)
                        {
                            dFirst = (current - firstPos).sqrMagnitude;
                            dLast = (current - lastPos).sqrMagnitude;
                        }
                        else
                        {
                            var anchor = endPoint ? endPoint.position : current;
                            dFirst = -(anchor - firstPos).sqrMagnitude;
                            dLast = -(anchor - lastPos).sqrMagnitude;
                        }

                        var distance = Mathf.Min(dFirst, dLast);
                        var reverse = dLast < dFirst;

                        if (!(distance < bestDist)) continue;
                        bestDist = distance;
                        bestIdx = i;
                        bestReverse = reverse;
                    }

                    if (bestIdx < 0) break;
                    var chosen = remaining[bestIdx];
                    remaining.RemoveAt(bestIdx);

                    if (bestReverse)
                        for (var i = chosen.cells.Count - 1; i >= 0; i--)
                            _waypoints.Add(pathBuildBoard.GetPathWaypointPosition(chosen.cells[i]));
                    else
                        foreach (var cell in chosen.cells)
                            _waypoints.Add(pathBuildBoard.GetPathWaypointPosition(cell));

                    current = _waypoints[^1];
                    hasAnchor = true;
                }
            }

            if (endPoint) _waypoints.Add(endPoint.position);
        }
    }
}