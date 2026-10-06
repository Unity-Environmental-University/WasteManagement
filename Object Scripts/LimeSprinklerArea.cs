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
        private MaterialPropertyBlock _propertyBlock;

        /// <summary>
        ///     Fills <paramref name="result" /> with the 3x3 block of in-bounds cells centred on
        ///     <paramref name="center" />, in column-major order.
        /// </summary>
        public static void GetCells(PathBuildBoard board, Vector2Int center, List<Vector2Int> result,
            bool includeCenter = true)
        {
            result.Clear();

            for (var columnOffset = -1; columnOffset <= 1; columnOffset++)
            for (var rowOffset = -1; rowOffset <= 1; rowOffset++)
            {
                if (!includeCenter && columnOffset == 0 && rowOffset == 0) continue;

                var cell = new Vector2Int(center.x + columnOffset, center.y + rowOffset);
                if (board.IsCellInBounds(cell)) result.Add(cell);
            }
        }

        /// <summary>Creates an empty, hidden area object; call <see cref="Show" /> to position it.</summary>
        public static LimeSprinklerArea Create(string name, Material material)
        {
            var area = new GameObject(name).AddComponent<LimeSprinklerArea>();
            for (var i = 0; i < 9; i++)
            {
                var tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
                tile.name = $"Tile {i}";
                // Purely visual: the tiles must not catch clicks meant for slots, cells or pipes.
                Destroy(tile.GetComponent<Collider>());
                tile.transform.SetParent(area.transform, false);

                var tileRenderer = tile.GetComponent<Renderer>();
                if (material) tileRenderer.sharedMaterial = material;
                tileRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tileRenderer.receiveShadows = false;
                area._tiles.Add(tileRenderer);
            }

            area.gameObject.SetActive(false);
            return area;
        }

        /// <summary>
        ///     Shows the 3x3 block around the cell under <paramref name="worldPosition" />. Hides the
        ///     area instead when that position is off-board.
        /// </summary>
        public void Show(PathBuildBoard board, Vector3 worldPosition, Color color)
        {
            if (!board || !board.TryWorldToCell(worldPosition, out var center))
            {
                Hide();
                return;
            }

            GetCells(board, center, _cells);

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
