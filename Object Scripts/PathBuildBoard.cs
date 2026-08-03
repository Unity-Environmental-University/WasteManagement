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
        Break
    }

    /// <summary>
    ///     Manages a grid of PathBuildCell objects for placing path pieces.
    ///     Handles grid generation, piece placement validation, visual previewing, and R-key rotation.
    ///     Tracks all placed pieces and their occupancy via piece IDs.
    /// </summary>
    public class PathBuildBoard : MonoBehaviour
    {
        /// <summary>
        ///     Raised whenever the occupied-cell graph changes. Runtime path previews can
        ///     subscribe to this instead of rebuilding every frame.
        /// </summary>
        public event Action PathLayoutChanged;

        [Header("Grid")] [SerializeField] private int columns = 10;

        [SerializeField] private int rows = 10;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private float cellGap = 0.1f;
        [SerializeField] private float cellHeight = 0.1f;

        [Header("Visuals")] [SerializeField] private Material cellMaterial;
        [SerializeField] private Material pipeMaterial;
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

        [SerializeField] private PipeVisualLibrary pipeVisualLibrary;

        public float entityOnBoardHeight;
        private readonly List<PlacedPathPiece> _placedPieces = new();
        private readonly Dictionary<int, GameObject> _placedVisuals = new();
        private readonly Dictionary<Vector2Int, PipeConnections> _priorityVisualConnections = new();

        private PathBuildCell[,] _cells;
        private PathBuildCell _hoveredCell;
        private IPathPiecePlaceable _lastPreviewedPiece;
        private PlacementInventory _placementInventory;
        private int _nextPieceId = 1;
        private int _highlightedPieceId; // Placed piece currently tinted for break preview (0 = none)
        private int[,] _pieceIds; // Tracks which piece occupies each cell (0 = empty)
        private GameObject _previewVisual;
        private PipeVisualLibrary _runtimePipeVisualLibrary;
        private Transform _visualRoot;
        private MaterialPropertyBlock _colorPropertyBlock;

        // World height of a scaled pipe tile, measured as tiles are built. Zero until the first
        // model-backed tile exists, in which case the fallback box height stands in.
        private float _measuredPipeTileHeight;

        // Footprint of the straight module, measured once and reused for every piece type so all
        // tiles share one scale. Cached against the library it was measured from.
        private float _sharedPipeFootprint;
        private PipeVisualLibrary _footprintSource;

        // The authored pipe FBXs run front-to-back along local Z. Board rotation calculations
        // use zero degrees for an east/west (local X) connection, so compensate at the source.
        private const float PipeSourceAxisCorrection = 90f;

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

        /// <summary>
        ///     Footprint every pipe tile is scaled to. Matching the cell pitch means adjacent tiles
        ///     share an edge rather than leaving the cell gap open; the seam overlap then pushes the
        ///     open sides just past that edge to hide the join.
        /// </summary>
        private float PipeCellPitch => cellSize + cellGap;

        /// <summary>
        ///     Height of a placed pipe above the cell surface. Derived from the scaled model so
        ///     waypoints keep tracking the channel as the cell size or art changes.
        /// </summary>
        public float PipeSurfaceHeight => _measuredPipeTileHeight > 0f ? _measuredPipeTileHeight : pipeVisualHeight;

        /// <summary>
        ///     Initializes the grid by attempting to bind existing cells or building new ones.
        ///     Refreshes visuals after setup.
        /// </summary>
        private void Awake()
        {
            if (!TryBindExistingCells())
                BuildGridIfNeeded();

            RefreshVisuals();
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

            if (Keyboard.current == null || selectedPiece == null || ActiveTool != PathBuildTool.Place) return;

            if (!Keyboard.current[Key.R].wasPressedThisFrame) return;
            selectedPiece.ToggleOrientation();
            RefreshVisuals();
        }

        public void SetActivePiece(IPathPiecePlaceable piece)
        {
            if (piece != null && ActivePiece != null && piece.Orientation != ActivePiece.Orientation)
                piece.ToggleOrientation();

            ActivePiece = piece;
            ActiveTool = piece == null ? PathBuildTool.None : PathBuildTool.Place;
            _lastPreviewedPiece = ActivePiece;
            RefreshVisuals();
        }

        public void ClearActivePiece() => SetActivePiece(null);

        public void SetActiveBreakTool()
        {
            ActivePiece = null;
            ActiveTool = PathBuildTool.Break;
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
            _priorityVisualConnections.Clear();
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

            var breakPreviewPieceId = GetBreakPreviewPieceId();

            for (var column = 0; column < columns; column++)
            for (var row = 0; row < rows; row++)
            {
                var cell = _cells[column, row];
                if (!cell) continue;

                var pieceId = _pieceIds[column, row];
                var cellColor = emptyColor;
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
            var previewColor = CanPlaceFootprint(footprint) ? validPreviewColor : invalidPreviewColor;
            UpdatePipeVisual(GetPreviewVisual(), footprint, selectedPiece.Orientation, previewColor);
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
            if (!CanPlaceFootprint(footprint))
                return null;

            var placedPiece = new PlacedPathPiece
            {
                id = _nextPieceId++,
                length = piece.Length,
                orientation = piece.Orientation,
                infraValue = piece.InfraValue
            };

            foreach (var cell in footprint)
            {
                _pieceIds[cell.x, cell.y] = placedPiece.id;
                placedPiece.cells.Add(cell);
            }

            _placedPieces.Add(placedPiece);
            var placedVisual = CreatePipeVisual($"Placed Pipe {placedPiece.id}");
            _placedVisuals[placedPiece.id] = placedVisual;
            RefreshAllPlacedPipeGeometry();
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
        public bool TryBreak(PathBuildCell cell, out int infraValue)
        {
            infraValue = 0;
            if (cell == null || _pieceIds == null || !IsInBounds(cell.Column, cell.Row))
                return false;

            var pieceId = _pieceIds[cell.Column, cell.Row];
            if (pieceId <= 0) return false;

            var pieceIndex = _placedPieces.FindIndex(piece => piece.id == pieceId);
            if (pieceIndex < 0)
                return false;

            var piece = _placedPieces[pieceIndex];
            infraValue = piece.infraValue;

            foreach (var occupied in piece.cells)
                if (IsInBounds(occupied.x, occupied.y) && _pieceIds[occupied.x, occupied.y] == piece.id)
                    _pieceIds[occupied.x, occupied.y] = 0;

            if (_placedVisuals.TryGetValue(piece.id, out var visual) && visual)
            {
                if (Application.isPlaying)
                    Destroy(visual);
                else
                    DestroyImmediate(visual);
            }

            _placedVisuals.Remove(piece.id);
            _placedPieces.RemoveAt(pieceIndex);
            RefreshAllPlacedPipeGeometry();
            RefreshVisuals();
            NotifyPathLayoutChanged();
            return true;
        }

        private void NotifyPathLayoutChanged()
        {
            PathLayoutChanged?.Invoke();

            // Waypoint containers in existing scenes are intentionally inactive. Unity does
            // not invoke OnEnable on those components, but their paths are still used by the
            // spawners. Refresh them explicitly so their board-hosted preview remains live.
            foreach (var path in FindObjectsByType<WaypointPath>(FindObjectsInactive.Include))
                path.RefreshLivePreview();
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
        ///     Checks if a given grid cell is occupied by any placed piece.
        /// </summary>
        /// <param name="column">The column index.</param>
        /// <param name="row">The row index.</param>
        /// <returns>True if the cell is in bounds and occupied, otherwise false.</returns>
        private bool IsOccupied(int column, int row)
        {
            return _pieceIds != null && IsInBounds(column, row) && _pieceIds[column, row] > 0;
        }

        /// <summary>
        ///     Convenience overload — checks if a cell is occupied using a Vector2Int.
        /// </summary>
        public bool IsOccupied(Vector2Int cell)
        {
            return IsOccupied(cell.x, cell.y);
        }

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
            _pieceIds = new int[columns, rows];
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
            _pieceIds = new int[columns, rows];
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
                var child = transform.GetChild(i).gameObject;

                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }

            _cells = null;
            _pieceIds = null;
            _visualRoot = null;
            _previewVisual = null;
        }

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

        /// <summary>
        ///     Hides the preview visual by deactivating it.
        /// </summary>
        private void HidePreviewVisual()
        {
            if (_previewVisual) _previewVisual.SetActive(false);
        }

        /// <summary>
        ///     Creates an empty visual root. Modular pipe tiles are added beneath this root by
        ///     <see cref="UpdatePipeVisual" />.
        /// </summary>
        /// <param name="visualName">The name of the GameObject.</param>
        /// <returns>The created visual GameObject (initially inactive).</returns>
        private GameObject CreatePipeVisual(string visualName)
        {
            if (!_visualRoot)
                _visualRoot = GetOrCreateVisualRoot();

            var visual = new GameObject(visualName)
            {
                name = visualName,
                layer = gameObject.layer
            };
            visual.transform.SetParent(_visualRoot, false);
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
                visual.SetActive(false);
                return;
            }

            ClearVisualChildren(visual.transform);
            visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            visual.transform.localScale = Vector3.one;

            var additionalCells = new HashSet<Vector2Int>(footprint);
            foreach (var cell in footprint)
            {
                if (!GetCell(cell)) continue;

                var connections = GetVisualConnections(cell, additionalCells, orientation);
                var tile = CreatePipeTile(visual.transform, cell, connections, orientation);
                if (!tile) continue;

                tile.name = $"Pipe Tile {cell.x},{cell.y}";
            }

            SetVisualColor(visual, color);
            visual.SetActive(true);
        }

        private void RefreshAllPlacedPipeGeometry()
        {
            foreach (var piece in _placedPieces)
                if (_placedVisuals.TryGetValue(piece.id, out var visual) && visual)
                    UpdatePipeVisual(visual, piece.cells, piece.orientation, placedPipeColor);
        }

        private GameObject CreatePipeTile(Transform parent, Vector2Int cell, PipeConnections connections,
            PathPieceOrientation fallbackOrientation)
        {
            var library = GetPipeVisualLibrary();
            var prefab = SelectPipePrefab(library, connections);

            // The tile transform carries the board rotation and the one shared footprint scale;
            // the model beneath it carries the per-axis seam adjustment. Splitting the two keeps
            // every tile reporting the same uniform footprint whichever mesh it happens to use.
            var tile = new GameObject("Pipe Tile");
            tile.transform.SetParent(parent, false);
            tile.transform.localPosition = Vector3.zero;
            tile.transform.localRotation = Quaternion.Euler(0f,
                GetPipeRotation(connections, fallbackOrientation) + PipeSourceAxisCorrection, 0f);
            tile.transform.localScale = Vector3.one;

            GameObject model;
            if (prefab)
            {
                model = Instantiate(prefab, tile.transform);
            }
            else
            {
                model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                model.transform.SetParent(tile.transform, false);
                if (model.TryGetComponent<Renderer>(out var renderer) && pipeMaterial)
                    renderer.sharedMaterial = pipeMaterial;
            }

            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            ConfigurePipeTileHierarchy(tile);

            var renderers = tile.GetComponentsInChildren<Renderer>(true);
            if (prefab) ApplyPipeSurfaceMaterial(renderers, library ? library.SurfaceMaterial : null);

            // The straight module is the only mesh that stays closed on its side walls; every
            // junction opens on both axes and so has to reach past the cell edge on both.
            var opensOnBothAxes = !prefab || !library || prefab != library.StraightPipe;
            NormalizePipeTile(tile, model.transform, cell, renderers, prefab, opensOnBothAxes);
            return tile;
        }

        private PipeVisualLibrary GetPipeVisualLibrary()
        {
            if (pipeVisualLibrary) return pipeVisualLibrary;
            if (!_runtimePipeVisualLibrary)
                _runtimePipeVisualLibrary = PipeVisualLibrary.Load();
            return _runtimePipeVisualLibrary;
        }

        private static GameObject SelectPipePrefab(PipeVisualLibrary library, PipeConnections connections)
        {
            if (!library) return null;

            var connectionCount = CountConnections(connections);
            // No dedicated cap is required: the straight model keeps isolated/off-route cells
            // visually tidy and gives one-neighbor route cells an opening toward an endpoint.
            if (connectionCount <= 1) return library.StraightPipe;

            // A three-way cell must use the tee. Substituting the four-way mesh leaves its
            // unused arm poking into a neighbour as a dead-end stub.
            if (connectionCount == 3)
                return library.TJunctionPipe ? library.TJunctionPipe : library.StraightPipe;

            if (connectionCount >= 4)
                return library.JunctionPipe ? library.JunctionPipe : library.StraightPipe;

            return IsStraight(connections)
                ? library.StraightPipe
                : library.CornerPipe ? library.CornerPipe : library.StraightPipe;
        }

        private void NormalizePipeTile(GameObject tile, Transform model, Vector2Int cell,
            IReadOnlyList<Renderer> renderers, GameObject prefab, bool opensOnBothAxes)
        {
            if (!TryCalculateLocalRendererBounds(tile.transform, renderers, out var bounds))
            {
                var fallbackPosition = GetLocalPosition(cell.x, cell.y);
                tile.transform.localPosition =
                    new Vector3(fallbackPosition.x, cellHeight * 0.5f + pipeVisualHeight * 0.5f, fallbackPosition.z);
                tile.transform.localScale = new Vector3(PipeCellPitch, pipeVisualHeight, PipeCellPitch);
                return;
            }

            // Every mesh in the kit is authored as a square single-cell module, so one footprint
            // scale serves all of them, and it is measured once from the straight module rather
            // than per mesh: a corner authored a fraction larger than its neighbours must not
            // arrive at a join a step wider. X and Z stay equal so mortar courses line up on
            // horizontal and vertical runs alike. Height is squashed separately for the top-down
            // read; because the squash is the same for every piece, it costs only brick proportion
            // on the walls, never joint alignment.
            var footprint = GetSharedPipeFootprint(prefab, bounds);
            var scale = footprint > Mathf.Epsilon ? PipeCellPitch / footprint : 1f;
            var verticalScale = scale * pipeHeightScale;
            tile.transform.localScale = new Vector3(scale, verticalScale, scale);

            // The seam overlap is directional. A tile reaches past the cell edge only on the axes
            // it actually opens on, closing those joins, and pulls in by the same amount on the
            // sides it keeps walled — otherwise two parallel runs bleed into each other's cells.
            // The authored modules run front-to-back along local Z, so that is the open axis.
            var openFactor = 1f + pipeSeamOverlap / PipeCellPitch;
            var closedFactor = 1f - pipeSeamOverlap / PipeCellPitch;
            var modelScale = new Vector3(opensOnBothAxes ? openFactor : closedFactor, 1f, openFactor);
            model.localScale = modelScale;

            var totalScale = new Vector3(scale * modelScale.x, verticalScale, scale * modelScale.z);
            var rotatedCenter = tile.transform.localRotation * Vector3.Scale(bounds.center, totalScale);
            var cellPosition = GetLocalPosition(cell.x, cell.y);
            tile.transform.localPosition = new Vector3(
                cellPosition.x - rotatedCenter.x,
                cellHeight * 0.5f - bounds.min.y * verticalScale,
                cellPosition.z - rotatedCenter.z);

            _measuredPipeTileHeight = bounds.size.y * verticalScale;
        }

        /// <summary>
        ///     Footprint used to scale every pipe tile, taken from the straight module so that all
        ///     piece types share one scale. Falls back to the tile's own bounds when no library
        ///     model backs the tile (the untextured box).
        /// </summary>
        private float GetSharedPipeFootprint(GameObject prefab, Bounds tileBounds)
        {
            var tileFootprint = Mathf.Max(tileBounds.size.x, tileBounds.size.z);
            var library = GetPipeVisualLibrary();
            var reference = library ? library.StraightPipe : null;
            if (!prefab || !reference) return tileFootprint;

            if (_footprintSource != library || _sharedPipeFootprint <= Mathf.Epsilon)
            {
                _footprintSource = library;
                _sharedPipeFootprint = MeasurePrefabFootprint(reference);
            }

            return _sharedPipeFootprint > Mathf.Epsilon ? _sharedPipeFootprint : tileFootprint;
        }

        private static float MeasurePrefabFootprint(GameObject prefab)
        {
            var bounds = default(Bounds);
            var hasBounds = false;
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh) continue;
                EncapsulateLocalBounds(prefab.transform, filter.transform, filter.sharedMesh.bounds,
                    ref bounds, ref hasBounds);
            }

            return hasBounds ? Mathf.Max(bounds.size.x, bounds.size.z) : 0f;
        }

        private static bool TryCalculateLocalRendererBounds(Transform root, IReadOnlyList<Renderer> renderers,
            out Bounds bounds)
        {
            bounds = default;
            var hasBounds = false;
            foreach (var renderer in renderers)
                EncapsulateLocalBounds(root, renderer.transform, renderer.localBounds, ref bounds, ref hasBounds);

            return hasBounds;
        }

        /// <summary>
        ///     Expands <paramref name="bounds" /> (expressed in <paramref name="root" />'s local space)
        ///     to cover the eight corners of <paramref name="localBounds" /> taken in
        ///     <paramref name="source" />'s local space.
        /// </summary>
        private static void EncapsulateLocalBounds(Transform root, Transform source, Bounds localBounds,
            ref Bounds bounds, ref bool hasBounds)
        {
            var min = localBounds.min;
            var max = localBounds.max;

            for (var x = 0; x < 2; x++)
            for (var y = 0; y < 2; y++)
            for (var z = 0; z < 2; z++)
            {
                var sourcePoint = new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y,
                    z == 0 ? min.z : max.z);
                var rootPoint = root.InverseTransformPoint(source.TransformPoint(sourcePoint));
                if (!hasBounds)
                {
                    bounds = new Bounds(rootPoint, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(rootPoint);
                }
            }
        }

        private static void ApplyPipeSurfaceMaterial(IReadOnlyList<Renderer> renderers, Material surfaceMaterial)
        {
            if (!surfaceMaterial) return;
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                    materials[i] = surfaceMaterial;
                renderer.sharedMaterials = materials;
            }
        }

        private void ConfigurePipeTileHierarchy(GameObject tile)
        {
            foreach (var child in tile.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = gameObject.layer;

            foreach (var collisionComp in tile.GetComponentsInChildren<Collider>(true))
                collisionComp.enabled = false;
        }

        private static void ClearVisualChildren(Transform visual)
        {
            for (var i = visual.childCount - 1; i >= 0; i--)
            {
                var child = visual.GetChild(i).gameObject;
                // Destroy is deferred in Play Mode. Detach first so hierarchy lookups cannot
                // resolve a stale tile while its replacement is created in the same frame.
                child.transform.SetParent(null, false);
                child.SetActive(false);
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
        }

        private PipeConnections GetVisualConnections(Vector2Int cell, HashSet<Vector2Int> additionalCells,
            PathPieceOrientation orientation)
        {
            // The ordered route produced by WaypointPath is authoritative. Using its exact
            // previous/next edges prevents nearby branches from changing a route tile into the
            // wrong corner or junction model.
            if (_priorityVisualConnections.TryGetValue(cell, out var priorityConnections))
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
            if (_priorityVisualConnections.ContainsKey(neighbor)) return false;

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
            IReadOnlyList<Vector2Int> alternateRouteCells = null)
        {
            var nextConnections = new Dictionary<Vector2Int, PipeConnections>();
            AddPriorityRoute(nextConnections, routeCells);
            AddPriorityRoute(nextConnections, alternateRouteCells);

            if (routeCells is { Count: > 0 })
            {
                if (startWorldPosition.HasValue)
                    AddPriorityOpening(nextConnections, routeCells[0],
                        ClampToOutsideRing(WorldToCellUnclamped(startWorldPosition.Value)));
                if (endWorldPosition.HasValue)
                    AddPriorityOpening(nextConnections, routeCells[^1],
                        ClampToOutsideRing(WorldToCellUnclamped(endWorldPosition.Value)));
            }

            if (HaveSameConnections(_priorityVisualConnections, nextConnections)) return;

            _priorityVisualConnections.Clear();
            foreach (var pair in nextConnections)
                _priorityVisualConnections.Add(pair.Key, pair.Value);
            RefreshAllPlacedPipeGeometry();
        }

        private static void AddPriorityRoute(IDictionary<Vector2Int, PipeConnections> connections,
            IReadOnlyList<Vector2Int> routeCells)
        {
            if (routeCells == null) return;

            for (var i = 0; i < routeCells.Count; i++)
            {
                connections.TryAdd(routeCells[i], PipeConnections.None);
                if (i > 0)
                    AddPriorityEdge(connections, routeCells[i - 1], routeCells[i]);
            }
        }

        public void ClearPriorityVisualPath()
        {
            if (_priorityVisualConnections.Count == 0) return;
            _priorityVisualConnections.Clear();
            RefreshAllPlacedPipeGeometry();
        }

        private static void AddPriorityEdge(IDictionary<Vector2Int, PipeConnections> connections,
            Vector2Int from, Vector2Int to)
        {
            var forward = GetConnectionDirection(to - from);
            var backward = GetConnectionDirection(from - to);
            if (forward == PipeConnections.None || backward == PipeConnections.None) return;

            connections.TryAdd(from, PipeConnections.None);
            connections.TryAdd(to, PipeConnections.None);
            connections[from] |= forward;
            connections[to] |= backward;
        }

        private static void AddPriorityOpening(IDictionary<Vector2Int, PipeConnections> connections,
            Vector2Int cell, Vector2Int outsideCell)
        {
            var direction = GetConnectionDirection(outsideCell - cell);
            if (direction != PipeConnections.None)
                connections[cell] |= direction;
        }

        private static PipeConnections GetConnectionDirection(Vector2Int delta)
        {
            if (delta == Vector2Int.right) return PipeConnections.East;
            if (delta == Vector2Int.up) return PipeConnections.North;
            if (delta == Vector2Int.left) return PipeConnections.West;
            if (delta == Vector2Int.down) return PipeConnections.South;
            return PipeConnections.None;
        }

        private static bool HaveSameConnections(IReadOnlyDictionary<Vector2Int, PipeConnections> left,
            IReadOnlyDictionary<Vector2Int, PipeConnections> right)
        {
            if (left.Count != right.Count) return false;
            foreach (var pair in left)
                if (!right.TryGetValue(pair.Key, out var connections) || connections != pair.Value)
                    return false;
            return true;
        }

        private bool TryGetPlacedPieceAt(Vector2Int cell, out PlacedPathPiece piece)
        {
            piece = null;
            var pieceId = _pieceIds[cell.x, cell.y];
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

        private static int CountConnections(PipeConnections connections)
        {
            var count = 0;
            if ((connections & PipeConnections.East) != 0) count++;
            if ((connections & PipeConnections.North) != 0) count++;
            if ((connections & PipeConnections.West) != 0) count++;
            if ((connections & PipeConnections.South) != 0) count++;
            return count;
        }

        private static bool IsStraight(PipeConnections connections)
        {
            return connections == (PipeConnections.East | PipeConnections.West) ||
                   connections == (PipeConnections.North | PipeConnections.South);
        }

        private static float GetPipeRotation(PipeConnections connections,
            PathPieceOrientation fallbackOrientation)
        {
            var count = CountConnections(connections);
            if (count == 0)
                return fallbackOrientation == PathPieceOrientation.Horizontal ? 0f : 90f;

            if (count == 1)
            {
                if ((connections & PipeConnections.East) != 0) return 0f;
                if ((connections & PipeConnections.South) != 0) return 90f;
                if ((connections & PipeConnections.West) != 0) return 180f;
                return 270f;
            }

            if (count == 2)
            {
                if (IsStraight(connections))
                    return (connections & PipeConnections.East) != 0 ? 0f : 90f;

                // brick_cornerPipe is authored opening north+west; after the source-axis
                // correction it opens north+east at zero, so each quadrant steps by 90.
                if (connections == (PipeConnections.North | PipeConnections.East)) return 0f;
                if (connections == (PipeConnections.East | PipeConnections.South)) return 90f;
                if (connections == (PipeConnections.South | PipeConnections.West)) return 180f;
                return 270f;
            }

            if (count == 3)
            {
                // brick_TPipe is authored opening north+south+west, so after the correction its
                // closed side faces south at zero. Rotate by whichever side has no connection.
                if ((connections & PipeConnections.South) == 0) return 0f;
                if ((connections & PipeConnections.West) == 0) return 90f;
                if ((connections & PipeConnections.North) == 0) return 180f;
                return 270f;
            }

            // The four-way mesh is rotationally symmetric.
            return 0f;
        }

        /// <summary>
        ///     Calculates the local position for a cell at the given column and row.
        ///     Centers the grid around the origin.
        /// </summary>
        /// <param name="column">The column index.</param>
        /// <param name="row">The row index.</param>
        /// <returns>The local position of the cell.</returns>
        private Vector3 GetLocalPosition(int column, int row)
        {
            var step = cellSize + cellGap;
            var xOffset = (column - (columns - 1) * 0.5f) * step;
            var zOffset = (row - (rows - 1) * 0.5f) * step;
            return new Vector3(xOffset, 0f, zOffset);
        }

        /// <summary>
        ///     Checks if all cells in the footprint are valid (in bounds and unoccupied).
        /// </summary>
        /// <param name="footprint">The list of grid cells to check.</param>
        /// <returns>True if all cells are available for placement, otherwise false.</returns>
        private bool CanPlaceFootprint(List<Vector2Int> footprint)
        {
            foreach (var cell in footprint)
                if (!IsInBounds(cell.x, cell.y) || _pieceIds[cell.x, cell.y] > 0)
                    return false;

            return true;
        }

        private int GetBreakPreviewPieceId()
        {
            if (ActiveTool != PathBuildTool.Break || !_hoveredCell || _pieceIds == null ||
                !IsInBounds(_hoveredCell.Column, _hoveredCell.Row))
                return 0;

            return _pieceIds[_hoveredCell.Column, _hoveredCell.Row];
        }

        private void RefreshPlacedVisualColors(int breakPreviewPieceId)
        {
            if (breakPreviewPieceId == _highlightedPieceId) return;

            SetPlacedVisualColor(_highlightedPieceId, placedPipeColor);
            SetPlacedVisualColor(breakPreviewPieceId, breakPreviewColor);
            _highlightedPieceId = breakPreviewPieceId;
        }

        private void SetPlacedVisualColor(int pieceId, Color color)
        {
            if (pieceId > 0 && _placedVisuals.TryGetValue(pieceId, out var visual))
                SetVisualColor(visual, color);
        }

        private void SetVisualColor(GameObject visual, Color color)
        {
            if (!visual) return;

            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                RendererColorUtility.SetColor(renderer, color, ref _colorPropertyBlock);
        }

        [Flags]
        private enum PipeConnections
        {
            None = 0,
            East = 1 << 0,
            North = 1 << 1,
            West = 1 << 2,
            South = 1 << 3
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
        public Vector3 GetPathWaypointPosition(Vector2Int position)
        {
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

            if (_pieceIds == null || _placedPieces.Count == 0)
                return false;

            if (!TryWorldToCell(worldPosition, out var cell))
                return false;

            var pieceId = _pieceIds[cell.x, cell.y];
            if (pieceId <= 0)
                return false;

            var piece = _placedPieces.Find(p => p.id == pieceId);
            if (piece == null)
                return false;

            var direction = piece.orientation == PathPieceOrientation.Horizontal
                ? transform.TransformDirection(Vector3.right)
                : transform.TransformDirection(Vector3.forward);

            rotation = Quaternion.LookRotation(direction, Vector3.up);
            return true;
        }

        /// <summary>
        ///     Returns true when this position is the currently discovered fork for a path using
        ///     this board. Live path previews keep the split point current as pieces are edited.
        /// </summary>
        public bool IsPathSplitPoint(Vector3 worldPosition)
        {
            foreach (var path in FindObjectsByType<WaypointPath>(FindObjectsInactive.Include))
                if (path && path.UsesBoard(this) && path.IsSplitPoint(worldPosition))
                    return true;

            return false;
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

            // Cell spacing includes both the cell itself and the gap between cells
            var step = cellSize + cellGap;

            // Reverse the GetLocalPosition math to recover column/row indices
            // (GetLocalPosition centers the grid, so we add the half-extent back)
            var column = Mathf.RoundToInt(local.x / step + (columns - 1) * 0.5f);
            var row = Mathf.RoundToInt(local.z / step + (rows - 1) * 0.5f);

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
            // Same cell, or exactly one axis differs by 1 (the other by 0)
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
