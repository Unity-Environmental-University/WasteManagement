using System.Collections.Generic;
using _project.Scripts.Core;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     Scatters a handful of buff/debuff tiles across random cells of the
    ///     <see cref="PathBuildBoard" /> grid once at game start. Each tile is randomly a
    ///     buff or a debuff and, for now, differs only by color — no gameplay effect yet.
    ///     No two tiles share a cell, but tiles may overlap other things on the board.
    /// </summary>
    public class BuffDebuffTileSpawner : MonoBehaviour
    {
        [Header("Random Placement")]
        [SerializeField] private bool randomizePlacement = true;
        [SerializeField] [Min(0)] private int tileCount = 3;

        [Tooltip("Extra height above the cell's top surface to place each tile at.")]
        [SerializeField] private float heightOffset = 0.12f;

        [Tooltip("Footprint of the generated tile across the cell (X/Z).")]
        [SerializeField] private float tileSize = 0.8f;

        [Tooltip("Flat thickness (Y) of the generated tile.")]
        [SerializeField] private float tileThickness = 0.08f;

        [Header("References")]
        [Tooltip("Board to scatter tiles across. Falls back to GameMaster.Instance.pathBuildBoard.")]
        [SerializeField] private PathBuildBoard board;

        private readonly List<GameObject> _spawnedTiles = new();
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
                var position = board.GetCellTopPosition(cell) + Vector3.up * heightOffset;

                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SafeDestroy(tile.GetComponent<Collider>()); // visual only — no physics needed yet
                tile.name = $"BuffDebuffTile ({cell.x},{cell.y})";
                tile.transform.SetParent(transform);
                tile.transform.position = position;
                tile.transform.localScale = new Vector3(tileSize, tileThickness, tileSize);

                var controller = tile.AddComponent<BuffDebuffTileController>();
                controller.SetKind(Random.value < 0.5f ? BuffDebuffKind.Buff : BuffDebuffKind.Debuff);

                _spawnedTiles.Add(tile);
            }

            if (Debugging)
                Debug.Log($"[BuffDebuffTileSpawner] Spawned {_spawnedTiles.Count} buff/debuff tiles.");
        }

        [ContextMenu("Clear Spawned Buff/Debuff Tiles")]
        public void ClearSpawnedTiles()
        {
            for (var i = _spawnedTiles.Count - 1; i >= 0; i--)
                SafeDestroy(_spawnedTiles[i]);

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
