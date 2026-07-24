using System.Collections.Generic;
using _project.Scripts.Core;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     Placement-slot utility that sprinkles lime over the pipeline. The wheel motion and the
    ///     sprinkle blend shape are both driven by the limeSpreader animator controller (Base Layer
    ///     and Sprinkle layer), so nothing here touches the animation. What remains is placement
    ///     wiring plus the stink effect, following the same shape as <see cref="WasteSifter" /> and
    ///     <see cref="TreatmentTank" />.
    /// </summary>
    public class LimeSprinkler : MonoBehaviour
    {
        [Header("Stink")]
        [SerializeField] private float limeStinkReduction = 0.5f;

        [Header("Grid")]
        [Tooltip("Board used to resolve nearby cells. Falls back to GameMaster.Instance.pathBuildBoard.")]
        [SerializeField] private PathBuildBoard board;

        private SpecialInteractController _slot;
        private int _infraValue;

        // Reused between calls so per-sprinkle lookups don't allocate.
        private readonly List<Vector2Int> _surroundingCells = new(9);

        #region Placement

        /// <summary>Called by <see cref="SpecialInteractController" /> when this utility is placed.</summary>
        public void SetSlot(SpecialInteractController slot, int infraValue = 0)
        {
            _slot = slot;
            _infraValue = infraValue;
            ApplyToNearbyCesspits();
        }

        #endregion

        #region Grid

        /// <summary>
        ///     The 3x3 block of board cells centred on this sprinkler's own cell, in column-major
        ///     order. Cells that fall off the board edge are skipped, so the result holds 9 entries
        ///     mid-board and fewer along an edge or corner; it is empty when the sprinkler itself
        ///     sits off-board or no <see cref="PathBuildBoard" /> can be resolved.
        ///     The returned list is reused between calls — copy it if you need to hold onto it.
        /// </summary>
        /// <param name="includeCenter">False to skip the sprinkler's own cell and return only the 8 neighbours.</param>
        private List<Vector2Int> GetSurroundingCells(bool includeCenter = true)
        {
            _surroundingCells.Clear();

            if (!ResolveBoard() || !board.TryWorldToCell(transform.position, out var center))
                return _surroundingCells;

            for (var columnOffset = -1; columnOffset <= 1; columnOffset++)
            for (var rowOffset = -1; rowOffset <= 1; rowOffset++)
            {
                if (!includeCenter && columnOffset == 0 && rowOffset == 0) continue;

                var cell = new Vector2Int(center.x + columnOffset, center.y + rowOffset);
                if (board.IsCellInBounds(cell)) _surroundingCells.Add(cell);
            }

            return _surroundingCells;
        }

        private bool ResolveBoard()
        {
            if (!board) board = GameMaster.Instance ? GameMaster.Instance.pathBuildBoard : null;
            return board;
        }

        #endregion

        #region Effect

        /// <summary>
        ///     Reduces the stink of every cesspit already standing in this sprinkler's 3x3 block.
        ///     Runs once, on placement — cesspits built later pick the reduction up themselves,
        ///     via <see cref="TryApplyTo" /> from <see cref="Cesspit.SetSlot" />.
        /// </summary>
        private void ApplyToNearbyCesspits()
        {
            foreach (var pit in FindObjectsByType<Cesspit>())
                TryApplyTo(pit);
        }

        /// <summary>
        ///     Applies this sprinkler's reduction to <paramref name="pit" />, if the pit stands in
        ///     the 3x3 block. Returns false — changing nothing — when it doesn't, when the pit is
        ///     gone, or when no board can be resolved.
        /// </summary>
        public bool TryApplyTo(Cesspit pit)
        {
            if (!pit) return false;

            // Empty when the board is unresolved or this sprinkler sits off-board; returning here
            // also keeps the board dereference below safe.
            var cells = GetSurroundingCells();
            if (cells.Count == 0) return false;

            if (!board.TryWorldToCell(pit.transform.position, out var pitCell) ||
                !cells.Contains(pitCell)) return false;

            pit.ApplyStinkReduction(limeStinkReduction);
            return true;
        }

        #endregion
    }
}
