using System.Collections.Generic;
using _project.Scripts.Core;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     Optionally scatters SpecialInteract placement slots across random cells of the
    ///     <see cref="PathBuildBoard" /> grid once at game start. Each slot is instantiated from a
    ///     prefab. Placement ignores pipe occupancy (slots may land on path cells), but no two
    ///     spawned slots share the same cell.
    /// </summary>
    public class SpecialInteractSpawner : MonoBehaviour
    {
        [Header("Random Placement")]
        [SerializeField] private bool randomizePlacement = true;

        [SerializeField] [Min(3)] private int slotCount = 3;

        [SerializeField] private GameObject slotPrefab;

        [Tooltip("Extra height above the cell's top surface to place each slot at.")]
        [SerializeField] private float heightOffset = 0.45f;

        [Header("References")]
        [Tooltip("Board to scatter slots across. Falls back to GameMaster.Instance.pathBuildBoard.")]
        [SerializeField] private PathBuildBoard board;

        private static bool Debugging => GameMaster.Instance && GameMaster.Instance.debugging;

        // Grid cells are built in PathBuildBoard.Awake, so any Start runs after the grid exists.
        private void Start()
        {
            if (randomizePlacement) SpawnRandomSlots();
        }

        /// <summary>
        ///     Picks unique random grid cells and instantiates a slot prefab on each.
        ///     Exposed for editor testing via the context menu.
        /// </summary>
        [ContextMenu("Spawn Random Slots")]
        public void SpawnRandomSlots()
        {
            if (!board) board = GameMaster.Instance ? GameMaster.Instance.pathBuildBoard : null;

            if (!board || !slotPrefab)
            {
                Debug.LogWarning("[SpecialInteractSpawner] Missing board/prefab or non-positive count; skipping.");
                return;
            }

            foreach (var cell in PickUniqueRandomCells(slotCount))
            {
                var position = board.GetCellTopPosition(cell) + Vector3.up * heightOffset;
                var slot = Instantiate(slotPrefab, position, Quaternion.identity, transform);
                slot.name = $"{slotPrefab.name} ({cell.x},{cell.y})";
            }

            if (Debugging) Debug.Log($"[SpecialInteractSpawner] Spawned up to {slotCount} slots on the grid.");
        }

        /// <summary>
        ///     Returns up to <paramref name="count" /> distinct random grid cells by shuffling the
        ///     full set of cell indices and taking the first few (clamped to the cell count).
        /// </summary>
        private List<Vector2Int> PickUniqueRandomCells(int count)
        {
            var total = board.Columns * board.Rows;
            count = Mathf.Min(count, total);

            var indices = new List<int>(total);
            for (var i = 0; i < total; i++) indices.Add(i);

            // Partial Fisher-Yates: only the first `count` slots need to be settled.
            for (var i = 0; i < count; i++)
            {
                var j = Random.Range(i, total);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            var result = new List<Vector2Int>(count);
            for (var i = 0; i < count; i++)
            {
                var index = indices[i];
                result.Add(new Vector2Int(index % board.Columns, index / board.Columns));
            }

            return result;
        }
    }
}