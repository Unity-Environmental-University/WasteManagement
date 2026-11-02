using System.Collections.Generic;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     Pipe-art connections forced by ordered pathfinder routes. Each route cell opens only
    ///     toward its previous/next route cell, plus the optional outside-board start/end anchors,
    ///     so nearby branches can't turn a route tile into the wrong corner or junction model.
    ///     Routes and keys are <see cref="PipeLayer" /> nodes, so pipe crossing in one cell keeps
    ///     separate connections per layer.
    /// </summary>
    internal sealed class PriorityRouteConnections
    {
        private readonly Dictionary<Vector2Int, PipeConnections> _connections = new();

        public bool TryGet(Vector2Int cell, out PipeConnections connections) =>
            _connections.TryGetValue(cell, out connections);

        public bool Contains(Vector2Int cell) => _connections.ContainsKey(cell);

        /// <summary>
        ///     Replaces the forced connections with those of the given routes. Returns the cells whose
        ///     connections changed — only those can change their tile model — or an empty set when
        ///     nothing did.
        /// </summary>
        public HashSet<Vector2Int> Set(IReadOnlyList<Vector2Int> routeCells, Vector2Int? startOutsideCell,
            Vector2Int? endOutsideCell, IEnumerable<IReadOnlyList<Vector2Int>> branchRouteCells,
            IEnumerable<(Vector2Int node, Vector2Int outsideCell)> branchEndOpenings = null)
        {
            var nextConnections = new Dictionary<Vector2Int, PipeConnections>();
            AddRoute(nextConnections, routeCells);
            if (branchRouteCells != null)
                foreach (var branchCells in branchRouteCells)
                    AddRoute(nextConnections, branchCells);

            if (routeCells is { Count: > 0 })
            {
                if (startOutsideCell.HasValue)
                    AddOpening(nextConnections, routeCells[0], startOutsideCell.Value);
                if (endOutsideCell.HasValue)
                    AddOpening(nextConnections, routeCells[^1], endOutsideCell.Value);
            }

            if (branchEndOpenings != null)
                foreach (var (node, outsideCell) in branchEndOpenings)
                    if (nextConnections.ContainsKey(node))
                        AddOpening(nextConnections, node, outsideCell);

            var changedCells = new HashSet<Vector2Int>();
            foreach (var pair in nextConnections)
                if (!_connections.TryGetValue(pair.Key, out var connections) || connections != pair.Value)
                    changedCells.Add(pair.Key);
            foreach (var pair in _connections)
                if (!nextConnections.ContainsKey(pair.Key))
                    changedCells.Add(pair.Key);
            if (changedCells.Count == 0) return changedCells;

            _connections.Clear();
            foreach (var pair in nextConnections)
                _connections.Add(pair.Key, pair.Value);
            return changedCells;
        }

        /// <summary>Removes every forced connection and returns the cells that had one.</summary>
        public List<Vector2Int> Clear()
        {
            var changedCells = new List<Vector2Int>(_connections.Keys);
            _connections.Clear();
            return changedCells;
        }

        private static void AddRoute(IDictionary<Vector2Int, PipeConnections> connections,
            IReadOnlyList<Vector2Int> routeCells)
        {
            if (routeCells == null) return;

            for (var i = 0; i < routeCells.Count; i++)
            {
                connections.TryAdd(routeCells[i], PipeConnections.None);
                if (i > 0)
                    AddEdge(connections, routeCells[i - 1], routeCells[i]);
            }
        }

        private static void AddEdge(IDictionary<Vector2Int, PipeConnections> connections,
            Vector2Int from, Vector2Int to)
        {
            var step = PipeLayer.Cell(to) - PipeLayer.Cell(from);
            var forward = PipeShapes.GetConnectionDirection(step);
            var backward = PipeShapes.GetConnectionDirection(-step);
            if (forward == PipeConnections.None || backward == PipeConnections.None) return;

            connections.TryAdd(from, PipeConnections.None);
            connections.TryAdd(to, PipeConnections.None);
            connections[from] |= forward;
            connections[to] |= backward;
        }

        private static void AddOpening(IDictionary<Vector2Int, PipeConnections> connections,
            Vector2Int cell, Vector2Int outsideCell)
        {
            var direction = PipeShapes.GetConnectionDirection(outsideCell - PipeLayer.Cell(cell));
            if (direction != PipeConnections.None)
                connections[cell] |= direction;
        }
    }
}
