using System.Collections.Generic;
using System.Linq;
using _project.Scripts.Core;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    public class SpecialTileSpawner : MonoBehaviour
    {
#region Utility Spawner
        
        [Header("Utility Spawner Configuration")]
        [SerializeField] private bool randomizeUtilityPlacement = true;
        [SerializeField] [Min(3)] private int utilityTileCount = 3;
        [SerializeField] private GameObject utilityTilePrefab;

        #endregion

#region Buff Spawner

     [Header("Buff/Debuff Spawner Configuration")]
        [SerializeField] private bool randomizeBuffPlacement = true;
        [SerializeField] [Min(0)] private int buffTileCount = 3;
        [SerializeField] private Vector3 tileScaleMultiplier = Vector3.one;
        [SerializeField] private BuffDebuffTileController buffTilePrefab;
        
        [Tooltip("Pool of possible effects. Each spawned tile randomly gets one option.")]
        [SerializeField] private BuffDebuffTileEffect[] effectOptions;

        #endregion

        [Tooltip("Extra height above the cell's top surface to place each slot at.")]
        [SerializeField] private float heightOffset = 0.45f;

        [Header("References")]
        [Tooltip("Board to scatter slots across. Falls back to GameMaster.Instance.pathBuildBoard.")]
        [SerializeField] private PathBuildBoard board;

        private static bool Debugging => GameMaster.Instance && GameMaster.Instance.debugging;
        
        // Active Lists
        public List<SpecialInteractController> SpawnedUtilityTiles { get; } = new();
        public List<BuffDebuffTileController> SpawnedBuffTiles { get; } = new();

        // Grid cells are built in PathBuildBoard.Awake, so any Start runs after the grid exists.
        private void Start()
        { 
            SpawnRandomSlots();
        }

        /// <summary>
        ///     Picks unique random grid cells and instantiates a slot prefab on each.
        ///     Exposed for editor testing via the context menu.
        /// </summary>
        [ContextMenu("Spawn Random Slots")]
        public void SpawnRandomSlots()
        {
            if (!board) board = GameMaster.Instance ? GameMaster.Instance.pathBuildBoard : null;

            if (!board)
            {
                Debug.LogWarning("[SpecialTileSpawner] Missing board/prefab or non-positive count; skipping.");
                return;
            }

            // Spawn Utility Tiles
            if (randomizeUtilityPlacement && utilityTilePrefab)
            {
                foreach (var cell in GridSamplingHelper.PickUniqueRandomCells(board, utilityTileCount))
                {
                    var position = board.GetCellTopPosition(cell) + Vector3.up * heightOffset;
                    var slot = Instantiate(utilityTilePrefab, position, Quaternion.identity, transform);
                    slot.name = $"{utilityTilePrefab.name} ({cell.x},{cell.y})";

                    SpawnedUtilityTiles.Add(slot.GetComponent<SpecialInteractController>());
                }
            }
            
            // Spawn Buff/Debuff Tiles
            if (randomizeBuffPlacement &&  buffTilePrefab)
            {
                foreach (var cell in GridSamplingHelper.PickUniqueRandomCells(board, buffTileCount))
                {
                    // One tile per cell: PickUniqueRandomCells only dedupes within this batch,
                    // so skip cells that already hold a tile from an earlier scatter or burial.
                    if (FindTileAt(cell)) continue;

                    var position = board.GetCellTopPosition(cell) + Vector3.up * heightOffset;

                    var controller = CreateTile(position);
                    controller.gameObject.name = $"BuffDebuffTile ({cell.x},{cell.y})";
                    controller.SetKind(Random.value < 0.5f ? BuffDebuffKind.Buff : BuffDebuffKind.Debuff);
                    controller.SetEffect(PickRandomEffect());

                    SpawnedBuffTiles.Add(controller);
                }
            }
            if (Debugging) Debug.Log($"[SpecialTileSpawner] Spawned up to {utilityTileCount} slots on the grid.");
        }


#region BuffTileFunctions

        // Scans every tile in the scene, not just _spawnedTiles, so tiles owned by another
        // spawner or dropped into the scene by hand still count toward one-tile-per-cell.
        private BuffDebuffTileController FindTileAt(Vector2Int cell)
        {
            return FindObjectsByType<BuffDebuffTileController>()
                .FirstOrDefault(tile => board.WorldToCell(tile.transform.position) == cell);
        }

        private BuffDebuffTileController CreateTile(Vector3 position)
        {
            if (buffTilePrefab)
                return Instantiate(buffTilePrefab, position, Quaternion.identity, transform);

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
            for (var i = SpawnedBuffTiles.Count - 1; i >= 0; i--)
                if (SpawnedBuffTiles[i])
                    SafeDestroy(SpawnedBuffTiles[i].gameObject);

            SpawnedBuffTiles.Clear();
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

        #endregion

        public void SpawnBuffTile(bool isDebuff, Vector3 position)
        {
            if (!board) board = GameMaster.Instance ? GameMaster.Instance.pathBuildBoard : null;

            if (!board)
            {
                Debug.LogWarning("[BuffDebuffTileSpawner] Missing board; skipping.");
                return;
            }

            var cell = board.WorldToCell(position);
            var existing = FindTileAt(cell);
            if (existing)
            {
                existing.SetKind(isDebuff ? BuffDebuffKind.Debuff : BuffDebuffKind.Buff);
                existing.SetEffect(PickRandomEffect());
                return;
            }

            var controller = CreateTile(position);
            controller.gameObject.name = $"BuffDebuffTile ({cell.x},{cell.y})";
            controller.SetKind(isDebuff ? BuffDebuffKind.Debuff : BuffDebuffKind.Buff);
            controller.SetEffect(PickRandomEffect());

            SpawnedBuffTiles.Add(controller);
        }
    }
}