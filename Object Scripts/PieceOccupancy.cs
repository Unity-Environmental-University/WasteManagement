using System.Collections.Generic;
using _project.Scripts.Core;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     Which placed piece occupies each board cell, tracked separately per <see cref="PathKind" />:
    ///     a conveyor belt and a pipe can share a cell. Piece ids start at 1; 0 means empty.
    /// </summary>
    internal sealed class PieceOccupancy
    {
        private readonly int[,] _pipeIds;
        private readonly Dictionary<Vector2Int, int> _beltIds = new();

        public PieceOccupancy(int columns, int rows)
        {
            _pipeIds = new int[columns, rows];
        }

        /// <summary>The id of the piece of <paramref name="kind" /> in the cell, or 0 when empty or off the board.</summary>
        public int Get(Vector2Int cell, PathKind kind)
        {
            if (kind == PathKind.RecyclingBelt)
                return _beltIds.GetValueOrDefault(cell, 0);

            return IsInBounds(cell) ? _pipeIds[cell.x, cell.y] : 0;
        }

        public bool IsOccupied(Vector2Int cell, PathKind kind) => Get(cell, kind) > 0;

        public void Set(Vector2Int cell, PathKind kind, int pieceId)
        {
            if (kind == PathKind.RecyclingBelt) _beltIds[cell] = pieceId;
            else _pipeIds[cell.x, cell.y] = pieceId;
        }

        /// <summary>Empties the cell, but only while it still holds <paramref name="pieceId" />.</summary>
        public void Clear(Vector2Int cell, PathKind kind, int pieceId)
        {
            if (Get(cell, kind) != pieceId) return;

            if (kind == PathKind.RecyclingBelt) _beltIds.Remove(cell);
            else _pipeIds[cell.x, cell.y] = 0;
        }

        private bool IsInBounds(Vector2Int cell) =>
            cell.x >= 0 && cell.x < _pipeIds.GetLength(0) && cell.y >= 0 && cell.y < _pipeIds.GetLength(1);
    }
}
