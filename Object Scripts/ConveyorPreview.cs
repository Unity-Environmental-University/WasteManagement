using System.Collections.Generic;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     The hover preview for a conveyor belt piece. The belt geometry is rebuilt only when the
    ///     previewed footprint or orientation changes; otherwise the existing preview is recolored.
    /// </summary>
    internal sealed class ConveyorPreview
    {
        private readonly PathBuildBoard _board;
        private GameObject _visual;
        private Vector2Int? _anchor;
        private int _length;
        private PathPieceOrientation _orientation;

        public ConveyorPreview(PathBuildBoard board)
        {
            _board = board;
        }

        public void Show(Vector2Int anchor, List<Vector2Int> footprint, PathPieceOrientation orientation,
            Color color)
        {
            if (!_visual || _anchor != anchor || _length != footprint.Count || _orientation != orientation)
            {
                if (_visual) _board.DestroyVisual(_visual);
                _visual = _board.CreatePipeVisual("Conveyor Preview");
                ConveyorVisual.Build(_visual.transform, _board, footprint, orientation);
                _anchor = anchor;
                _length = footprint.Count;
                _orientation = orientation;
            }

            _visual.SetActive(true);
            _board.SetVisualColor(_visual, color);
        }

        public void Hide()
        {
            if (_visual) _visual.SetActive(false);
        }

        /// <summary>Drops the reference after the board has destroyed its children.</summary>
        public void Forget()
        {
            _visual = null;
            _anchor = null;
        }
    }
}
