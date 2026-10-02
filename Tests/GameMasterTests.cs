using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using _project.Scripts.Core;
using _project.Scripts.Object_Scripts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using UnityEngine.TestTools;
using Pointer = UnityEngine.InputSystem.Pointer;

namespace _project.Scripts.Tests
{
    public class GameMasterTests
    {
        private readonly List<GameObject> _created = new();
        private Mouse _panTestMouse;
        private Pointer _previousPointer;

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created.Where(go => go))
                Object.DestroyImmediate(go);

            _created.Clear();
            if (_panTestMouse != null && _panTestMouse.added)
                InputSystem.RemoveDevice(_panTestMouse);
            if (_previousPointer != null && _previousPointer.added)
                _previousPointer.MakeCurrent();
            _panTestMouse = null;
            _previousPointer = null;
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
        public void PlacementInventory_ConsumeSelected_ClearsSelectionWhenAnotherItemIsQueued()
        {
            var inventory = CreateGameObject("Inventory").AddComponent<PlacementInventory>();
            var first = new TestPlaceable();
            var second = new TestPlaceable();

            inventory.Add(first);
            inventory.Add(second);

            Assert.AreEqual(first, inventory.ConsumeSelected());
            Assert.AreEqual(1, inventory.Items.Count);
            Assert.AreEqual(second, inventory.Items[0]);
            Assert.IsNull(inventory.SelectedItem);
            Assert.AreEqual(-1, inventory.SelectedIndex);
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
        public void PlacementInventory_SetActiveTool_KeepsToolArmedAcrossConsume()
        {
            var inventory = CreateGameObject("Inventory").AddComponent<PlacementInventory>();
            var tool = new TestPlaceable();

            inventory.SetActiveTool(tool);
            Assert.AreEqual(tool, inventory.SelectedItem);
            Assert.IsTrue(inventory.HasPersistentTool);

            // Infinite supply: placing (ConsumeSelected) reports the tool but leaves it armed so the
            // player can keep placing copies without reselecting.
            Assert.AreEqual(tool, inventory.ConsumeSelected());
            Assert.AreEqual(tool, inventory.SelectedItem, "Persistent tool must stay armed after placement.");
            Assert.AreEqual(tool, inventory.ConsumeSelected(), "A second placement must still be possible.");
        }

        [Test]
        public void PlacementInventory_SetActiveTool_ThenClearSelection_DisarmsAndEmptiesInventory()
        {
            var inventory = CreateGameObject("Inventory").AddComponent<PlacementInventory>();
            inventory.SetActiveTool(new TestPlaceable());

            inventory.ClearSelection();

            Assert.IsFalse(inventory.HasPersistentTool);
            Assert.IsNull(inventory.SelectedItem);
            Assert.IsEmpty(inventory.Items);
        }

        [Test]
        public void PlacementInventory_SetActiveTool_ReplacesAnyPreviousSelection()
        {
            var inventory = CreateGameObject("Inventory").AddComponent<PlacementInventory>();
            var first = new TestPlaceable();
            var second = new TestPlaceable();

            inventory.SetActiveTool(first);
            inventory.SetActiveTool(second);

            Assert.AreEqual(second, inventory.SelectedItem);
            Assert.AreEqual(1, inventory.Items.Count);
            Assert.AreEqual(second, inventory.Items[0]);
        }

        [Test]
        public void ShopManager_OpenShop_ReactivatesInactiveUiRootAndShowsPanel()
        {
            var gameMaster = CreateGameObject("Game Master").AddComponent<GameMaster>();
            var shopRoot = CreateGameObject("Shop UI");
            var shopPanel = CreateGameObject("Shop Panel");
            shopPanel.transform.SetParent(shopRoot.transform);
            shopRoot.SetActive(false);

            var shopManager = shopRoot.AddComponent<ShopManager>();
            SetPrivateField(shopManager, "shopPanel", shopPanel);
            gameMaster.shopManager = shopManager;

            shopManager.OpenShop();

            Assert.IsTrue(shopRoot.activeSelf);
            Assert.IsTrue(shopPanel.activeInHierarchy);
        }

        [Test]
        public void CameraController_RepeatedRequestsDoNotReplaceActiveShake_AndCanShakeAgainAfterStopping()
        {
            var controller = CreateGameObject("Camera Controller").AddComponent<CameraController>();
            var mainCamera = CreateGameObject("Main Camera").AddComponent<Camera>();
            var secondaryCamera = CreateGameObject("Secondary Camera").AddComponent<Camera>();
            SetPrivateField(controller, "mainCamera", mainCamera);
            SetPrivateField(controller, "secondaryCamera", secondaryCamera);

            controller.Shake(1f);
            var firstShake = GetPrivateField<object>(controller, "_shakeTween");

            controller.Shake(1f);
            Assert.AreSame(firstShake, GetPrivateField<object>(controller, "_shakeTween"));

            controller.StopShake();
            Assert.IsFalse(controller.IsShaking);

            controller.Shake(1f);
            Assert.IsTrue(controller.IsShaking);
            Assert.AreNotSame(firstShake, GetPrivateField<object>(controller, "_shakeTween"));
        }

        [Test]
        public void CameraController_PanStopsAtBoardScaledLimit_AndRecenterReturnsHome()
        {
            var gameMasterGo = CreateGameObject("Game Master");
            var boardGo = CreateGameObject("Path Board");
            boardGo.transform.SetParent(gameMasterGo.transform);
            var board = boardGo.AddComponent<PathBuildBoard>();
            gameMasterGo.AddComponent<GameMaster>();
            var controller = gameMasterGo.AddComponent<CameraController>();
            var secondaryCamera = CreateGameObject("Secondary Camera").AddComponent<Camera>();
            var home = new Vector3(1f, 20f, -3f);
            secondaryCamera.transform.position = home;
            SetPrivateField(controller, "secondaryCamera", secondaryCamera);
            SetPrivateField(controller, "panRange", 0.5f);
            SetPrivateField(controller, "recenterDuration", 0f);

            var pitch = board.CellWorldPitch;
            var limit = new Vector2(board.Columns * pitch.x, board.Rows * pitch.y) * 0.25f;

            Assert.IsFalse(controller.IsPanned);

            controller.PanBy(new Vector2(1000f, -1000f));

            var offset = secondaryCamera.transform.position - home;
            Assert.IsTrue(controller.IsPanned);
            Assert.AreEqual(limit.x, offset.x, 0.001f);
            Assert.AreEqual(0f, offset.y, 0.001f);
            Assert.AreEqual(-limit.y, offset.z, 0.001f);

            controller.Recenter();

            Assert.IsFalse(controller.IsPanned);
            Assert.AreEqual(home, secondaryCamera.transform.position);
        }

        [Test]
        public void CameraController_PanInput_MovesOnlyDuringAnArmedPress()
        {
            var fixture = CreatePanInputFixture();
            var home = fixture.Camera.transform.position;
            SendPanMouse(new Vector2(400, 300), true);
            SendPanMouse(new Vector2(450, 300), true);
            Assert.AreEqual(home, fixture.Camera.transform.position);
            SendPanMouse(new Vector2(400, 300), false);

            fixture.Board.SetActivePanTool();
            SendPanMouse(new Vector2(400, 300), true);
            SendPanMouse(new Vector2(450, 300), true);
            Assert.IsTrue(fixture.Controller.IsPanned);
            SendPanMouse(new Vector2(450, 300), false);
            var releasedPosition = fixture.Camera.transform.position;
            SendPanMouse(new Vector2(500, 300), false);
            Assert.AreEqual(releasedPosition, fixture.Camera.transform.position);
        }

        [TestCase("Disable")]
        [TestCase("Recenter")]
        [TestCase("MainCamera")]
        [TestCase("ToolChange")]
        [TestCase("DeviceRemoved")]
        [TestCase("FocusLost")]
        public void CameraController_PanInput_CleansUpInterruptedGestures(string interruption)
        {
            var fixture = CreatePanInputFixture();
            fixture.Board.SetActivePanTool();
            SendPanMouse(new Vector2(400, 300), true);
            SendPanMouse(new Vector2(450, 300), true);
            Assert.IsTrue(fixture.Controller.IsPanned);

            switch (interruption)
            {
                case "Disable": fixture.Controller.enabled = false; break;
                case "Recenter": fixture.Controller.Recenter(); break;
                case "MainCamera": fixture.Controller.SwitchTo(CameraView.Main); break;
                case "ToolChange": fixture.Board.ClearActivePiece(); break;
                case "DeviceRemoved": InputSystem.RemoveDevice(_panTestMouse); break;
                case "FocusLost": fixture.Controller.SendMessage("OnApplicationFocus", false); break;
            }

            InputSystem.Update();
            var stoppedPosition = fixture.Camera.transform.position;
            if (_panTestMouse.added) SendPanMouse(new Vector2(500, 300), true);
            Assert.AreEqual(stoppedPosition, fixture.Camera.transform.position);
            Assert.IsFalse(GetPrivateField<bool>(fixture.Controller, "_isPanning"));
        }

        [Test]
        public void CameraController_PanInput_ReenableAllowsAFreshGesture()
        {
            var fixture = CreatePanInputFixture();
            fixture.Board.SetActivePanTool();
            SendPanMouse(new Vector2(400, 300), true);
            fixture.Controller.enabled = false;
            fixture.Controller.enabled = true;
            SendPanMouse(new Vector2(400, 300), false);
            SendPanMouse(new Vector2(400, 300), true);
            SendPanMouse(new Vector2(450, 300), true);
            Assert.IsTrue(fixture.Controller.IsPanned);
        }

        [Test]
        public void CameraController_PanInput_KeepsThePointerThatBeganTheGesture()
        {
            var fixture = CreatePanInputFixture();
            fixture.Board.SetActivePanTool();
            SendPanMouse(new Vector2(400, 300), true);
            SendPanMouse(new Vector2(450, 300), true);
            var draggedPosition = fixture.Camera.transform.position;
            var otherMouse = InputSystem.AddDevice<Mouse>();
            try
            {
                InputSystem.QueueStateEvent(otherMouse, new MouseState { position = new Vector2(200, 200) });
                InputSystem.Update();
                Assert.Less((draggedPosition - fixture.Camera.transform.position).sqrMagnitude, 0.0001f);
                SendPanMouse(new Vector2(500, 300), true);
                Assert.Greater((draggedPosition - fixture.Camera.transform.position).sqrMagnitude, 0.0001f,
                    "Another pointer becoming current must not interrupt the original drag.");
            }
            finally
            {
                InputSystem.RemoveDevice(otherMouse);
            }
        }

        [UnityTest]
        public IEnumerator CameraController_PanInput_IgnoresInteractiveUiPresses()
        {
            var fixture = CreatePanInputFixture();
            fixture.Board.SetActivePanTool();
            CreateGameObject("Event System").AddComponent<EventSystem>();
            var canvasHost = new GameObject("Input Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(GraphicRaycaster));
            _created.Add(canvasHost);
            canvasHost.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var buttonHost = new GameObject("Input Button", typeof(RectTransform), typeof(Image), typeof(Button));
            _created.Add(buttonHost);
            var buttonRect = (RectTransform)buttonHost.transform;
            buttonRect.SetParent(canvasHost.transform, false);
            buttonRect.anchorMin = Vector2.zero;
            buttonRect.anchorMax = Vector2.one;
            buttonRect.offsetMin = buttonRect.offsetMax = Vector2.zero;
            yield return null;
            Canvas.ForceUpdateCanvases();
            var home = fixture.Camera.transform.position;
            var uiPosition = RectTransformUtility.WorldToScreenPoint(null, buttonRect.position);

            SendPanMouse(uiPosition, true);
            Assert.IsTrue(PointerUi.IsPointerOverInteractiveUi(),
                $"The test press must hit the button at {uiPosition}, rect {buttonRect.rect}.");
            SendPanMouse(uiPosition + new Vector2(10, 0), true);

            Assert.AreEqual(home, fixture.Camera.transform.position);
            Assert.IsFalse(GetPrivateField<bool>(fixture.Controller, "_isPanning"));
        }

        [Test]
        public void ShopManager_SelectPanTool_ArmsPanOnlyWhileThePlanningCameraIsActive()
        {
            var gameMasterGo = CreateGameObject("Game Master");
            var boardGo = CreateGameObject("Path Board");
            boardGo.transform.SetParent(gameMasterGo.transform);
            var board = boardGo.AddComponent<PathBuildBoard>();
            var gameMaster = gameMasterGo.AddComponent<GameMaster>();
            var controller = gameMasterGo.AddComponent<CameraController>();
            var mainCamera = CreateGameObject("Main Camera").AddComponent<Camera>();
            var secondaryCamera = CreateGameObject("Secondary Camera").AddComponent<Camera>();
            SetPrivateField(controller, "mainCamera", mainCamera);
            SetPrivateField(controller, "secondaryCamera", secondaryCamera);
            var shopManager = CreateGameObject("Shop UI").AddComponent<ShopManager>();

            gameMaster.placementInventory.Add(new TestPlaceable());
            board.SetActivePiece(new PathPiecePlaceable("Short Pipe", "", 1, 2, null, 4));
            Assert.IsFalse(controller.IsPanArmed);

            shopManager.SelectPanTool();

            Assert.IsNull(gameMaster.PendingPlacement);
            Assert.AreEqual(PathBuildTool.Pan, board.ActiveTool);
            Assert.IsNull(board.ActivePiece);
            Assert.IsTrue(controller.IsPanArmed);

            controller.SwitchTo(CameraView.Main);
            Assert.IsFalse(controller.IsPanArmed);
        }

        [Test]
        public void PathBuildBoard_RotateActivePiece_FlipsOnlyAnArmedPiece()
        {
            var board = CreateGameObject("Path Board").AddComponent<PathBuildBoard>();
            var pipe = new PathPiecePlaceable("Short Pipe", "", 1, 2, null, 4);
            var startOrientation = pipe.Orientation;

            Assert.IsFalse(board.CanRotateActivePiece);

            board.SetActivePiece(pipe);
            Assert.IsTrue(board.CanRotateActivePiece);

            board.RotateActivePiece();
            Assert.AreNotEqual(startOrientation, pipe.Orientation);

            board.RotateActivePiece();
            Assert.AreEqual(startOrientation, pipe.Orientation);

            board.SetActiveBreakTool();
            Assert.IsFalse(board.CanRotateActivePiece);
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

        private (CameraController Controller, PathBuildBoard Board, Camera Camera) CreatePanInputFixture()
        {
            var gmHost = CreateGameObject("Game Master");
            var boardHost = CreateGameObject("Path Board");
            boardHost.transform.SetParent(gmHost.transform);
            var board = boardHost.AddComponent<PathBuildBoard>();
            var gm = gmHost.AddComponent<GameMaster>();
            gm.turnController.enabled = false;
            var controller = gmHost.AddComponent<CameraController>();
            var mainCamera = CreateGameObject("Main Camera").AddComponent<Camera>();
            var camera = CreateGameObject("Planning Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 10, 0);
            camera.transform.rotation = Quaternion.Euler(90, 0, 0);
            camera.pixelRect = new Rect(0, 0, 800, 600);
            SetPrivateField(controller, "mainCamera", mainCamera);
            SetPrivateField(controller, "secondaryCamera", camera);
            SetPrivateField(controller, "recenterDuration", 0f);
            _previousPointer = Pointer.current;
            _panTestMouse = InputSystem.AddDevice<Mouse>();
            return (controller, board, camera);
        }

        private void SendPanMouse(Vector2 position, bool pressed)
        {
            InputSystem.QueueStateEvent(_panTestMouse,
                new MouseState { position = position }.WithButton(MouseButton.Left, pressed));
            InputSystem.Update();
        }

        private GameObject CreateGameObject(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        private static void SetPrivateField<T>(T target, string fieldName, object value)
        {
            var field = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Expected private field '{fieldName}' on {typeof(T).Name}.");
            field.SetValue(target, value);
        }

        private static TValue GetPrivateField<TValue>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Expected private field '{fieldName}' on {target.GetType().Name}.");
            return (TValue)field.GetValue(target);
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
