using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     Isolated pipe runs on its own layer above regular pipe, so the two can cross in one cell.
    ///     Route searches and route art tell the layers apart with a node: the plain cell for regular
    ///     pipe, and the cell shifted by <see cref="Offset" /> columns for isolated pipe. Neighboring
    ///     nodes on one layer are still one step apart, so grid arithmetic keeps working on nodes;
    ///     convert back with <see cref="Cell" /> before touching anything physical.
    /// </summary>
    internal static class PipeLayer
    {
        private const int Offset = 1 << 16;

        public static Vector2Int Node(Vector2Int cell, bool isolated) =>
            isolated ? new Vector2Int(cell.x + Offset, cell.y) : cell;

        public static bool IsIsolated(Vector2Int node) => node.x >= Offset / 2;

        public static Vector2Int Cell(Vector2Int node) =>
            IsIsolated(node) ? new Vector2Int(node.x - Offset, node.y) : node;
    }
}
