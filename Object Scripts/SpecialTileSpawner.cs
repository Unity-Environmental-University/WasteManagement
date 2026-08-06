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

        [Tooltip("Roll a die for extra utility slots every time the town levels up.")]
        [SerializeField] private bool spawnUtilityTilesOnLevelUp = true;

        [Tooltip("Sides on the die rolled per level gained. 4 = D4, so 1-4 new slots per level.")]
        [SerializeField] [Min(1)] private int levelUpUtilityDieSides = 4;

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

        // Tracks the level the last OnLevelChanged reported so a level-up can be told apart from
        // the initial level broadcast in TurnController.GameStartSequence. -1 = nothing seen yet.
        private int _lastKnownLevel = -1;

        // Grid cells are built in PathBuildBoard.Awake, so any Start runs after the grid exists.
        private void Start()
        {
            SpawnRandomSlots();
        }

        private void OnEnable()
        {
            // Seed from the live level when the run is already underway (currentLevel is still 0
            // before GameStartSequence); otherwise the start-of-run broadcast seeds it instead.
            if (_lastKnownLevel < 0 && GameMaster.Instance && GameMaster.Instance.turnController &&
                GameMaster.Instance.turnController.currentLevel > 0)
                _lastKnownLevel = GameMaster.Instance.turnController.currentLevel;

            TurnController.OnLevelChanged += HandleLevelChanged;
        }

        private void OnDisable()
        {
            TurnController.OnLevelChanged -= HandleLevelChanged;
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
            if (randomizeUtilityPlacement) SpawnUtilityTiles(utilityTileCount);

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

#region UtilityTileFunctions

        /// <summary>
        ///     Instantiates up to <paramref name="count" /> utility slots on random cells that don't
        ///     already hold one. Returns the slots actually created — fewer than requested when the
        ///     board has run out of free cells.
        /// </summary>
        private List<SpecialInteractController> SpawnUtilityTiles(int count)
        {
            var spawned = new List<SpecialInteractController>();

            if (!board) board = GameMaster.Instance ? GameMaster.Instance.pathBuildBoard : null;
            if (!board || !utilityTilePrefab || count <= 0) return spawned;

            var occupied = OccupiedUtilityCells();

            foreach (var cell in GridSamplingHelper.PickUniqueRandomCells(board, count, occupied.Contains))
            {
                var position = board.GetCellTopPosition(cell) + Vector3.up * heightOffset;
                var slot = Instantiate(utilityTilePrefab, position, Quaternion.identity, transform);
                slot.name = $"{utilityTilePrefab.name} ({cell.x},{cell.y})";

                // Slots added mid-wave must stay hidden until the next card phase, which is when
                // TurnController re-enables every tile renderer.
                if (IsTowerPhase && slot.TryGetComponent<Renderer>(out var slotRenderer))
                    slotRenderer.enabled = false;

                var controller = slot.GetComponent<SpecialInteractController>();
                SpawnedUtilityTiles.Add(controller);
                spawned.Add(controller);
            }

            return spawned;
        }

        private static bool IsTowerPhase
        {
            get
            {
                var turnController = GameMaster.Instance ? GameMaster.Instance.turnController : null;
                return turnController && turnController.currentPhase == GamePhase.Tower;
            }
        }

        // Scans every slot in the scene, not just SpawnedUtilityTiles, so hand-placed slots also
        // block a cell from being picked again.
        private HashSet<Vector2Int> OccupiedUtilityCells()
        {
            return FindObjectsByType<SpecialInteractController>(FindObjectsInactive.Include)
                .Select(slot => board.WorldToCell(slot.transform.position))
                .ToHashSet();
        }

        private void HandleLevelChanged(int newLevel)
        {
            // The first event is TurnController broadcasting the starting level, not a level-up.
            if (_lastKnownLevel < 0)
            {
                _lastKnownLevel = newLevel;
                return;
            }

            var levelsGained = newLevel - _lastKnownLevel;
            _lastKnownLevel = newLevel;

            if (!spawnUtilityTilesOnLevelUp || levelsGained <= 0) return;

            // One die roll per level gained, so skipping several levels at once pays out for each.
            // TODO: Add visual for this.
            var count = 0;
            for (var i = 0; i < levelsGained; i++) count += Random.Range(1, levelUpUtilityDieSides + 1);

            var spawned = SpawnUtilityTiles(count);

            if (Debugging)
                Debug.Log($"[SpecialTileSpawner] Level {newLevel}: rolled {count} utility slot(s), " +
                          $"placed {spawned.Count}.");
        }

        #endregion


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