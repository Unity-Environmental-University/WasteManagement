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
        public void PathPieceShopItem_PurchaseActivatesBoardToolWithoutChangingQueuedPlacement()
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

            Assert.AreEqual(queuedItem, gameMaster.PendingPlacement);
            Assert.AreEqual(1, gameMaster.placementInventory.Items.Count);
            Assert.IsNotNull(board.ActivePiece);
            Assert.AreEqual(2, board.ActivePiece.Length);
            Assert.AreEqual(4, board.ActivePiece.InfraValue);
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
