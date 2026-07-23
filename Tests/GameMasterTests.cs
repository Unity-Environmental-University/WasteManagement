using System.Collections.Generic;
using System.Linq;
using _project.Scripts.Core;
using _project.Scripts.Object_Scripts;
using NUnit.Framework;
using UnityEngine;

namespace _project.Scripts.Tests
{
    public class GameMasterTests
    {
        private readonly List<GameObject> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created.Where(go => go))
                Object.DestroyImmediate(go);

            _created.Clear();
        }

        [Test]
        public void Awake_AssignsChildPathBuildBoard_WhenFieldIsUnset()
        {
            var gameMasterGo = CreateGameObject("Game Master");
            var boardGo = CreateGameObject("Path Board");
            boardGo.transform.SetParent(gameMasterGo.transform);
            boardGo.AddComponent<PathBuildBoard>();

            var gameMaster = gameMasterGo.AddComponent<GameMaster>();

            Assert.IsNotNull(gameMaster.pathBuildBoard);
            Assert.AreEqual(boardGo.GetComponent<PathBuildBoard>(), gameMaster.pathBuildBoard);
        }

        [Test]
        public void PathPieceShopItem_PurchaseActivatesBoardToolAndClearsQueuedPlacementSelection()
        {
            var gameMasterGo = CreateGameObject("Game Master");
            var boardGo = CreateGameObject("Path Board");
            boardGo.transform.SetParent(gameMasterGo.transform);
            var board = boardGo.AddComponent<PathBuildBoard>();
            var gameMaster = gameMasterGo.AddComponent<GameMaster>();
            var queuedItem = new TestPlaceable();
            var pipeShopItem = new PathPieceShopItem("Short Pipe", "", 1, 2, null, 4);

            gameMaster.placementInventory.Add(queuedItem);
            pipeShopItem.Purchase();

            Assert.IsNull(gameMaster.PendingPlacement);
            Assert.AreEqual(1, gameMaster.placementInventory.Items.Count);
            Assert.AreEqual(queuedItem, gameMaster.placementInventory.Items[0]);
            Assert.IsNotNull(board.ActivePiece);
            Assert.AreEqual(PathBuildTool.Place, board.ActiveTool);
            Assert.AreEqual(2, board.ActivePiece.Length);
            Assert.AreEqual(4, board.ActivePiece.InfraValue);
        }

        [Test]
        public void PathBreakShopItem_PurchaseActivatesBoardBreakToolAndClearsQueuedPlacementSelection()
        {
            var gameMasterGo = CreateGameObject("Game Master");
            var boardGo = CreateGameObject("Path Board");
            boardGo.transform.SetParent(gameMasterGo.transform);
            var board = boardGo.AddComponent<PathBuildBoard>();
            var gameMaster = gameMasterGo.AddComponent<GameMaster>();
            var queuedItem = new TestPlaceable();
            var breakShopItem = new PathBreakShopItem("Break Pipe", "", 1, null);

            gameMaster.placementInventory.Add(queuedItem);
            breakShopItem.Purchase();

            Assert.IsNull(gameMaster.PendingPlacement);
            Assert.AreEqual(1, gameMaster.placementInventory.Items.Count);
            Assert.AreEqual(queuedItem, gameMaster.placementInventory.Items[0]);
            Assert.AreEqual(PathBuildTool.Break, board.ActiveTool);
            Assert.IsNull(board.ActivePiece);
        }

        [Test]
        public void PathBuildBoard_UpdateClearsActivePathTool_WhenUtilitySelectionIsPending()
        {
            var gameMasterGo = CreateGameObject("Game Master");
            var boardGo = CreateGameObject("Path Board");
            boardGo.transform.SetParent(gameMasterGo.transform);
            var board = boardGo.AddComponent<PathBuildBoard>();
            var gameMaster = gameMasterGo.AddComponent<GameMaster>();
            var queuedItem = new TestPlaceable();
            var pipe = new PathPiecePlaceable("Short Pipe", "", 1, 2, null, 4);

            board.SetActivePiece(pipe);
            gameMaster.placementInventory.Add(queuedItem);
            board.SendMessage("Update", SendMessageOptions.DontRequireReceiver);

            Assert.AreEqual(queuedItem, gameMaster.PendingPlacement);
            Assert.AreEqual(PathBuildTool.None, board.ActiveTool);
            Assert.IsNull(board.ActivePiece);
        }

        [Test]
        public void PlacementInventory_SelectItemByReference_SelectsQueuedItem()
        {
            var inventory = CreateGameObject("Inventory").AddComponent<PlacementInventory>();
            var first = new TestPlaceable();
            var second = new TestPlaceable();

            inventory.Add(first);
            inventory.Add(second);

            Assert.IsTrue(inventory.SelectItem(second));
            Assert.AreEqual(second, inventory.SelectedItem);
        }

        [Test]
        public void PlacementInventory_Clear_DiscardsQueuedItemsAndSelection()
        {
            var inventory = CreateGameObject("Inventory").AddComponent<PlacementInventory>();
            inventory.Add(new TestPlaceable());

            inventory.Clear();

            Assert.IsEmpty(inventory.Items);
            Assert.IsNull(inventory.SelectedItem);
            Assert.AreEqual(-1, inventory.SelectedIndex);
        }

        [Test]
        public void CesspitCap_PurchaseThenClickSealsOnlySelectedCesspit()
        {
            var gameMaster = CreateGameObject("Game Master").AddComponent<GameMaster>();
            var first = CreateGameObject("First Cesspit").AddComponent<Cesspit>();
            var second = CreateGameObject("Second Cesspit").AddComponent<Cesspit>();
            first.maxFullness = 10f;
            first.fullness = 10f;
            second.maxFullness = 20f;
            second.fullness = 7f;
            var cap = new CesspitCapShopItem("Cesspit Cap", "", 1, null);

            cap.Purchase();
            first.OnPointerClick(null);

            Assert.IsTrue(first.IsSealed);
            Assert.IsFalse(second.IsSealed);
            Assert.AreEqual(10f, first.fullness, "Sealing should not drain the cesspit.");
            Assert.AreEqual(7f, second.fullness);
            Assert.IsNull(gameMaster.PendingPlacement);
            Assert.IsNull(cap.Place(first.transform), "A cap cannot be placed on the path or an empty slot.");
            Assert.AreEqual(0, cap.InfraValue);
        }

        [Test]
        public void BuryCesspit_PurchaseThenClickConsumesPlacement_WhenCesspitIsSealed()
        {
            var gameMaster = CreateGameObject("Game Master").AddComponent<GameMaster>();
            var cesspit = CreateGameObject("Cesspit").AddComponent<Cesspit>();
            var cap = new CesspitCapShopItem("Cesspit Cap", "", 1, null);
            var burial = new BuryCesspitShopItem("Bury Cesspit", "", 1, null);

            cap.Purchase();
            cesspit.OnPointerClick(null);
            Assert.IsTrue(cesspit.IsSealed);

            burial.Purchase();
            cesspit.OnPointerClick(null);

            Assert.IsNull(gameMaster.PendingPlacement);
        }

        private GameObject CreateGameObject(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        private sealed class TestPlaceable : IPlaceable
        {
            public string DisplayName => "Test Placeable";
            public string Description => string.Empty;
            public int RequiredLevel => 1;
            public int InfraValue => 1;
            public Sprite DisplaySprite => null;
            public bool RemoveAfterPurchase => true;
            public PlaceableType PlaceableType => PlaceableType.Utility;

            public void Purchase() { }

            public GameObject Place(Transform location) => null;
        }
    }
}
