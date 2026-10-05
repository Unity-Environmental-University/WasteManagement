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

        [Header("Area Overlay")]
        [Tooltip("Transparent material for the tiles marking the affected cells.")]
        [SerializeField] private Material areaMaterial;
        [Tooltip("Tint while choosing where to place a sprinkler.")]
        [SerializeField] private Color previewAreaColor = new(0.85f, 1f, 0.4f, 0.55f);
        [Tooltip("Fainter tint that stays under a placed sprinkler.")]
        [SerializeField] private Color placedAreaColor = new(0.85f, 1f, 0.4f, 0.15f);

        private SpecialInteractController _slot;
        private int _infraValue;
        private LimeSprinklerArea _area;

        // One shared placement preview; only one slot can be hovered at a time.
        private static LimeSprinklerArea _previewArea;

        // Reused between calls so per-sprinkle lookups don't allocate.
        private readonly List<Vector2Int> _surroundingCells = new(9);

        private void OnEnable()
        {
            LiveComponentRegistry.Register(this);
        }

        private void OnDisable()
        {
            LiveComponentRegistry.Unregister(this);
        }

        private void OnDestroy()
        {
            if (_area) Destroy(_area.gameObject);
        }

        #region Placement

        /// <summary>Called by <see cref="SpecialInteractController" /> when this utility is placed.</summary>
        public void SetSlot(SpecialInteractController slot, int infraValue = 0)
        {
            _slot = slot;
            _infraValue = infraValue;
            ApplyToNearbyCesspits();
            ShowPlacedArea();
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
        private List<Vector2Int> GetSurroundingCells()
        {
            _surroundingCells.Clear();

            if (!ResolveBoard() || !board.TryWorldToCell(transform.position, out var center))
                return _surroundingCells;

            LimeSprinklerArea.GetCells(board, center, _surroundingCells);
            return _surroundingCells;
        }

        private bool ResolveBoard()
        {
            if (!board) board = GameMaster.Instance ? GameMaster.Instance.pathBuildBoard : null;
            return board;
        }

        #endregion

        #region Area Overlay

        private void ShowPlacedArea()
        {
            if (!ResolveBoard()) return;

            if (!_area) _area = LimeSprinklerArea.Create($"{name} Area", areaMaterial);
            _area.Show(board, transform.position, placedAreaColor);
        }

        /// <summary>
        ///     Shows the bold placement preview for a sprinkler built from <paramref name="prefab" />
        ///     at <paramref name="worldPosition" />, using that prefab's material and preview tint.
        /// </summary>
        public static void ShowPlacementPreview(LimeSprinkler prefab, Vector3 worldPosition)
        {
            if (!prefab) return;

            var targetBoard = prefab.board ? prefab.board :
                GameMaster.Instance ? GameMaster.Instance.pathBuildBoard : null;
            if (!targetBoard) return;

            if (!_previewArea) _previewArea = LimeSprinklerArea.Create("Lime Sprinkler Preview", prefab.areaMaterial);
            _previewArea.Show(targetBoard, worldPosition, prefab.previewAreaColor);
        }

        public static void HidePlacementPreview()
        {
            if (_previewArea) _previewArea.Hide();
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
