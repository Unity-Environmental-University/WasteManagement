using System;
using System.Collections.Generic;
using _project.Scripts.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     Defines the orientation of path pieces on the grid.
    /// </summary>
    public enum PathPieceOrientation
    {
        /// <summary>Path piece extends along the X axis (columns).</summary>
        Horizontal,

        /// <summary>Path piece extends along the Z axis (rows).</summary>
        Vertical
    }

    public enum PathBuildTool
    {
        None,
        Place,
        Break,
        Pan
    }

    /// <summary>
    ///     Manages a grid of PathBuildCell objects for placing path pieces.
    ///     Handles grid generation, piece placement validation, visual previewing, and R-key rotation.
    ///     Tracks all placed pieces and their occupancy via piece IDs.
    /// </summary>
    public class PathBuildBoard : MonoBehaviour
    {
        internal static readonly Vector2Int[] CellNeighborOffsets =
        {
            Vector2Int.right,
            Vector2Int.left,
            Vector2Int.up,
            Vector2Int.down
        };

        // Board cells under a placed conveyor belt.
        private static readonly Color BeltCellColor = new(0.15f, 0.4f, 0.22f);

        [Header("Grid")] [SerializeField] private int columns = 10;
        [SerializeField] private int rows = 10;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private float cellGap = 0.1f;
        [SerializeField] private float cellHeight = 0.1f;

        [Header("Visuals")] [SerializeField] private Material cellMaterial;
        [SerializeField] private Material pipeMaterial;
        [SerializeField] private Material conveyorMaterial;
        [SerializeField] private PipeVisualLibrary pipeVisualLibrary;
        [SerializeField] private Color emptyColor = new(0.18f, 0.18f, 0.18f, 1f);
        [SerializeField] private Color occupiedColor = new(0.28f, 0.28f, 0.28f, 1f);
        [SerializeField] private Color validPreviewColor = new(0.35f, 0.8f, 1f, 1f);
        [SerializeField] private Color invalidPreviewColor = new(1f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color placedPipeColor = new(0.8f, 0.8f, 0.8f, 1f);
        [SerializeField] private Color breakPreviewColor = new(1f, 0.55f, 0.2f, 1f);

        [Tooltip("Height used only for the untextured fallback box drawn when no pipe model is available.")]
        [SerializeField]
        private float pipeVisualHeight = 0.1f;

        [Tooltip("Extra span added on top of the cell pitch so neighbouring tiles overlap slightly instead of leaving a hairline seam.")]
        [SerializeField, Min(0f)]
        private float pipeSeamOverlap = 0.01f;

        [Tooltip("Vertical squash for pipe tiles. 1 keeps the model's authored proportions; lower flattens the pipe for the top-down camera. Applied equally to every piece, so joins stay aligned.")]
        [SerializeField, Range(0.1f, 1f)]
        private float pipeHeightScale = 0.5f;

        [Tooltip("Where issues ride inside the pipe channel, as a fraction of the pipe's height. 0 is the channel floor, 1 the top of its walls.")]
        [SerializeField, Range(0f, 1f)]
        private float entityRideHeightFraction = 0.45f;

        public float entityOnBoardHeight;

        // Grid and placed pieces
        private PathBuildCell[,] _cells;
        private PieceOccupancy _occupancy; // Null until the grid is built
        private readonly List<PlacedPathPiece> _placedPieces = new();
        private readonly Dictionary<int, GameObject> _placedVisuals = new();
        private int _nextPieceId = 1;

        // Hover, selection and previews
        private PathBuildCell _hoveredCell;
        private IPathPiecePlaceable _lastPreviewedPiece;
        private PlacementInventory _placementInventory;
        private GameObject _previewVisual;
        private ConveyorPreview _conveyorPreview;
        private int _highlightedPieceId; // Placed piece currently tinted for break preview (0 = none)
        private PathSplitter _highlightedSplitter;

        // Pipe visuals
        private Transform _visualRoot;
        private PipeVisualLibrary _runtimePipeVisualLibrary;
        private MaterialPropertyBlock _colorPropertyBlock;
        private readonly PriorityRouteConnections _priorityRoutes = new();

        // Last color actually applied to each visual, so an unchanged preview/placement repaint
        // can skip the GetComponentsInChildren<Renderer> walk entirely.
        private readonly Dictionary<GameObject, Color> _appliedVisualColors = new();

        private PipeTilePool _tilePool;
        private PipeTileShaper _tileShaper;
        private PipeTilePool TilePool => _tilePool ??= new PipeTilePool(this);
        private PipeTileShaper TileShaper => _tileShaper ??= new PipeTileShaper(this, TilePool);

        /// <summary>
        ///     Raised whenever the occupied-cell graph changes. Runtime path previews can
        ///     subscribe to this instead of rebuilding every frame.
        /// </summary>
        public event Action PathLayoutChanged;

        public Material ConveyorMaterial => conveyorMaterial ? conveyorMaterial : pipeMaterial;

        public PathKind ActivePathKind { get; private set; }

        /// <summary>
        ///     Read-only collection of all path pieces that have been successfully placed on the board.
        /// </summary>
        public IReadOnlyList<PlacedPathPiece> PlacedPieces => _placedPieces;

        public PathBuildTool ActiveTool { get; private set; }

        public IPathPiecePlaceable ActivePiece { get; private set; }

        /// <summary>Number of columns (X axis) in the grid.</summary>
        public int Columns => columns;

        /// <summary>Number of rows (Z axis) in the grid.</summary>
        public int Rows => rows;

        /// <summary>
        ///     World-space size of a single cell's visual: footprint on X/Z and height on Y.
        ///     Accounts for the board's own scale, so callers don't hardcode cell dimensions.
        /// </summary>
        public Vector3 CellWorldSize => Vector3.Scale(transform.lossyScale, new Vector3(cellSize, cellHeight, cellSize));

        /// <summary>World-space center-to-center spacing of adjacent cells.</summary>
        public Vector2 CellWorldPitch => new(
            Mathf.Abs(transform.lossyScale.x) * PipeCellPitch,
            Mathf.Abs(transform.lossyScale.z) * PipeCellPitch);

        /// <summary>
        ///     Footprint every pipe tile is scaled to. Matching the cell pitch means adjacent tiles
        ///     share an edge rather than leaving the cell gap open; the seam overlap then pushes the
        ///     open sides just past that edge to hide the join.
        /// </summary>
        internal float PipeCellPitch => cellSize + cellGap;

        internal float CellHeight => cellHeight;

        /// <summary>Height of the untextured box drawn when no pipe model is available.</summary>
        internal float FallbackPipeHeight => pipeVisualHeight;

        internal float PipeHeightScale => pipeHeightScale;

        internal float PipeSeamOverlap => pipeSeamOverlap;

        /// <summary>
        ///     Height of a placed pipe above the cell surface. Derived from the scaled model so
        ///     waypoints keep tracking the channel as the cell size or art changes.
        /// </summary>
        private float PipeSurfaceHeight =>
            _tileShaper is { MeasuredTileHeight: > 0f } ? _tileShaper.MeasuredTileHeight : pipeVisualHeight;

        private static PathKind KindOf(IPathPiecePlaceable piece) =>
            piece is ITypedPathPiece typed ? typed.PathKind : PathKind.Pipe;

        /// <summary>
        ///     Initializes the grid by attempting to bind existing cells or building new ones.
        ///     Refreshes visuals after setup.
        /// </summary>
        private void Awake()
        {
            if (!TryBindExistingCells())
                BuildGridIfNeeded();

            RefreshVisuals();
            PathSplitter.AvailabilityChanged += RefreshWaypointPaths;
            Receptacle.AvailabilityChanged += RefreshWaypointPaths;
            NotifyPathLayoutChanged();
        }

        /// <summary>
        ///     Monitors the active path build piece and refreshes visuals when it changes.
        ///     Handles R-key input to toggle the orientation of the active piece.
        /// </summary>
        private void Update()
        {
            BindInventory();

            var selectedPiece = ActivePiece;
            if (selectedPiece != _lastPreviewedPiece)
            {
                _lastPreviewedPiece = selectedPiece;
                RefreshVisuals();
            }

            if (Keyboard.current != null && Keyboard.current[Key.R].wasPressedThisFrame)
                RotateActivePiece();
        }

        /// <summary>True while a piece is armed for placement, so rotating it has an effect.</summary>
        public bool CanRotateActivePiece => ActivePiece != null && ActiveTool == PathBuildTool.Place;

        /// <summary>
        ///     Flips the armed piece between horizontal and vertical. Shared by the R key and the
        ///     toolbar's ROTATE switch; does nothing unless a piece is armed.
        /// </summary>
        public void RotateActivePiece()
        {
            if (!CanRotateActivePiece) return;

            ActivePiece.ToggleOrientation();
            RefreshVisuals();
        }

        public void SetActivePiece(IPathPiecePlaceable piece)
        {
            if (piece != null && ActivePiece != null && piece.Orientation != ActivePiece.Orientation)
                piece.ToggleOrientation();

            ActivePiece = piece;
            if (piece != null) ActivePathKind = KindOf(piece);
            ActiveTool = piece == null ? PathBuildTool.None : PathBuildTool.Place;
            _lastPreviewedPiece = ActivePiece;
            RefreshVisuals();
        }

        public void ClearActivePiece() => SetActivePiece(null);

        public void SetActiveBreakTool(PathKind pathKind = PathKind.Pipe)
        {
            ActivePathKind = pathKind;
            ActivePiece = null;
            ActiveTool = PathBuildTool.Break;
            _lastPreviewedPiece = null;
            RefreshVisuals();
        }

        /// <summary>
        ///     Arms the view-pan tool. Nothing is placed or broken while it is active; world
        ///     drags are left to the CameraController.
        /// </summary>
        public void SetActivePanTool()
        {
            ActivePiece = null;
            ActiveTool = PathBuildTool.Pan;
            _lastPreviewedPiece = null;
            RefreshVisuals();
        }

        /// <summary>
        ///     True while a non-path item (e.g., a sifter or cesspit) is selected in the placement
        ///     inventory. The active path piece is kept, but its preview and cell placement are
        ///     suppressed until the utility selection is cleared or consumed.
        /// </summary>
        public static bool IsUtilityItemSelected()
        {
            var gm = GameMaster.Instance;
            var pending = gm ? gm.PendingPlacement : null;
            return pending != null && pending.PlaceableType != PlaceableType.Path;
        }

        /// <summary>
        ///     Lazily subscribes to the placement inventory's SelectionChanged event, so the
        ///     preview refreshes when the selection flips between path and non-path items.
        /// </summary>
        private void BindInventory()
        {
            var inventory = GameMaster.Instance ? GameMaster.Instance.placementInventory : null;
            if (_placementInventory == inventory) return;

            if (_placementInventory is not null)
                _placementInventory.SelectionChanged -= HandleSelectionChanged;

            _placementInventory = inventory;

            if (_placementInventory is null) return;
            _placementInventory.SelectionChanged += HandleSelectionChanged;
            HandleSelectionChanged(_placementInventory.SelectedItem);
        }

        private void HandleSelectionChanged(IPlaceable pending)
        {
            if (pending != null && pending.PlaceableType != PlaceableType.Path)
            {
                ActivePiece = null;
                ActiveTool = PathBuildTool.None;
                _lastPreviewedPiece = null;
            }

            RefreshVisuals();
        }

        private void OnDestroy()
        {
            PathSplitter.AvailabilityChanged -= RefreshWaypointPaths;
            Receptacle.AvailabilityChanged -= RefreshWaypointPaths;
            if (_placementInventory is not null)
                _placementInventory.SelectionChanged -= HandleSelectionChanged;
        }

        /// <summary>
        ///     Clears all cells and placed pieces, then rebuilds the grid from scratch.
        ///     Can be invoked from the Unity Editor context menu.
        /// </summary>
        [ContextMenu("Rebuild Grid")]
        public void RebuildGrid()
        {
            ClearGeneratedCells();
            _hoveredCell = null;
            _placedPieces.Clear();
            _placedVisuals.Clear();
            _priorityRoutes.Clear();
            _nextPieceId = 1;
            _highlightedPieceId = 0;
            BuildGridIfNeeded();
            RefreshVisuals();
            NotifyPathLayoutChanged();
        }

        /// <summary>
        ///     Updates all cell colors based on occupancy and displays a preview visual for the active path piece.
        ///     Shows valid (blue) or invalid (red) preview based on placement feasibility.
        /// </summary>
        public void RefreshVisuals()
        {
            if (_cells == null) return;

            RefreshSplitterHighlight();
            var breakPreviewPieceId = GetBreakPreviewPieceId();

            for (var column = 0; column < columns; column++)
            for (var row = 0; row < rows; row++)
            {
                var cell = _cells[column, row];
                if (!cell) continue;

                var coordinate = new Vector2Int(column, row);
                var pieceId = _occupancy.Get(coordinate, PathKind.Pipe);
                var beltId = _occupancy.Get(coordinate, PathKind.RecyclingBelt);
                var cellColor = emptyColor;
                if (beltId > 0)
                    cellColor = beltId == breakPreviewPieceId ? breakPreviewColor : BeltCellColor;
                if (pieceId > 0)
                    cellColor = pieceId == breakPreviewPieceId ? breakPreviewColor : occupiedColor;
                cell.SetColor(cellColor);
            }

            RefreshPlacedVisualColors(breakPreviewPieceId);

            if (ActiveTool == PathBuildTool.Break)
            {
                HidePreviewVisual();
                return;
            }

            if (!_hoveredCell)
            {
                HidePreviewVisual();
                return;
            }

            var selectedPiece = ActivePiece;
            if (selectedPiece == null || IsUtilityItemSelected())
            {
                HidePreviewVisual();
                return;
            }

            var footprint = GetFootprint(new Vector2Int(_hoveredCell.Column, _hoveredCell.Row), selectedPiece.Length,
                selectedPiece.Orientation);
            var kind = KindOf(selectedPiece);
            var previewColor = CanPlaceFootprint(footprint, kind) ? validPreviewColor : invalidPreviewColor;
            if (kind == PathKind.RecyclingBelt)
            {
                if (_previewVisual) _previewVisual.SetActive(false);
                _conveyorPreview ??= new ConveyorPreview(this);
                _conveyorPreview.Show(new Vector2Int(_hoveredCell.Column, _hoveredCell.Row), footprint,
                    selectedPiece.Orientation, previewColor);
            }
            else
            {
                _conveyorPreview?.Hide();
                UpdatePipeVisual(GetPreviewVisual(), footprint, selectedPiece.Orientation, previewColor);
            }
        }

        /// <summary>
        ///     Sets the currently hovered cell and refreshes visuals to show the preview.
        /// </summary>
        /// <param name="cell">The cell the mouse is hovering over.</param>
        public void SetHoveredCell(PathBuildCell cell)
        {
            _hoveredCell = cell;
            RefreshVisuals();
        }

        /// <summary>
        ///     Clears the hovered cell if it matches the provided cell, hiding the preview visual.
        /// </summary>
        /// <param name="cell">The cell that the mouse is exiting.</param>
        public void ClearHoveredCell(PathBuildCell cell)
        {
            if (_hoveredCell != cell) return;

            _hoveredCell = null;
            HidePreviewVisual();
            RefreshVisuals();
        }

        /// <summary>
        ///     Attempts to place a path piece on the board starting at the anchor cell.
        ///     Validates the footprint, assigns a unique piece ID, updates occupancy, and creates the visual.
        /// </summary>
        /// <param name="anchorCell">The cell at which the piece starts (origin cell).</param>
        /// <param name="piece">The path piece to place.</param>
        /// <returns>The anchor cell's GameObject if placement succeeds, otherwise null.</returns>
        public GameObject TryPlace(PathBuildCell anchorCell, IPathPiecePlaceable piece)
        {
            if (anchorCell == null || piece == null)
                return null;

            var footprint = GetFootprint(new Vector2Int(anchorCell.Column, anchorCell.Row), piece.Length,
                piece.Orientation);
            var kind = KindOf(piece);
            if (!CanPlaceFootprint(footprint, kind))
                return null;

            var placedPiece = new PlacedPathPiece
            {
                id = _nextPieceId++,
                pathKind = kind,
                length = piece.Length,
                orientation = piece.Orientation,
                infraValue = piece.InfraValue
            };

            foreach (var cell in footprint)
            {
                _occupancy.Set(cell, kind, placedPiece.id);
                placedPiece.cells.Add(cell);
            }

            _placedPieces.Add(placedPiece);
            var placedVisual = CreatePipeVisual($"Placed {kind} {placedPiece.id}");
            _placedVisuals[placedPiece.id] = placedVisual;
            if (kind == PathKind.RecyclingBelt)
            {
                ConveyorVisual.Build(placedVisual.transform, this, placedPiece.cells, placedPiece.orientation);
                placedVisual.SetActive(true);
            }
            else
            {
                UpdatePipeVisual(placedVisual, placedPiece.cells, placedPiece.orientation, placedPipeColor);
                RefreshNeighborPipeGeometry(placedPiece.cells);
                if (Application.isPlaying)
                    PipePlacementPuff.Play(this, placedPiece.cells, PipeSurfaceHeight);
            }
            HidePreviewVisual();
            RefreshVisuals();
            NotifyPathLayoutChanged();
            return anchorCell.gameObject;
        }

        /// <summary>
        ///     Removes the placed path piece occupying the supplied cell.
        /// </summary>
        /// <param name="cell">Any cell occupied by the piece to remove.</param>
        /// <param name="infraValue">The infrastructure value that should be removed from the turn total.</param>
        /// <returns>True if a placed piece was removed, otherwise false.</returns>
        public bool TryBreak(PathBuildCell cell, out int infraValue) => TryBreak(cell, ActivePathKind, out infraValue);

        public bool TryBreak(PathBuildCell cell, PathKind kind, out int infraValue)
        {
            infraValue = 0;
            if (cell == null || _occupancy == null || !IsInBounds(cell.Column, cell.Row))
                return false;

            var pieceId = _occupancy.Get(new Vector2Int(cell.Column, cell.Row), kind);
            if (pieceId <= 0) return false;

            var pieceIndex = _placedPieces.FindIndex(piece => piece.id == pieceId);
            if (pieceIndex < 0)
                return false;

            var piece = _placedPieces[pieceIndex];
            infraValue = piece.infraValue;

            foreach (var occupied in piece.cells)
                _occupancy.Clear(occupied, kind, piece.id);

            if (_placedVisuals.TryGetValue(piece.id, out var visual) && visual)
            {
                ReleaseVisualTiles(visual);
                DestroyObject(visual);
            }

            _placedVisuals.Remove(piece.id);
            _placedPieces.RemoveAt(pieceIndex);
            RefreshNeighborPipeGeometry(piece.cells);
            RefreshVisuals();
            NotifyPathLayoutChanged();
            return true;
        }

        private void NotifyPathLayoutChanged()
        {
            PathLayoutChanged?.Invoke();
            RefreshWaypointPaths();
        }

        private void RefreshWaypointPaths()
        {
            // Waypoint containers in existing scenes are intentionally inactive. Unity does
            // not invoke OnEnable on those components, but their paths are still used by the
            // spawners. Refresh them explicitly so their board-hosted preview remains live.
            var paths = FindObjectsByType<WaypointPath>(FindObjectsInactive.Include);
            foreach (var path in paths)
                path.RefreshLivePreview();

            foreach (var splitter in PathSplitter.Live)
                if (splitter)
                    splitter.RefreshConnections(this, paths);
        }

        /// <summary>
        ///     Calculates the list of grid cells occupied by a piece of a given length and orientation.
        /// </summary>
        /// <param name="anchor">The starting cell position (column, row).</param>
        /// <param name="length">The number of cells the piece spans.</param>
        /// <param name="orientation">Horizontal (extends along X) or Vertical (extends along Z).</param>
        /// <returns>A list of grid positions occupied by the piece.</returns>
        private static List<Vector2Int> GetFootprint(Vector2Int anchor, int length, PathPieceOrientation orientation)
        {
            var footprint = new List<Vector2Int>(length);

            for (var i = 0; i < length; i++)
                footprint.Add(orientation == PathPieceOrientation.Horizontal
                    ? new Vector2Int(anchor.x + i, anchor.y)
                    : new Vector2Int(anchor.x, anchor.y + i));

            return footprint;
        }

        /// <summary>
        ///     Convenience overload — checks if a cell is occupied using a Vector2Int.
        /// </summary>
        public bool IsOccupied(Vector2Int cell) => IsOccupied(cell, PathKind.Pipe);

        public bool IsOccupied(Vector2Int cell, PathKind kind) =>
            _occupancy != null && _occupancy.IsOccupied(cell, kind);

        /// <summary>
        ///     Returns true if the given cell is within the grid bounds (public accessor
        ///     for the private <see cref="IsInBounds(int,int)" />).
        /// </summary>
        public bool IsCellInBounds(Vector2Int cell)
        {
            return IsInBounds(cell.x, cell.y);
        }

        /// <summary>
        ///     Projects a cell outside the board onto the nearest one-cell perimeter ring.
        ///     For example, rows above the board become row == rows instead of staying farther
        ///     away, while in-bounds axes keep their original coordinate.
        /// </summary>
        public Vector2Int ClampToOutsideRing(Vector2Int cell)
        {
            var column = cell.x;
            var row = cell.y;

            if (column < 0) column = -1;
            else if (column >= columns) column = columns;

            if (row < 0) row = -1;
            else if (row >= rows) row = rows;

            return new Vector2Int(column, row);
        }

        /// <summary>
        ///     Generates a new grid of PathBuildCell GameObjects at runtime.
        ///     Each cell is a cube primitive with a trigger collider and a PathBuildCell component.
        /// </summary>
        private void BuildGridIfNeeded()
        {
            columns = Mathf.Max(1, columns);
            rows = Mathf.Max(1, rows);

            _cells = new PathBuildCell[columns, rows];
            _occupancy = new PieceOccupancy(columns, rows);
            _visualRoot = GetOrCreateVisualRoot();

            for (var column = 0; column < columns; column++)
            for (var row = 0; row < rows; row++)
            {
                var cellObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cellObject.name = $"Path Cell {column},{row}";
                cellObject.layer = gameObject.layer;
                cellObject.transform.SetParent(transform, false);
                cellObject.transform.localPosition = GetLocalPosition(column, row);
                cellObject.transform.localScale = new Vector3(cellSize, cellHeight, cellSize);

                var rend = cellObject.GetComponent<Renderer>();
                if (cellMaterial) rend.sharedMaterial = cellMaterial;

                var collisionComp = cellObject.GetComponent<Collider>();
                if (collisionComp) collisionComp.isTrigger = true;

                var cell = cellObject.AddComponent<PathBuildCell>();
                cell.Initialize(this, column, row, rend);
                _cells[column, row] = cell;
            }
        }

        /// <summary>
        ///     Attempts to bind existing PathBuildCell children instead of generating new ones.
        ///     Useful for preserving manually placed cells in the scene.
        /// </summary>
        /// <returns>True if binding succeeds (correct count and no duplicates), otherwise false.</returns>
        private bool TryBindExistingCells()
        {
            var existingCells = GetComponentsInChildren<PathBuildCell>(true);
            if (existingCells.Length != columns * rows)
                return false;

            _cells = new PathBuildCell[columns, rows];
            _occupancy = new PieceOccupancy(columns, rows);
            _visualRoot = GetOrCreateVisualRoot();

            foreach (var cell in existingCells)
            {
                if (!IsInBounds(cell.Column, cell.Row) || _cells[cell.Column, cell.Row] != null)
                    return false;

                cell.Initialize(this, cell.Column, cell.Row, cell.GetComponent<Renderer>());
                _cells[cell.Column, cell.Row] = cell;
            }

            return true;
        }

        /// <summary>
        ///     Destroys all child GameObjects (cells and visuals) and clears the internal state.
        ///     Uses DestroyImmediate in edit mode and Destroy in play mode.
        /// </summary>
        private void ClearGeneratedCells()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyObject(transform.GetChild(i).gameObject);
            }

            _cells = null;
            _occupancy = null;
            _visualRoot = null;
            _previewVisual = null;
            _conveyorPreview?.Forget();
            _tilePool?.Clear();
            _appliedVisualColors.Clear();
        }

        /// <summary>The "PipeVisuals" child that holds every pipe visual, created on first use.</summary>
        internal Transform VisualRoot
        {
            get
            {
                if (!_visualRoot)
                    _visualRoot = GetOrCreateVisualRoot();
                return _visualRoot;
            }
        }

        /// <summary>Material for the untextured box drawn when no pipe model is available.</summary>
        internal Material FallbackPipeMaterial => pipeMaterial;

        /// <summary>
        ///     Gets or creates the "PipeVisuals" child GameObject that holds all pipe visuals.
        /// </summary>
        /// <returns>The Transform of the visual root.</returns>
        private Transform GetOrCreateVisualRoot()
        {
            var existing = transform.Find("PipeVisuals");
            if (existing) return existing;

            var root = new GameObject("PipeVisuals");
            root.transform.SetParent(transform, false);
            return root.transform;
        }

        /// <summary>
        ///     Lazily creates and returns the preview visual GameObject (only created once).
        /// </summary>
        /// <returns>The preview visual GameObject.</returns>
        private GameObject GetPreviewVisual()
        {
            if (_previewVisual) return _previewVisual;

            _previewVisual = CreatePipeVisual("Pipe Preview");
            return _previewVisual;
        }

        /// <summary>Returns a visual's tiles to the pool, hides it, and destroys it.</summary>
        internal void DestroyVisual(GameObject visual)
        {
            ReleaseVisualTiles(visual);
            visual.SetActive(false);
            DestroyObject(visual);
        }

        /// <summary>Destroys at end of frame in Play mode and immediately in Edit mode.</summary>
        internal static void DestroyObject(UnityEngine.Object target)
        {
            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        private void HidePreviewVisual()
        {
            if (_previewVisual) _previewVisual.SetActive(false);
            _conveyorPreview?.Hide();
        }

        /// <summary>
        ///     Creates an empty visual root. Modular pipe tiles are added beneath this root by
        ///     <see cref="UpdatePipeVisual" />.
        /// </summary>
        /// <param name="visualName">The name of the GameObject.</param>
        /// <returns>The created visual GameObject (initially inactive).</returns>
        internal GameObject CreatePipeVisual(string visualName)
        {
            var visual = new GameObject(visualName)
            {
                name = visualName,
                layer = gameObject.layer
            };
            visual.transform.SetParent(VisualRoot, false);
            visual.SetActive(false);
            return visual;
        }

        /// <summary>
        ///     Rebuilds a pipe visual from one normalized modular model per occupied cell. Models
        ///     are chosen from the cell's live neighbor connections, then rotated and scaled from
        ///     their measured bounds. The horizontal size matches the cell pitch (including the
        ///     gap), which makes consecutive cells meet as a single continuous structure.
        /// </summary>
        /// <param name="visual">The visual GameObject to update.</param>
        /// <param name="footprint">The list of grid cells the piece occupies.</param>
        /// <param name="orientation">The orientation of the piece.</param>
        /// <param name="color">The color to apply to the visual.</param>
        private void UpdatePipeVisual(GameObject visual, List<Vector2Int> footprint, PathPieceOrientation orientation,
            Color color)
        {
            if (!visual)
                return;

            if (footprint == null || footprint.Count == 0)
            {
                ReleaseVisualTiles(visual);
                visual.SetActive(false);
                return;
            }

            visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            visual.transform.localScale = Vector3.one;

            var tileMap = TilePool.GetTileMap(visual);

            var additionalCells = new HashSet<Vector2Int>(footprint);

            // Return tiles whose cells dropped out of the footprint before rebuilding the rest.
            List<Vector2Int> staleCells = null;
            foreach (var pair in tileMap)
                if (!additionalCells.Contains(pair.Key))
                    (staleCells ??= new List<Vector2Int>()).Add(pair.Key);
            if (staleCells != null)
                foreach (var cell in staleCells)
                {
                    TilePool.ReleaseTile(tileMap[cell]);
                    tileMap.Remove(cell);
                }

            var tilesChanged = staleCells != null;
            foreach (var cell in footprint)
            {
                if (!GetCell(cell)) continue;

                var connections = GetVisualConnections(cell, additionalCells, orientation);
                var tile = TilePool.AcquireTile(visual.transform, tileMap, cell);
                tilesChanged |= TileShaper.ConfigureTile(tile, cell, connections, orientation);
            }

            // Repaint only when a tile actually changed or the color did — repainting walks
            // every renderer under the visual.
            if (tilesChanged || !_appliedVisualColors.TryGetValue(visual, out var appliedColor) ||
                appliedColor != color)
                SetVisualColor(visual, color);
            visual.SetActive(true);
        }

        /// <summary>
        ///     Rebuilds only the placed pieces that can visually react to the supplied cells
        ///     changing: pieces occupying those cells plus pieces in edge-adjacent cells (their
        ///     endpoint connections may have opened or closed). Distant pieces are untouched.
        /// </summary>
        private void RefreshNeighborPipeGeometry(IEnumerable<Vector2Int> cells)
        {
            var affectedIds = new HashSet<int>();
            foreach (var cell in cells)
            {
                var pieceId = _occupancy.Get(cell, PathKind.Pipe);
                if (pieceId > 0) affectedIds.Add(pieceId);

                foreach (var offset in CellNeighborOffsets)
                {
                    var neighborId = _occupancy.Get(cell + offset, PathKind.Pipe);
                    if (neighborId > 0) affectedIds.Add(neighborId);
                }
            }

            foreach (var pieceId in affectedIds)
            {
                var pieceIndex = _placedPieces.FindIndex(p => p.id == pieceId);
                if (pieceIndex < 0) continue;

                var piece = _placedPieces[pieceIndex];
                if (_placedVisuals.TryGetValue(pieceId, out var visual) && visual)
                    UpdatePipeVisual(visual, piece.cells, piece.orientation, placedPipeColor);
            }
        }

        /// <summary>Returns a visual's tiles to the pool. Call before destroying the visual.</summary>
        private void ReleaseVisualTiles(GameObject visual)
        {
            _appliedVisualColors.Remove(visual);
            TilePool.ReleaseVisual(visual);
        }

        /// <summary>The inspector-assigned pipe library, or the default one loaded on first use.</summary>
        internal PipeVisualLibrary PipeLibrary
        {
            get
            {
                if (pipeVisualLibrary) return pipeVisualLibrary;
                if (!_runtimePipeVisualLibrary)
                    _runtimePipeVisualLibrary = PipeVisualLibrary.Load();
                return _runtimePipeVisualLibrary;
            }
        }

        private PipeConnections GetVisualConnections(Vector2Int cell, HashSet<Vector2Int> additionalCells,
            PathPieceOrientation orientation)
        {
            // The ordered route produced by WaypointPath is authoritative. Using its exact
            // previous/next edges prevents nearby branches from changing a route tile into the
            // wrong corner or junction model.
            if (_priorityRoutes.TryGet(cell, out var priorityConnections))
                return priorityConnections;

            var connections = PipeConnections.None;
            if (HasVisualConnection(cell, cell + Vector2Int.right, additionalCells, orientation))
                connections |= PipeConnections.East;
            if (HasVisualConnection(cell, cell + Vector2Int.up, additionalCells, orientation))
                connections |= PipeConnections.North;
            if (HasVisualConnection(cell, cell + Vector2Int.left, additionalCells, orientation))
                connections |= PipeConnections.West;
            if (HasVisualConnection(cell, cell + Vector2Int.down, additionalCells, orientation))
                connections |= PipeConnections.South;
            return connections;
        }

        private bool HasVisualConnection(Vector2Int cell, Vector2Int neighbor,
            HashSet<Vector2Int> currentCells, PathPieceOrientation currentOrientation)
        {
            if (!IsInBounds(neighbor.x, neighbor.y)) return false;
            if (_priorityRoutes.Contains(neighbor)) return false;

            // Cells belonging to the same placed/previewed segment always connect. Different
            // pieces connect only at compatible endpoints; mere side adjacency is not a junction.
            if (currentCells != null && currentCells.Contains(neighbor)) return true;
            if (!TryGetPlacedPieceAt(neighbor, out var neighborPiece)) return false;

            if (!IsEndpoint(cell, currentCells, currentOrientation) ||
                !IsEndpoint(neighbor, neighborPiece.cells, neighborPiece.orientation))
                return false;

            if (currentOrientation != neighborPiece.orientation)
                return true;

            var delta = neighbor - cell;
            return currentOrientation == PathPieceOrientation.Horizontal ? delta.y == 0 : delta.x == 0;
        }

        /// <summary>
        ///     Makes an ordered pathfinder route authoritative for pipe-art connections. Each
        ///     route cell receives openings only toward its previous/next route cell, plus the
        ///     optional outside-board start/end anchors. Off-route visuals continue to use the
        ///     normal best-effort piece connection rules.
        /// </summary>
        public void SetPriorityVisualPath(IReadOnlyList<Vector2Int> routeCells,
            Vector3? startWorldPosition = null, Vector3? endWorldPosition = null,
            IEnumerable<IReadOnlyList<Vector2Int>> branchRouteCells = null,
            IEnumerable<(Vector2Int cell, Vector2Int outsideCell)> branchEndOpenings = null)
        {
            Vector2Int? startOutsideCell = startWorldPosition.HasValue
                ? ClampToOutsideRing(WorldToCellUnclamped(startWorldPosition.Value))
                : null;
            Vector2Int? endOutsideCell = endWorldPosition.HasValue
                ? ClampToOutsideRing(WorldToCellUnclamped(endWorldPosition.Value))
                : null;

            var changedCells = _priorityRoutes.Set(routeCells, startOutsideCell, endOutsideCell, branchRouteCells,
                branchEndOpenings);
            if (changedCells.Count > 0)
                RefreshNeighborPipeGeometry(changedCells);
        }

        public void ClearPriorityVisualPath()
        {
            var changedCells = _priorityRoutes.Clear();
            if (changedCells.Count > 0)
                RefreshNeighborPipeGeometry(changedCells);
        }

        private bool TryGetPlacedPieceAt(Vector2Int cell, out PlacedPathPiece piece)
        {
            piece = null;
            var pieceId = _occupancy.Get(cell, PathKind.Pipe);
            if (pieceId <= 0) return false;

            piece = _placedPieces.Find(candidate => candidate.id == pieceId);
            return piece != null;
        }

        private static bool IsEndpoint(Vector2Int cell, ICollection<Vector2Int> cells,
            PathPieceOrientation orientation)
        {
            if (cells == null || cells.Count == 0) return false;
            var axis = orientation == PathPieceOrientation.Horizontal ? Vector2Int.right : Vector2Int.up;
            return !cells.Contains(cell - axis) || !cells.Contains(cell + axis);
        }

        /// <summary>
        ///     Calculates the local position for a cell at the given column and row.
        ///     Centers the grid around the origin.
        /// </summary>
        /// <param name="column">The column index.</param>
        /// <param name="row">The row index.</param>
        /// <returns>The local position of the cell.</returns>
        internal Vector3 GetLocalPosition(int column, int row)
        {
            var xOffset = (column - (columns - 1) * 0.5f) * PipeCellPitch;
            var zOffset = (row - (rows - 1) * 0.5f) * PipeCellPitch;
            return new Vector3(xOffset, 0f, zOffset);
        }

        /// <summary>
        ///     Checks if all cells in the footprint are valid (in bounds and unoccupied).
        /// </summary>
        /// <param name="footprint">The list of grid cells to check.</param>
        /// <returns>True if all cells are available for placement, otherwise false.</returns>
        private bool CanPlaceFootprint(List<Vector2Int> footprint, PathKind kind = PathKind.Pipe)
        {
            foreach (var cell in footprint)
                if (!IsInBounds(cell.x, cell.y) || IsOccupied(cell, kind))
                    return false;

            return true;
        }

        // A splitter sits on top of the path, so it claims the hover (and the click) over the piece below it.
        private PathSplitter GetBreakPreviewSplitter()
        {
            if (ActiveTool != PathBuildTool.Break || !_hoveredCell ||
                !IsInBounds(_hoveredCell.Column, _hoveredCell.Row))
                return null;

            var splitter = UtilityRemoval.FindSplitterAt(this, new Vector2Int(_hoveredCell.Column, _hoveredCell.Row));
            return splitter && splitter.CanRemove ? splitter : null;
        }

        private void RefreshSplitterHighlight()
        {
            var splitter = GetBreakPreviewSplitter();
            if (splitter == _highlightedSplitter) return;

            if (_highlightedSplitter) _highlightedSplitter.SetHighlight(null);
            _highlightedSplitter = splitter;
            if (splitter) splitter.SetHighlight(breakPreviewColor);
        }

        private int GetBreakPreviewPieceId()
        {
            if (ActiveTool != PathBuildTool.Break || !_hoveredCell || _occupancy == null ||
                !IsInBounds(_hoveredCell.Column, _hoveredCell.Row) || _highlightedSplitter)
                return 0;

            return _occupancy.Get(new Vector2Int(_hoveredCell.Column, _hoveredCell.Row), ActivePathKind);
        }

        private void RefreshPlacedVisualColors(int breakPreviewPieceId)
        {
            if (breakPreviewPieceId == _highlightedPieceId) return;

            var oldPiece = _placedPieces.Find(p => p.id == _highlightedPieceId);
            SetPlacedVisualColor(_highlightedPieceId, oldPiece?.pathKind == PathKind.RecyclingBelt
                ? ConveyorVisual.BeltColor : placedPipeColor);
            SetPlacedVisualColor(breakPreviewPieceId, breakPreviewColor);
            _highlightedPieceId = breakPreviewPieceId;
        }

        private void SetPlacedVisualColor(int pieceId, Color color)
        {
            if (pieceId > 0 && _placedVisuals.TryGetValue(pieceId, out var visual))
                SetVisualColor(visual, color);
        }

        internal void SetVisualColor(GameObject visual, Color color)
        {
            if (!visual) return;

            _appliedVisualColors[visual] = color;
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                RendererColorUtility.SetColor(renderer, color, ref _colorPropertyBlock);
        }

        /// <summary>
        ///     Checks if a column and row are within the grid bounds.
        /// </summary>
        /// <param name="column">The column index.</param>
        /// <param name="row">The row index.</param>
        /// <returns>True if the position is valid, otherwise false.</returns>
        private bool IsInBounds(int column, int row)
        {
            return column >= 0 && column < columns && row >= 0 && row < rows;
        }

        /// <summary>
        ///     Gets the PathBuildCell at the specified grid position.
        /// </summary>
        /// <param name="position">The grid position (column, row).</param>
        /// <returns>The PathBuildCell if in bounds, otherwise null.</returns>
        private PathBuildCell GetCell(Vector2Int position)
        {
            return IsInBounds(position.x, position.y) ? _cells[position.x, position.y] : null;
        }

        /// <summary>
        ///     Returns the world-space position for the given grid cell. Falls back to the computed
        ///     local position (transformed by this board) when the cell GameObject is missing.
        /// </summary>
        private Vector3 GetCellWorldPosition(Vector2Int position)
        {
            var cell = GetCell(position);
            if (cell) return cell.transform.position;
            return transform.TransformPoint(GetLocalPosition(position.x, position.y));
        }

        /// <summary>
        ///     Returns a world-space waypoint position for the given cell — centered on the pipe's
        ///     top surface so entities travel along the pipe rather than inside the grid.
        /// </summary>
        public Vector3 GetPathWaypointPosition(Vector2Int position, PathKind kind = PathKind.Pipe)
        {
            if (kind == PathKind.RecyclingBelt)
                return GetCellTopPosition(position) + transform.up * CellWorldPitch.x * .24f;
            var basePos = GetCellWorldPosition(position);
            // The kit's pipes are open-topped channels, so issues sit part-way up the walls
            // rather than on top of them.
            return new Vector3(basePos.x,
                basePos.y + cellHeight * 0.5f + PipeSurfaceHeight * entityRideHeightFraction, basePos.z);
        }

        /// <summary>
        ///     Returns a world-space position centered on the top surface of the given cell —
        ///     suitable for parenting objects (such as placement slots) that should sit on the board.
        /// </summary>
        public Vector3 GetCellTopPosition(Vector2Int position)
        {
            var basePos = GetCellWorldPosition(position);
            return new Vector3(basePos.x, basePos.y + cellHeight * 0.5f, basePos.z);
        }

        /// <summary>
        ///     Calculates a rotation for utility objects placed directly on a path piece.
        /// </summary>
        public bool TryGetPathFacingRotation(Vector3 worldPosition, out Quaternion rotation)
        {
            rotation = Quaternion.identity;

            if (_occupancy == null || _placedPieces.Count == 0)
                return false;

            if (!TryWorldToCell(worldPosition, out var cell))
                return false;

            var pieceId = _occupancy.Get(cell, PathKind.Pipe);
            if (pieceId <= 0)
                return false;

            var piece = _placedPieces.Find(p => p.id == pieceId);
            if (piece == null)
                return false;

            var direction = piece.orientation == PathPieceOrientation.Horizontal
                ? transform.TransformDirection(Vector3.right)
                : transform.TransformDirection(Vector3.forward);

            // Face downstream when a live route runs through this cell, so direction-dependent
            // art (e.g. the sifter's comminutor on its exit side) matches the water's travel.
            foreach (var path in FindObjectsByType<WaypointPath>(FindObjectsInactive.Include))
                if (path && path.UsesBoard(this) && path.TryGetFlowSign(worldPosition, direction, out var sign))
                {
                    direction *= sign;
                    break;
                }

            rotation = Quaternion.LookRotation(direction, Vector3.up);
            return true;
        }

        /// <summary>
        ///     Converts a world-space position into the nearest grid cell coordinate.
        ///     Used to map fixed anchor Transforms (e.g., WaypointPath.startPoint/endPoint)
        ///     onto the grid so we can test piece adjacency against them.
        ///     Returns the clamped cell index even if the point is outside the grid bounds.
        /// </summary>
        public Vector2Int WorldToCell(Vector3 worldPosition)
        {
            var cell = WorldToCellUnclamped(worldPosition);

            // Clamp so out-of-bounds anchors snap to the nearest-edge cell
            var column = Mathf.Clamp(cell.x, 0, columns - 1);
            var row = Mathf.Clamp(cell.y, 0, rows - 1);

            return new Vector2Int(column, row);
        }

        /// <summary>
        ///     Converts a world-space position into the nearest grid cell coordinate,
        ///     returning false if the position falls outside the board bounds.
        /// </summary>
        public bool TryWorldToCell(Vector3 worldPosition, out Vector2Int cell)
        {
            cell = WorldToCellUnclamped(worldPosition);
            return IsInBounds(cell.x, cell.y);
        }

        /// <summary>
        ///     Converts a world-space position into the nearest grid cell coordinate without
        ///     clamping it to the board bounds. Endpoint markers just outside the playable grid
        ///     can therefore map to cells like row -1 or rows.
        /// </summary>
        public Vector2Int WorldToCellUnclamped(Vector3 worldPosition)
        {
            // Transform world position into this board's local space
            var local = transform.InverseTransformPoint(worldPosition);

            // Reverse the GetLocalPosition math to recover column/row indices
            // (GetLocalPosition centers the grid, so we add the half-extent back)
            var column = Mathf.RoundToInt(local.x / PipeCellPitch + (columns - 1) * 0.5f);
            var row = Mathf.RoundToInt(local.z / PipeCellPitch + (rows - 1) * 0.5f);

            return new Vector2Int(column, row);
        }

        /// <summary>
        ///     Returns true if two grid cells are 4-way neighbors (share an edge) OR identical.
        ///     Diagonals are NOT considered adjacent — path pieces must link orthogonally.
        ///     Identical cells return true, so anchor Transforms placed directly on a piece's
        ///     endpoint cell count as "connected" to that piece.
        /// </summary>
        public static bool AreCellsAdjacent(Vector2Int a, Vector2Int b)
        {
            var dx = Mathf.Abs(a.x - b.x);
            var dy = Mathf.Abs(a.y - b.y);
            // Same cell, or exactly one axis, differs by 1 (the other by 0)
            if (dx == 0 && dy == 0) return true;
            return (dx == 1 && dy == 0) || (dx == 0 && dy == 1);
        }

        /// <summary>
        ///     Represents a path piece that has been successfully placed on the board.
        ///     Stores metadata (ID, length, orientation) and the list of cells it occupies.
        /// </summary>
        [Serializable]
        public class PlacedPathPiece
        {
            /// <summary>Unique identifier for this placed piece.</summary>
            public int id;
            public PathKind pathKind;

            /// <summary>The number of cells this piece spans.</summary>
            public int length;

            /// <summary>The orientation of the piece (Horizontal or Vertical).</summary>
            public PathPieceOrientation orientation;

            /// <summary>The infrastructure value contributed by this piece.</summary>
            public int infraValue;

            /// <summary>The grid cells occupied by this piece.</summary>
            public List<Vector2Int> cells = new();
        }
    }
}
