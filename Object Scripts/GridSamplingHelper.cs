using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     Shared helpers for sampling cells on a <see cref="PathBuildBoard" /> grid. Keeps the
    ///     random-placement logic in one place so spawners (special-interact slots, buff/debuff
    ///     tiles, ...) don't each reimplement it.
    /// </summary>
    public static class GridSamplingHelper
    {
        /// <summary>
        ///     Returns up to <paramref name="count" /> distinct random grid cells by shuffling the
        ///     full set of cell indices and taking the first few (clamped to the cell count).
        ///     Uses a partial Fisher-Yates: only the first <paramref name="count" /> cells are settled.
        /// </summary>
        public static List<Vector2Int> PickUniqueRandomCells(PathBuildBoard board, int count)
        {
            return PickUniqueRandomCells(board, count, null);
        }

        /// <summary>
        ///     Same as <see cref="PickUniqueRandomCells(PathBuildBoard,int)" />, but skips any cell for
        ///     which <paramref name="isBlocked" /> returns true. Fewer than <paramref name="count" />
        ///     cells come back when the board doesn't have that many free ones left.
        /// </summary>
        public static List<Vector2Int> PickUniqueRandomCells(PathBuildBoard board, int count,
            Func<Vector2Int, bool> isBlocked)
        {
            var total = board ? board.Columns * board.Rows : 0;

            var candidates = new List<Vector2Int>(total);
            for (var i = 0; i < total; i++)
            {
                var cell = new Vector2Int(i % board.Columns, i / board.Columns);
                if (isBlocked is null || !isBlocked(cell)) candidates.Add(cell);
            }

            count = Mathf.Clamp(count, 0, candidates.Count);

            var result = new List<Vector2Int>(count);
            if (count == 0) return result;

            for (var i = 0; i < count; i++)
            {
                var j = Random.Range(i, candidates.Count);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
                result.Add(candidates[i]);
            }

            return result;
        }
    }
}