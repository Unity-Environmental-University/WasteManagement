using System.Collections.Generic;
using UnityEngine;

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
            var total = board ? board.Columns * board.Rows : 0;
            count = Mathf.Clamp(count, 0, total);

            var result = new List<Vector2Int>(count);
            if (count == 0) return result;

            var indices = new List<int>(total);
            for (var i = 0; i < total; i++) indices.Add(i);

            for (var i = 0; i < count; i++)
            {
                var j = Random.Range(i, total);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            for (var i = 0; i < count; i++)
            {
                var index = indices[i];
                result.Add(new Vector2Int(index % board.Columns, index / board.Columns));
            }

            return result;
        }
    }
}