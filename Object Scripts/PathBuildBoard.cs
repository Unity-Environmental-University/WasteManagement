using System;
using System.Collections.Generic;
using _project.Scripts.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    /// Defines the orientation of path pieces on the grid.
    /// </summary>
    public enum PathPieceOrientation
    {
        /// <summary>Path piece extends along the X axis (columns).</summary>
        Horizontal,
        /// <summary>Path piece extends along the Z axis (rows).</summary>
        Vertical
    }

    /// <summary>
    /// Manages a grid of PathBuildCell objects for placing path pieces.
    /// Handles grid generation, piece placement validation, visual previewing, and R-key rotation.
    /// Tracks all placed pieces and their occupancy via piece IDs.
    /// </summary>
    public class PathBuildBoard : MonoBehaviour
    {
        [Header("Grid")] [SerializeField] private int columns = 10;

        [SerializeField] private int rows = 10;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private float cellGap = 0.1f;
        [SerializeField] private float cellHeight = 0.1f;

        [Header("Visuals")] [SerializeField] private Color emptyColor = new(0.18f, 0.18f, 0.18f, 1f);

        [SerializeField] private Color occupiedColor = new(0.28f, 0.28f, 0.28f, 1f);
        [SerializeField] private Color validPreviewColor = new(0.35f, 0.8f, 1f, 1f);
        [SerializeField] private Color invalidPreviewColor = new(1f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color placedPipeColor = new(0.8f, 0.8f, 0.8f, 1f);
        [SerializeField] private float pipeVisualHeight = 0.2f;
        private readonly List<PlacedPathPiece> _placedPieces = new();
        private readonly Dictionary<int, GameObject> _placedVisuals = new();

        private PathBuildCell[,] _cells;
        private PathBuildCell _hoveredCell;
        private IPathPiecePlaceable _lastPreviewedPiece;
        private int _nextPieceId = 1;
        private int[,] _pieceIds; // Tracks which piece occupies each cell (0 = empty)
        private GameObject _previewVisual;
        private Transform _visualRoot;
        
        public float entityOnBoardHeight;

        /// <summary>
        /// Read-only collection of all path pieces that have been successfully placed on the board.
        /// </summary>
        public IReadOnlyList<PlacedPathPiece> PlacedPieces => _placedPieces;

        /// <summary>
        /// Initializes the grid by attempting to bind existing cells or building new ones.
        /// Refreshes visuals after setup.
        /// </summary>
        private void Awake()
        {
            if (!TryBindExistingCells())
                BuildGridIfNeeded();

            RefreshVisuals();
        }

        /// <summary>
        /// Monitors the pending placement piece from GameMaster and refreshes visuals when it changes.
        /// Handles R-key input to toggle the orientation of the selected piece.
        /// </summary>
        private void Update()
        {
            var selectedPiece =
                GameMaster.Instance ? GameMaster.Instance.PendingPlacement as IPathPiecePlaceable : null;
            if (selectedPiece != _lastPreviewedPiece)
            {
                _lastPreviewedPiece = selectedPiece;
                RefreshVisuals();
            }

            if (selectedPiece == null) return;

            if (Keyboard.current == null || !Keyboard.current[Key.R].wasPressedThisFrame) return;
            selectedPiece.ToggleOrientation();
            RefreshVisuals();
        }

        /// <summary>
        /// Clears all cells and placed pieces, then rebuilds the grid from scratch.
        /// Can be invoked from the Unity Editor context menu.
        /// </summary>
        [ContextMenu("Rebuild Grid")]
        public void RebuildGrid()
        {
            ClearGeneratedCells();
            _hoveredCell = null;
            _placedPieces.Clear();
            _placedVisuals.Clear();
            _nextPieceId = 1;
            BuildGridIfNeeded();
            RefreshVisuals();
        }

        /// <summary>
        /// Updates all cell colors based on occupancy and displays a preview visual for the pending piece.
        /// Shows valid (blue) or invalid (red) preview based on placement feasibility.
        /// </summary>
        public void RefreshVisuals()
        {
            if (_cells == null) return;

            for (var column = 0; column < columns; column++)
            for (var row = 0; row < rows; row++)
            {
                var cell = _cells[column, row];
                if (!cell) continue;

                cell.SetColor(_pieceIds[column, row] > 0 ? occupiedColor : emptyColor);
            }

            if (!_hoveredCell)
            {
                HidePreviewVisual();
                return;
            }

            var selectedPiece =
                GameMaster.Instance ? GameMaster.Instance.PendingPlacement as IPathPiecePlaceable : null;
            if (selectedPiece == null)
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
        /// Sets the currently hovered cell and refreshes visuals to show the preview.
        /// </summary>
        /// <param name="cell">The cell the mouse is hovering over.</param>
        public void SetHoveredCell(PathBuildCell cell)
        {
            _hoveredCell = cell;
            RefreshVisuals();
        }

        /// <summary>
        /// Clears the hovered cell if it matches the provided cell, hiding the preview visual.
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
        /// Attempts to place a path piece on the board starting at the anchor cell.
        /// Validates the footprint, assigns a unique piece ID, updates occupancy, and creates the visual.
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
                orientation = piece.Orientation
            };

            foreach (var cell in footprint)
            {
                _pieceIds[cell.x, cell.y] = placedPiece.id;
                placedPiece.cells.Add(cell);
            }

            _placedPieces.Add(placedPiece);
            var placedVisual = CreatePipeVisual($"Placed Pipe {placedPiece.id}", placedPipeColor);
            _placedVisuals[placedPiece.id] = placedVisual;
            UpdatePipeVisual(placedVisual, footprint, piece.Orientation, placedPipeColor);
            HidePreviewVisual();
            RefreshVisuals();
            return anchorCell.gameObject;
        }

        /// <summary>
        /// Calculates the list of grid cells occupied by a piece of a given length and orientation.
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
        /// Checks if a specific grid cell is occupied by any placed piece.
        /// </summary>
        /// <param name="column">The column index.</param>
        /// <param name="row">The row index.</param>
        /// <returns>True if the cell is in bounds and occupied, otherwise false.</returns>
        public bool IsOccupied(int column, int row)
        {
            return IsInBounds(column, row) && _pieceIds[column, row] > 0;
        }

        /// <summary>
        /// Convenience overload — checks if a cell is occupied using a Vector2Int.
        /// </summary>
        public bool IsOccupied(Vector2Int cell) => IsOccupied(cell.x, cell.y);

        /// <summary>
        /// Returns true if the given cell is within the grid bounds (public accessor
        /// for the private <see cref="IsInBounds(int,int)"/>).
        /// </summary>
        public bool IsCellInBounds(Vector2Int cell) => IsInBounds(cell.x, cell.y);

        /// <summary>
        /// Generates a new grid of PathBuildCell GameObjects at runtime.
        /// Each cell is a cube primitive with a trigger collider and a PathBuildCell component.
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
                rend.material = new Material(rend.sharedMaterial);

                var collisionComp = cellObject.GetComponent<Collider>();
                if (collisionComp) collisionComp.isTrigger = true;

                var cell = cellObject.AddComponent<PathBuildCell>();
                cell.Initialize(this, column, row, rend);
                _cells[column, row] = cell;
            }
        }

        /// <summary>
        /// Attempts to bind existing PathBuildCell children instead of generating new ones.
        /// Useful for preserving manually-placed cells in the scene.
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
        /// Destroys all child GameObjects (cells and visuals) and clears internal state.
        /// Uses DestroyImmediate in edit mode and Destroy in play mode.
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
        /// Gets or creates the "PipeVisuals" child GameObject that holds all pipe visual cubes.
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
        /// Lazily creates and returns the preview visual GameObject (only created once).
        /// </summary>
        /// <returns>The preview visual GameObject.</returns>
        private GameObject GetPreviewVisual()
        {
            if (_previewVisual) return _previewVisual;

            _previewVisual = CreatePipeVisual("Pipe Preview", validPreviewColor);
            return _previewVisual;
        }

        /// <summary>
        /// Hides the preview visual by deactivating it.
        /// </summary>
        private void HidePreviewVisual()
        {
            if (_previewVisual) _previewVisual.SetActive(false);
        }

        /// <summary>
        /// Creates a new cube GameObject for visualizing pipes (preview or placed).
        /// Collider is disabled so it doesn't interfere with mouse input on cells.
        /// </summary>
        /// <param name="visualName">The name of the GameObject.</param>
        /// <param name="color">The initial color of the visual.</param>
        /// <returns>The created visual GameObject (initially inactive).</returns>
        private GameObject CreatePipeVisual(string visualName, Color color)
        {
            if (_visualRoot == null)
                _visualRoot = GetOrCreateVisualRoot();

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = visualName;
            visual.layer = gameObject.layer;
            visual.transform.SetParent(_visualRoot, false);

            var collisionComp = visual.GetComponent<Collider>();
            if (collisionComp) collisionComp.enabled = false;

            var rend = visual.GetComponent<Renderer>();
            rend.material = new Material(rend.sharedMaterial)
            {
                color = color
            };
            visual.SetActive(false);
            return visual;
        }

        /// <summary>
        /// Updates a pipe visual's position, scale, and color based on the footprint and orientation.
        /// Calculates the center of the footprint and stretches the visual to span all cells.
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

            var firstCell = GetCell(footprint[0]);
            var lastCell = GetCell(footprint[^1]);
            if (!firstCell || !lastCell)
            {
                visual.SetActive(false);
                return;
            }

            var rend = visual.GetComponent<Renderer>();
            if (rend)
                rend.material.color = color;

            var firstPosition = firstCell.transform.localPosition;
            var lastPosition = lastCell.transform.localPosition;
            var center = (firstPosition + lastPosition) * 0.5f;
            var totalLength = cellSize * footprint.Count + cellGap * (footprint.Count - 1);
            var pipeWidth = Mathf.Max(cellSize * 0.6f, cellSize - cellGap);

            visual.transform.localPosition = new Vector3(center.x, (cellHeight + pipeVisualHeight) * 0.5f, center.z);
            visual.transform.localScale = orientation == PathPieceOrientation.Horizontal
                ? new Vector3(totalLength, pipeVisualHeight, pipeWidth)
                : new Vector3(pipeWidth, pipeVisualHeight, totalLength);
            visual.SetActive(true);
        }

        /// <summary>
        /// Calculates the local position for a cell at the given column and row.
        /// Centers the grid around the origin.
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
        /// Checks if all cells in the footprint are valid (in bounds and unoccupied).
        /// </summary>
        /// <param name="footprint">The list of grid cells to check.</param>
        /// <returns>True if all cells are available for placement, otherwise false.</returns>
        private bool CanPlaceFootprint(List<Vector2Int> footprint)
        {
            foreach (var cell in footprint)
            {
                if (!IsInBounds(cell.x, cell.y) || _pieceIds[cell.x, cell.y] > 0)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if a column and row are within the grid bounds.
        /// </summary>
        /// <param name="column">The column index.</param>
        /// <param name="row">The row index.</param>
        /// <returns>True if the position is valid, otherwise false.</returns>
        private bool IsInBounds(int column, int row)
        {
            return column >= 0 && column < columns && row >= 0 && row < rows;
        }

        /// <summary>
        /// Gets the PathBuildCell at the specified grid position.
        /// </summary>
        /// <param name="position">The grid position (column, row).</param>
        /// <returns>The PathBuildCell if in bounds, otherwise null.</returns>
        private PathBuildCell GetCell(Vector2Int position)
        {
            return IsInBounds(position.x, position.y) ? _cells[position.x, position.y] : null;
        }

        /// <summary>
        /// Returns the world-space position for the given grid cell. Falls back to the computed
        /// local position (transformed by this board) when the cell GameObject is missing.
        /// </summary>
        private Vector3 GetCellWorldPosition(Vector2Int position)
        {
            var cell = GetCell(position);
            if (cell) return cell.transform.position;
            return transform.TransformPoint(GetLocalPosition(position.x, position.y));
        }

        /// <summary>
        /// Returns a world-space waypoint position for the given cell — centered on the pipe's
        /// top surface so entities travel along the pipe rather than inside the grid.
        /// </summary>
        public Vector3 GetPathWaypointPosition(Vector2Int position)
        {
            var basePos = GetCellWorldPosition(position);
            return new Vector3(basePos.x, basePos.y + cellHeight * 0.5f + pipeVisualHeight, basePos.z);
        }

        /// <summary>
        /// Converts a world-space position into the nearest grid cell coordinate.
        /// Used to map fixed anchor Transforms (e.g., WaypointPath.startPoint/endPoint)
        /// onto the grid so we can test piece adjacency against them.
        /// Returns the clamped cell index even if the point is outside the grid bounds.
        /// </summary>
        public Vector2Int WorldToCell(Vector3 worldPosition)
        {
            // Transform world position into this board's local space
            var local = transform.InverseTransformPoint(worldPosition);

            // Cell spacing includes both the cell itself and the gap between cells
            var step = cellSize + cellGap;

            // Reverse the GetLocalPosition math to recover column/row indices
            // (GetLocalPosition centers the grid, so we add the half-extent back)
            var column = Mathf.RoundToInt(local.x / step + (columns - 1) * 0.5f);
            var row = Mathf.RoundToInt(local.z / step + (rows - 1) * 0.5f);

            // Clamp so out-of-bounds anchors snap to the nearest edge cell
            column = Mathf.Clamp(column, 0, columns - 1);
            row = Mathf.Clamp(row, 0, rows - 1);

            return new Vector2Int(column, row);
        }

        /// <summary>
        /// Returns true if two grid cells are 4-way neighbors (share an edge) OR identical.
        /// Diagonals are NOT considered adjacent — path pieces must link orthogonally.
        /// Identical cells return true so anchor Transforms placed directly on a piece's
        /// endpoint cell count as "connected" to that piece.
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
        /// Represents a path piece that has been successfully placed on the board.
        /// Stores metadata (ID, length, orientation) and the list of cells it occupies.
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
            /// <summary>The grid cells occupied by this piece.</summary>
            public List<Vector2Int> cells = new();
        }
    }
}