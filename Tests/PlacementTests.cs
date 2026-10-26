using _project.Scripts.Core;
using _project.Scripts.Object_Scripts;
using NUnit.Framework;
using UnityEngine;

namespace _project.Scripts.Tests
{
    /// <summary>Where each shop item may be placed on the board, and what a board click places or breaks.</summary>
    public class PlacementTests : PathTestBase
    {
        [Test]
        public void PathSplitterShopItem_PlacesOnCellWithoutPipe()
        {
            var board = CreateBoardWithGameMaster();
            var prefab = CreateGameObject("Path Splitter Prefab");
            prefab.AddComponent<PathSplitter>();
            var item = new PathSplitterShopItem("Path Splitter", string.Empty, 1, prefab, null, 1);
            var slot = GetCell(board, 1, 2).transform;

            Assert.IsFalse(board.IsOccupied(new Vector2Int(1, 2)));
            var placed = Track(item.Place(slot));

            Assert.IsNotNull(placed);
            Assert.AreEqual(slot.position, placed.transform.position);
        }

        [Test]
        public void LimeSprinklerPlace_SucceedsOnEmptyUtilityCell()
        {
            var board = CreateBoardWithGameMaster();
            var item = new LimeSprinklerShopItem("Lime Sprinkler", "", 1, CreatePrimitive("Lime Sprinkler Prefab"),
                null, 2);
            var slot = CreateSlotOn(board, new Vector2Int(1, 1));

            Assert.IsFalse(board.IsOccupied(new Vector2Int(1, 1)));
            var placed = Track(item.Place(slot));

            Assert.IsNotNull(placed);
            Assert.AreEqual(slot.position, placed.transform.position);
        }

        [Test]
        public void NonLimePlaceables_ReturnNullOnEmptyUtilityCell()
        {
            var board = CreateBoardWithGameMaster();
            var prefab = CreatePrimitive("Placement Prefab");
            var slot = CreateSlotOn(board, new Vector2Int(1, 1));

            IPlaceable[] items =
            {
                new TowerShopItem("Tower", "", 1, prefab, null, 1),
                new SifterShopItem("Sifter", "", 1, prefab, null, 1),
                new CesspitShopItem("Cesspit", "", 1, prefab, null, 1),
                new TreatmentTankShopItem("Treatment Tank", "", 1, prefab, null, 1)
            };

            Assert.IsFalse(board.IsOccupied(new Vector2Int(1, 1)));
            foreach (var item in items)
                Assert.IsNull(item.Place(slot), $"{item.DisplayName} should require pipe beneath its slot.");
        }

        [Test]
        public void SifterPlace_ReturnsNull_WhenSlotIsOutsideBoardNearOccupiedEdge()
        {
            var board = CreateBoardWithGameMaster();
            var item = new SifterShopItem("Sifter", "", 1, CreatePrimitive("Sifter Prefab"), null, 1);
            PlaceVertical(board, 1, 0, 2);

            Assert.IsNull(item.Place(CreateSlotOn(board, new Vector2Int(1, -1))));
        }

        [Test]
        public void BuffDebuffTile_Awake_IgnoresPointerRaycastsWhileKeepingTrigger()
        {
            var tileGo = CreateGameObject("Buff Tile");
            var childGo = CreateGameObject("Buff Tile Child");
            var ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            childGo.transform.SetParent(tileGo.transform);

            tileGo.AddComponent<BuffDebuffTileController>();

            Assert.AreEqual(ignoreRaycastLayer, tileGo.layer);
            Assert.AreEqual(ignoreRaycastLayer, childGo.layer);
            Assert.IsTrue(tileGo.GetComponent<Collider>().isTrigger);
        }

        [Test]
        public void OnMouseDown_PathPlacementThenBreak_TracksInfrastructureValueAndMoves()
        {
            var board = CreateGameObject("Path Board").AddComponent<PathBuildBoard>();
            var gm = CreateGameObject("Game Master").AddComponent<GameMaster>();
            var piece = new PathPiecePlaceable("Pipe", "", 1, 2, null, 3);
            var targetCell = GetCell(board, 1, 1);
            gm.turnController.currentPhase = GamePhase.Card;
            board.SetActivePiece(piece);

            targetCell.SendMessage("OnMouseDown");

            Assert.AreEqual(3, gm.turnController.infrastructureValue);
            Assert.AreEqual(1, gm.turnController.moveCount);
            Assert.IsNull(gm.PendingPlacement);
            Assert.AreEqual(piece, board.ActivePiece, "The pipe stays armed for the next placement.");
            Assert.AreEqual(1, board.PlacedPieces.Count);
            Assert.IsTrue(board.IsOccupied(new Vector2Int(1, 1)));
            Assert.IsTrue(board.IsOccupied(new Vector2Int(2, 1)));

            board.SetActiveBreakTool();
            targetCell.SendMessage("OnMouseDown");

            Assert.AreEqual(0, board.PlacedPieces.Count);
            Assert.IsFalse(board.IsOccupied(new Vector2Int(1, 1)));
            Assert.IsFalse(board.IsOccupied(new Vector2Int(2, 1)));
            Assert.AreEqual(2, gm.turnController.moveCount);
            Assert.AreEqual(0, gm.turnController.infrastructureValue);
        }

        private PathBuildBoard CreateBoardWithGameMaster()
        {
            var board = CreateGameObject("Path Board").AddComponent<PathBuildBoard>();
            CreateGameObject("Game Master").AddComponent<GameMaster>().pathBuildBoard = board;
            return board;
        }

        private Transform CreateSlotOn(PathBuildBoard board, Vector2Int cell)
        {
            var slot = CreateGameObject($"Slot {cell}").transform;
            slot.position = board.GetCellTopPosition(cell);
            return slot;
        }
    }
}
