using System.Collections.Generic;
using System.Linq;
using _project.Scripts.Core;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     Scatters a handful of buff/debuff tiles across random cells of the
    ///     <see cref="PathBuildBoard" /> grid once at game start. Each tile is randomly a
    ///     buff or a debuff. No two tiles share a cell, but tiles may overlap other things
    ///     on the board.
    /// </summary>
    public class BuffDebuffTileSpawner : MonoBehaviour
    {
        [Header("Random Placement")]
        [SerializeField] private bool randomizePlacement = true;
        [SerializeField] [Min(0)] private int tileCount = 3;

        [Tooltip("Extra height above the cell's top surface to place each tile at.")]
        [SerializeField] private float heightOffset = 0.45f;

        [Tooltip("Scale of generated fallback tiles, relative to one board cell (1,1,1 = exactly cell-sized).")]
        [SerializeField] private Vector3 tileScaleMultiplier = Vector3.one;

        [Header("Tile Effects")]
        [Tooltip("Optional visual/controller prefab. Effects are assigned from Effect Options below.")]
        [SerializeField] private BuffDebuffTileController tilePrefab;

        [Tooltip("Pool of possible effects. Each spawned tile randomly gets one option.")]
        [SerializeField] private BuffDebuffTileEffect[] effectOptions;

        [Header("References")]
        [Tooltip("Board to scatter tiles across. Falls back to GameMaster.Instance.pathBuildBoard.")]
        [SerializeField] private PathBuildBoard board;

        private readonly List<BuffDebuffTileController> _spawnedTiles = new();
        private static bool Debugging => GameMaster.Instance && GameMaster.Instance.debugging;

        private void Start()
        {
            if (randomizePlacement)
                SpawnRandomTiles();
        }

        [ContextMenu("Spawn Random Buff/Debuff Tiles")]
        public void SpawnRandomTiles()
        {
            if (!board) board = GameMaster.Instance ? GameMaster.Instance.pathBuildBoard : null;

            if (!board)
            {
                Debug.LogWarning("[BuffDebuffTileSpawner] Missing board; skipping.");
                return;
            }

            foreach (var cell in GridSamplingHelper.PickUniqueRandomCells(board, tileCount))
            {
                // One tile per cell: PickUniqueRandomCells only dedupes within this batch,
                // so skip cells that already hold a tile from an earlier scatter or burial.
                if (FindTileAt(cell)) continue;

                var position = board.GetCellTopPosition(cell) + Vector3.up * heightOffset;

                var controller = CreateTile(position);
                controller.gameObject.name = $"BuffDebuffTile ({cell.x},{cell.y})";
                controller.SetKind(Random.value < 0.5f ? BuffDebuffKind.Buff : BuffDebuffKind.Debuff);
                controller.SetEffect(PickRandomEffect());

                _spawnedTiles.Add(controller);
            }

            if (Debugging)
                Debug.Log($"[BuffDebuffTileSpawner] Spawned {_spawnedTiles.Count} buff/debuff tiles.");
        }

        /// <summary>
        ///     Spawns a single debuff tile snapped to the board cell containing
        ///     <paramref name="worldPosition" /> (e.g., where a cesspit was just buried).
        /// </summary>
        public BuffDebuffTileController SpawnDebuffTileAt(Vector3 worldPosition)
        {
            if (!board) board = GameMaster.Instance ? GameMaster.Instance.pathBuildBoard : null;

            if (!board)
            {
                Debug.LogWarning("[BuffDebuffTileSpawner] Missing board; skipping.");
                return null;
            }

            var cell = board.WorldToCell(worldPosition);

            // One tile per cell: if the cell already has a tile, flip it to a debuff
            // instead of stacking a second tile (and second trigger) on top of it.
            var existing = FindTileAt(cell);
            if (existing)
            {
                existing.SetKind(BuffDebuffKind.Debuff);
                existing.SetEffect(PickRandomEffect());
                return existing;
            }

            var position = board.GetCellTopPosition(cell) + Vector3.up * heightOffset;

            var controller = CreateTile(position);
            controller.gameObject.name = $"BuffDebuffTile ({cell.x},{cell.y})";
            controller.SetKind(BuffDebuffKind.Debuff);
            controller.SetEffect(PickRandomEffect());

            _spawnedTiles.Add(controller);
            return controller;
        }

        // Scans every tile in the scene, not just _spawnedTiles, so tiles owned by another
        // spawner or dropped into the scene by hand still count toward one-tile-per-cell.
        private BuffDebuffTileController FindTileAt(Vector2Int cell)
        {
            return FindObjectsByType<BuffDebuffTileController>()
                .FirstOrDefault(tile => board.WorldToCell(tile.transform.position) == cell);
        }

        private BuffDebuffTileController CreateTile(Vector3 position)
        {
            if (tilePrefab)
                return Instantiate(tilePrefab, position, Quaternion.identity, transform);

            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.transform.SetParent(transform);
            tile.transform.position = position;
            tile.transform.localScale = Vector3.Scale(board.CellWorldSize, tileScaleMultiplier);

            var controller = tile.AddComponent<BuffDebuffTileController>();
            return controller;
        }

        private BuffDebuffTileEffect PickRandomEffect()
        {
            return effectOptions is { Length: > 0 }
                ? effectOptions[Random.Range(0, effectOptions.Length)]
                : null;
        }

        [ContextMenu("Clear Spawned Buff/Debuff Tiles")]
        public void ClearSpawnedTiles()
        {
            for (var i = _spawnedTiles.Count - 1; i >= 0; i--)
                if (_spawnedTiles[i])
                    SafeDestroy(_spawnedTiles[i].gameObject);

            _spawnedTiles.Clear();
        }

        // Destroy works at runtime; DestroyImmediate is required from the edit-time context menus.
        private static void SafeDestroy(Object obj)
        {
            if (!obj) return;

            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
        }
    }
}
