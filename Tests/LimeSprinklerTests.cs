using System.Collections.Generic;
using System.Reflection;
using _project.Scripts.Object_Scripts;
using NUnit.Framework;
using UnityEngine;

namespace _project.Scripts.Tests
{
    /// <summary>
    ///     Covers the lime sprinkler's stink suppression: a sprinkler reduces the stink of every
    ///     cesspit in its 3x3 block, in either placement order, and the reduction keeps applying as
    ///     the pit fills up. Placement is simulated by calling the same <c>SetSlot</c> hooks
    ///     <see cref="SpecialInteractController" /> calls when an item is dropped into a slot.
    /// </summary>
    public class LimeSprinklerTests
    {
        private const float BaseStink = 1f;
        private const float FullStinkBonus = 2f;
        private const float Reduction = 0.5f;

        private readonly List<GameObject> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
                if (go)
                    Object.DestroyImmediate(go);

            _created.Clear();
        }

        [Test]
        public void SetSlot_ReducesStink_OfCesspitAlreadyAdjacent()
        {
            var board = CreateBoard();
            var pit = CreateCesspit(board, new Vector2Int(3, 3));

            Assert.AreEqual(BaseStink, pit.CurrentStink, 0.0001f, "precondition: pit starts at base stink");

            CreateSprinkler(board, new Vector2Int(4, 3)).SetSlot(null);

            Assert.AreEqual(BaseStink - Reduction, pit.CurrentStink, 0.0001f);
        }

        [Test]
        public void SetSlot_ReducesStink_OfCesspitPlacedAfterTheSprinkler()
        {
            var board = CreateBoard();
            CreateSprinkler(board, new Vector2Int(4, 3)).SetSlot(null);

            // Placed later — the pit picks the sprinkler up from its own side of the pairing.
            var pit = CreateCesspit(board, new Vector2Int(3, 3));
            pit.SetSlot(null);

            Assert.AreEqual(BaseStink - Reduction, pit.CurrentStink, 0.0001f);
        }

        [Test]
        public void SetSlot_ReducesStink_OfDiagonalNeighbour()
        {
            var board = CreateBoard();
            var pit = CreateCesspit(board, new Vector2Int(3, 3));

            CreateSprinkler(board, new Vector2Int(4, 4)).SetSlot(null);

            Assert.AreEqual(BaseStink - Reduction, pit.CurrentStink, 0.0001f);
        }

        [Test]
        public void SetSlot_LeavesCesspit_OutsideTheBlockUntouched()
        {
            var board = CreateBoard();
            var pit = CreateCesspit(board, new Vector2Int(3, 3));

            // Two cells away on both axes: outside the 3x3 in either placement order.
            var sprinkler = CreateSprinkler(board, new Vector2Int(5, 5));
            sprinkler.SetSlot(null);
            pit.SetSlot(null);

            Assert.AreEqual(BaseStink, pit.CurrentStink, 0.0001f);
        }

        [Test]
        public void Reduction_KeepsApplying_AsFullnessRaisesStink()
        {
            var board = CreateBoard();
            var pit = CreateCesspit(board, new Vector2Int(3, 3));
            CreateSprinkler(board, new Vector2Int(4, 3)).SetSlot(null);

            // Fill the pit after the one-shot placement sweep has already run.
            pit.fullness = pit.maxFullness;

            // Stink is recomputed from fullness on every read, so the reduction must still bite.
            Assert.AreEqual(BaseStink + FullStinkBonus - Reduction, pit.CurrentStink, 0.0001f);
        }

        [Test]
        public void TwoSprinklers_CoveringTheSamePit_StackTheirReductions()
        {
            var board = CreateBoard();
            var pit = CreateCesspit(board, new Vector2Int(3, 3));

            CreateSprinkler(board, new Vector2Int(4, 3)).SetSlot(null);
            CreateSprinkler(board, new Vector2Int(2, 3)).SetSlot(null);

            Assert.AreEqual(BaseStink - Reduction * 2f, pit.CurrentStink, 0.0001f);
        }

        [Test]
        public void Stink_IsFlooredAtZero_WhenReductionExceedsIt()
        {
            var board = CreateBoard();
            var pit = CreateCesspit(board, new Vector2Int(3, 3));

            var sprinkler = CreateSprinkler(board, new Vector2Int(4, 3));
            SetField(sprinkler, "limeStinkReduction", BaseStink * 10f);
            sprinkler.SetSlot(null);

            Assert.AreEqual(0f, pit.CurrentStink, 0.0001f);
        }

        [Test]
        public void TryApplyTo_ReturnsFalse_WhenSprinklerSitsOffBoard()
        {
            var board = CreateBoard();
            var pit = CreateCesspit(board, new Vector2Int(3, 3));

            var sprinkler = CreateSprinkler(board, new Vector2Int(3, 3));
            sprinkler.transform.position = board.transform.position + Vector3.right * 10_000f;

            // No 3x3 can be resolved, so nothing is applied — and nothing dereferences a null board.
            Assert.IsFalse(sprinkler.TryApplyTo(pit));
            Assert.AreEqual(BaseStink, pit.CurrentStink, 0.0001f);
        }

        [Test]
        public void TryApplyTo_ReturnsFalse_WhenPitIsNull()
        {
            var board = CreateBoard();

            Assert.IsFalse(CreateSprinkler(board, new Vector2Int(3, 3)).TryApplyTo(null));
        }

        private PathBuildBoard CreateBoard()
        {
            return CreateGameObject("Path Board").AddComponent<PathBuildBoard>();
        }

        private Cesspit CreateCesspit(PathBuildBoard board, Vector2Int cell)
        {
            var pit = CreateGameObject($"Cesspit {cell}").AddComponent<Cesspit>();
            pit.transform.position = PositionOn(board, cell);
            pit.maxFullness = 10f;
            pit.fullness = 0f;
            SetField(pit, "baseStink", BaseStink);
            SetField(pit, "fullStinkBonus", FullStinkBonus);
            return pit;
        }

        private LimeSprinkler CreateSprinkler(PathBuildBoard board, Vector2Int cell)
        {
            var sprinkler = CreateGameObject($"Lime Sprinkler {cell}").AddComponent<LimeSprinkler>();
            sprinkler.transform.position = PositionOn(board, cell);
            SetField(sprinkler, "board", board);
            SetField(sprinkler, "limeStinkReduction", Reduction);
            return sprinkler;
        }

        /// <summary>
        ///     World position for <paramref name="cell" />, asserting it round-trips back to the same
        ///     cell — otherwise an out-of-bounds fixture cell would make the "outside the block" tests
        ///     pass for the wrong reason.
        /// </summary>
        private static Vector3 PositionOn(PathBuildBoard board, Vector2Int cell)
        {
            var position = board.GetCellTopPosition(cell);
            Assert.IsTrue(board.TryWorldToCell(position, out var roundTrip),
                $"fixture cell {cell} is off-board");
            Assert.AreEqual(cell, roundTrip, $"fixture cell {cell} did not round-trip");
            return position;
        }

        private GameObject CreateGameObject(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        private static void SetField<T>(object target, string fieldName, T value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field {fieldName} on {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }
}
