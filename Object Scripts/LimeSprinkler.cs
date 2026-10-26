using System.Collections.Generic;
using _project.Scripts.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     Placement-slot utility that sprinkles lime over the pipeline. The wheel motion and the
    ///     sprinkle blend shape are both driven by the limeSpreader animator controller (Base Layer
    ///     and Sprinkle layer), so nothing here touches the animation. What remains is placement
    ///     wiring plus the lime's effect on every cesspit in its square: less stink, and the pit's
    ///     contents slowly broken down during waves. The square grows with the town's level.
    /// </summary>
    public class LimeSprinkler : MonoBehaviour, IPointerClickHandler, IRemovableUtility
    {
        [Header("Stink")]
        [SerializeField] private float limeStinkReduction = 0.5f;

        [Header("Breakdown")]
        [Tooltip("Fullness removed per second from each covered cesspit while a wave runs.")]
        [SerializeField] [Min(0f)] private float fullnessDrainPerSecond = 0.5f;

        [Header("Reach")]
        [Tooltip("Side of the square of cells covered, by town level: element 0 at level 1, and so on. " +
                 "Levels past the end keep the last size.")]
        [SerializeField] private int[] areaSizeByLevel = { 3, 5, 9 };

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

        // Pits this sprinkler has taken stink off, so removal gives back exactly what it took even
        // after the square has grown.
        private readonly HashSet<Cesspit> _limedPits = new();

        private void OnEnable()
        {
            LiveComponentRegistry.Register(this);
            TurnController.OnLevelChanged += HandleLevelChanged;
        }

        private void OnDisable()
        {
            LiveComponentRegistry.Unregister(this);
            TurnController.OnLevelChanged -= HandleLevelChanged;
        }

        private void Update()
        {
            // Only while waste is flowing, and only once there's a pit in reach.
            if (_limedPits.Count == 0) return;
            var turnController = GameMaster.Instance ? GameMaster.Instance.turnController : null;
            if (!turnController || turnController.currentPhase != GamePhase.Tower) return;

            DrainCoveredCesspits(fullnessDrainPerSecond * Time.deltaTime);
        }

        /// <summary>Breaks down <paramref name="amount" /> of every cesspit standing in this sprinkler's square.</summary>
        public void DrainCoveredCesspits(float amount)
        {
            var cells = GetSurroundingCells();
            foreach (var pit in _limedPits)
                if (pit && Covers(pit, cells))
                    pit.Drain(amount);
        }

        // A bigger town means a bigger square: pick up the pits it now reaches and redraw the overlay.
        private void HandleLevelChanged(int level)
        {
            if (!_area || !_area.gameObject.activeSelf) return;
            ApplyToNearbyCesspits();
            ShowPlacedArea();
        }

        /// <summary>Side of the covered square at the town's current level.</summary>
        public int AreaSize => SizeForLevel(areaSizeByLevel, CurrentLevel);

        private static int CurrentLevel
        {
            get
            {
                var gm = GameMaster.Instance;
                return gm && gm.turnController ? Mathf.Max(1, gm.turnController.currentLevel) : 1;
            }
        }

        private static int SizeForLevel(int[] sizes, int level)
        {
            if (sizes == null || sizes.Length == 0) return 3;
            return Mathf.Max(1, sizes[Mathf.Clamp(level - 1, 0, sizes.Length - 1)]);
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

        public bool CanRemove => true;

        public void Remove()
        {
            // Lift the reduction this sprinkler applied to the cesspits in its square.
            foreach (var pit in _limedPits)
                if (pit)
                    pit.ApplyStinkReduction(-limeStinkReduction);
            _limedPits.Clear();

            if (_slot) _slot.ClearOccupied(_infraValue);
            Destroy(gameObject);
        }

        public void OnPointerClick(PointerEventData eventData) => UtilityRemoval.TryRemove(this);

        #endregion

        #region Grid

        /// <summary>
        ///     The square of board cells centred on this sprinkler's own cell (see <see cref="AreaSize" />),
        ///     in column-major order. Cells that fall off the board edge are skipped, so the result is
        ///     full mid-board and smaller along an edge or corner; it is empty when the sprinkler itself
        ///     sits off-board or no <see cref="PathBuildBoard" /> can be resolved.
        ///     The returned list is reused between calls — copy it if you need to hold onto it.
        /// </summary>
        private List<Vector2Int> GetSurroundingCells()
        {
            _surroundingCells.Clear();

            if (!ResolveBoard() || !board.TryWorldToCell(transform.position, out var center))
                return _surroundingCells;

            LimeSprinklerArea.GetCells(board, center, AreaSize, _surroundingCells);
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
            _area.Show(board, transform.position, AreaSize, placedAreaColor);
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
            _previewArea.Show(targetBoard, worldPosition, SizeForLevel(prefab.areaSizeByLevel, CurrentLevel),
                prefab.previewAreaColor);
        }

        public static void HidePlacementPreview()
        {
            if (_previewArea) _previewArea.Hide();
        }

        #endregion

        #region Effect

        /// <summary>
        ///     Reduces the stink of every cesspit already standing in this sprinkler's square.
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
        ///     its square and hasn't had it already. Returns false — changing nothing — when it doesn't,
        ///     when the pit is gone, or when no board can be resolved.
        /// </summary>
        public bool TryApplyTo(Cesspit pit)
        {
            if (!pit || _limedPits.Contains(pit) || !Covers(pit, GetSurroundingCells())) return false;

            pit.ApplyStinkReduction(limeStinkReduction);
            _limedPits.Add(pit);
            return true;
        }

        // Takes the square from GetSurroundingCells, so a caller checking many pits builds it once.
        private bool Covers(Cesspit pit, List<Vector2Int> cells)
        {
            // Empty when the board is unresolved or this sprinkler sits off-board; checking the
            // count first also keeps the board dereference safe.
            return cells.Count > 0 && board.TryWorldToCell(pit.transform.position, out var pitCell) &&
                   cells.Contains(pitCell);
        }

        #endregion
    }
}
