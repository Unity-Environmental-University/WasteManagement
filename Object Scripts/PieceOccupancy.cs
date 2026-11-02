using System.Collections.Generic;
using _project.Scripts.Core;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     Which placed piece occupies each board cell, tracked separately per <see cref="PathKind" />:
    ///     a conveyor belt and a pipe can share a cell. Pipe has two layers, regular and isolated, so
    ///     those can cross in one cell too. Piece ids start at 1; 0 means empty.
    /// </summary>
    internal sealed class PieceOccupancy
    {
        private readonly int[,] _pipeIds;
        private readonly int[,] _isolatedPipeIds;
        private readonly Dictionary<Vector2Int, int> _beltIds = new();

        public PieceOccupancy(int columns, int rows)
        {
            _pipeIds = new int[columns, rows];
            _isolatedPipeIds = new int[columns, rows];
        }

        /// <summary>
        ///     The id of the piece of <paramref name="kind" /> in the cell, or 0 when empty or off the
        ///     board. For pipe that is the piece on the requested layer.
        /// </summary>
        public int Get(Vector2Int cell, PathKind kind, bool isolated = false)
        {
            if (kind == PathKind.RecyclingBelt)
                return _beltIds.GetValueOrDefault(cell, 0);
            if (!IsInBounds(cell)) return 0;

            return isolated ? _isolatedPipeIds[cell.x, cell.y] : _pipeIds[cell.x, cell.y];
        }

        /// <summary>
        ///     The piece drawn on top of the cell: isolated pipe over regular pipe. Belts have only one
        ///     layer.
        /// </summary>
        public int GetTop(Vector2Int cell, PathKind kind)
        {
            var isolated = kind == PathKind.Pipe ? Get(cell, kind, true) : 0;
            return isolated > 0 ? isolated : Get(cell, kind);
        }

        /// <summary>Whether any layer of <paramref name="kind" /> holds a piece in the cell.</summary>
        public bool IsOccupied(Vector2Int cell, PathKind kind) => GetTop(cell, kind) > 0;

        public void Set(Vector2Int cell, PathKind kind, bool isolated, int pieceId)
        {
            if (kind == PathKind.RecyclingBelt) _beltIds[cell] = pieceId;
            else if (isolated) _isolatedPipeIds[cell.x, cell.y] = pieceId;
            else _pipeIds[cell.x, cell.y] = pieceId;
        }

        /// <summary>Empties the cell's layer, but only while it still holds <paramref name="pieceId" />.</summary>
        public void Clear(Vector2Int cell, PathKind kind, bool isolated, int pieceId)
        {
            if (Get(cell, kind, isolated) != pieceId) return;

            if (kind == PathKind.RecyclingBelt) _beltIds.Remove(cell);
            else if (isolated) _isolatedPipeIds[cell.x, cell.y] = 0;
            else _pipeIds[cell.x, cell.y] = 0;
        }

        private bool IsInBounds(Vector2Int cell) =>
            cell.x >= 0 && cell.x < _pipeIds.GetLength(0) && cell.y >= 0 && cell.y < _pipeIds.GetLength(1);
    }
}
