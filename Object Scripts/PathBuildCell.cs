using _project.Scripts.Core;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    /// Represents a single cell in the path building grid.
    /// Handles user interaction (clicks, hovers) for placing path pieces during the Card phase.
    /// Each cell tracks its grid position and communicates with the parent PathBuildBoard.
    /// </summary>
    public class PathBuildCell : MonoBehaviour
    {
        private PathBuildBoard _board;
        private Renderer _cellRenderer;

        /// <summary>
        /// The column index of this cell in the grid (X axis).
        /// </summary>
        public int Column { get; private set; }
        
        /// <summary>
        /// The row index of this cell in the grid (Z axis).
        /// </summary>
        public int Row { get; private set; }

        /// <summary>
        /// Handles mouse click on this cell. Attempts to place the pending path piece if:
        /// - GameMaster exists and is in the Card phase
        /// - A valid IPathPiecePlaceable is pending
        /// - The piece placement succeeds
        /// Consumes the selected item from inventory and refreshes board visuals on success.
        /// </summary>
        private void OnMouseDown()
        {
            var gm = GameMaster.Instance;
            if (!gm || !gm.turnController || gm.turnController.currentPhase != GamePhase.Card)
                return;

            if (gm.PendingPlacement is not IPathPiecePlaceable pending) return;

            var placed = pending.Place(transform);
            if (!placed) return;

            gm.placementInventory.ConsumeSelected();
            _board?.RefreshVisuals();
        }

        /// <summary>
        /// Notifies the board when the mouse enters this cell, triggering preview visuals.
        /// </summary>
        private void OnMouseEnter()
        {
            _board?.SetHoveredCell(this);
        }

        /// <summary>
        /// Notifies the board when the mouse exits this cell, clearing preview visuals.
        /// </summary>
        private void OnMouseExit()
        {
            _board?.ClearHoveredCell(this);
        }

        /// <summary>
        /// Initializes the cell with references to the parent board, grid position, and renderer.
        /// Called by PathBuildBoard during grid generation or binding.
        /// </summary>
        /// <param name="board">The parent PathBuildBoard managing this cell.</param>
        /// <param name="column">The column index in the grid.</param>
        /// <param name="row">The row index in the grid.</param>
        /// <param name="cellRenderer">The Renderer component for visual updates.</param>
        public void Initialize(PathBuildBoard board, int column, int row, Renderer cellRenderer)
        {
            _board = board;
            _cellRenderer = cellRenderer;
            Column = column;
            Row = row;
        }

        /// <summary>
        /// Updates the visual color of this cell (e.g., empty, occupied, or preview states).
        /// </summary>
        /// <param name="color">The color to apply to the cell's material.</param>
        public void SetColor(Color color)
        {
            if (_cellRenderer)
                _cellRenderer.material.color = color;
        }

        /// <summary>
        /// Delegates piece placement to the parent board.
        /// </summary>
        /// <param name="piece">The path piece to place.</param>
        /// <returns>The GameObject if placement succeeds, otherwise null.</returns>
        public GameObject TryPlace(IPathPiecePlaceable piece)
        {
            return _board ? _board.TryPlace(this, piece) : null;
        }
    }
}