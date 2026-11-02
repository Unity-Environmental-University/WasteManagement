using System.Collections.Generic;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     Flat tinted tiles laid over the board cells a <see cref="LimeSprinkler" /> affects. Used
    ///     twice: a bold copy follows the cursor over utility slots while a sprinkler is being placed,
    ///     and a fainter copy stays under each placed sprinkler. Tiles are separate quads rather than
    ///     cell tints, so the board's own per-hover repaint never overwrites them.
    /// </summary>
    public class LimeSprinklerArea : MonoBehaviour
    {
        // Lifts the tiles off the cell surface so they don't z-fight with it.
        private const float SurfaceOffset = 0.01f;

        private readonly List<Renderer> _tiles = new(9);
        private readonly List<Vector2Int> _cells = new(9);
        private Material _material;
        private MaterialPropertyBlock _propertyBlock;

        /// <summary>
        ///     Fills <paramref name="result" /> with the <paramref name="size" /> x <paramref name="size" />
        ///     block of in-bounds cells centred on <paramref name="center" />, in column-major order.
        ///     An even size can't centre on a cell, so its extra row and column fall on the far side.
        /// </summary>
        public static void GetCells(PathBuildBoard board, Vector2Int center, int size, List<Vector2Int> result)
        {
            result.Clear();
            size = Mathf.Max(1, size);
            var low = -(size - 1) / 2;
            var high = low + size - 1;

            for (var columnOffset = low; columnOffset <= high; columnOffset++)
            for (var rowOffset = low; rowOffset <= high; rowOffset++)
            {
                var cell = new Vector2Int(center.x + columnOffset, center.y + rowOffset);
                if (board.IsCellInBounds(cell)) result.Add(cell);
            }
        }

        /// <summary>Creates an empty, hidden area object; call <see cref="Show" /> to position it.</summary>
        public static LimeSprinklerArea Create(string name, Material material)
        {
            var area = new GameObject(name).AddComponent<LimeSprinklerArea>();
            area._material = material;
            area.gameObject.SetActive(false);
            return area;
        }

        // Tiles are added as the area grows with the town's level and kept for reuse after that.
        private Renderer GetTile(int index)
        {
            while (_tiles.Count <= index)
            {
                var tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
                tile.name = $"Tile {_tiles.Count}";
                // Purely visual: the tiles must not catch clicks meant for slots, cells or pipes.
                Destroy(tile.GetComponent<Collider>());
                tile.transform.SetParent(transform, false);

                var tileRenderer = tile.GetComponent<Renderer>();
                if (_material) tileRenderer.sharedMaterial = _material;
                tileRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tileRenderer.receiveShadows = false;
                _tiles.Add(tileRenderer);
            }

            return _tiles[index];
        }

        /// <summary>
        ///     Shows the <paramref name="areaSize" /> x <paramref name="areaSize" /> block around the cell under
        ///     <paramref name="worldPosition" />. Hides the area instead when that position is off-board.
        /// </summary>
        public void Show(PathBuildBoard board, Vector3 worldPosition, int areaSize, Color color)
        {
            if (!board || !board.TryWorldToCell(worldPosition, out var center))
            {
                Hide();
                return;
            }

            GetCells(board, center, areaSize, _cells);
            if (_cells.Count > 0) GetTile(_cells.Count - 1);

            var boardTransform = board.transform;
            var size = board.CellWorldSize;
            // Quads face -Z; tipping them 90° about X lays them flat, facing the board's up.
            var rotation = boardTransform.rotation * Quaternion.Euler(90f, 0f, 0f);
            // GetCellTopPosition only adds the unscaled half-height, which leaves the tiles inside
            // the cell on a scaled board; lifting by the scaled half-height clears the surface.
            var lift = boardTransform.up * (size.y * 0.5f + SurfaceOffset);

            for (var i = 0; i < _tiles.Count; i++)
            {
                var tile = _tiles[i];
                var active = i < _cells.Count;
                tile.gameObject.SetActive(active);
                if (!active) continue;

                var tileTransform = tile.transform;
                tileTransform.SetPositionAndRotation(
                    board.GetCellTopPosition(_cells[i]) + lift, rotation);
                tileTransform.localScale = new Vector3(size.x, size.z, 1f);
                RendererColorUtility.SetColor(tile, color, ref _propertyBlock);
            }

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
