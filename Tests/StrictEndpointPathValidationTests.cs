using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using _project.Scripts.Core;
using _project.Scripts.Object_Scripts;
using _project.Scripts.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace _project.Scripts.Tests
{
    public class StrictEndpointPathValidationTests
    {
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
    public void Rebuild_ReturnsTrue_WhenPathTouchesLowerAndUpperEndpoints()
    {
        var fixture = CreatePathFixture();
        PlaceVertical(fixture.Board, 1, 0, 10);

            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.IsTrue(fixture.Path.IsValid);
        Assert.AreEqual(12, fixture.Path.Count);
    }

    [Test]
    public void Rebuild_ReturnsTrue_WhenEndpointMarkerIsFarOutsideBoardButAlignedWithEdgeCell()
    {
        var fixture = CreatePathFixture(upperRow: 12);
        PlaceVertical(fixture.Board, 1, 0, 10);

        Assert.IsTrue(fixture.Path.Rebuild());
        Assert.IsTrue(fixture.Path.IsValid);
    }

        [Test]
        public void Rebuild_ReturnsFalse_WhenPathIsNearLowerEndpointButNotEdgeAdjacent()
        {
            var fixture = CreatePathFixture(0);
            PlaceVertical(fixture.Board, 1, 0, 10);

            Assert.IsFalse(fixture.Path.Rebuild());
            Assert.AreEqual("No placed path cell touches the lower endpoint square.", fixture.Path.InvalidReason);
        }

        [Test]
        public void Rebuild_ReturnsFalse_WhenPathIsNearUpperEndpointButNotEdgeAdjacent()
        {
            var fixture = CreatePathFixture(1, 0);
            PlaceVertical(fixture.Board, 1, 0, 10);

            Assert.IsFalse(fixture.Path.Rebuild());
            Assert.AreEqual("No placed path cell touches the upper endpoint square.", fixture.Path.InvalidReason);
        }

        [Test]
        public void Rebuild_ReturnsFalse_WhenOnlyLowerEndpointIsConnected()
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 2);

            Assert.IsFalse(fixture.Path.Rebuild());
            Assert.AreEqual("No placed path cell touches the upper endpoint square.", fixture.Path.InvalidReason);
        }

        [Test]
        public void Rebuild_ReturnsFalse_WhenOnlyUpperEndpointIsConnected()
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 8, 2);

            Assert.IsFalse(fixture.Path.Rebuild());
            Assert.AreEqual("No placed path cell touches the lower endpoint square.", fixture.Path.InvalidReason);
        }

        [Test]
        public void Rebuild_ReturnsFalse_WhenEndpointCandidatesAreDisconnected()
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 2);
            PlaceVertical(fixture.Board, 1, 8, 2);

            Assert.IsFalse(fixture.Path.Rebuild());
            Assert.AreEqual("Placed path does not connect lower endpoint to upper endpoint.",
                fixture.Path.InvalidReason);
        }

        [Test]
        public void Rebuild_ReturnsTrue_WhenConnectedPathHasSideTouchingBranch()
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 10);
            PlaceHorizontal(fixture.Board, 2, 5, 2);

            Assert.IsTrue(fixture.Path.Rebuild());
            Assert.IsTrue(fixture.Path.IsValid);
            Assert.AreEqual(12, fixture.Path.Count);
        }

        [Test]
        public void Rebuild_LeavesCountZero_WhenValidationFails()
        {
            var fixture = CreatePathFixture(0);
            PlaceVertical(fixture.Board, 1, 0, 10);

            fixture.Path.Rebuild();

            Assert.AreEqual(0, fixture.Path.Count);
        }

        [Test]
        public void Rebuild_ReturnsFalse_WhenValidationFails()
        {
            var fixture = CreatePathFixture(0);
            PlaceVertical(fixture.Board, 1, 0, 10);

            Assert.IsFalse(fixture.Path.Rebuild());
        }

        [Test]
        public void Rebuild_DoesNotEmitDirectStartToEndFallback_WhenValidationFails()
        {
            var fixture = CreatePathFixture(0);
            PlaceVertical(fixture.Board, 1, 0, 10);

            fixture.Path.Rebuild();

            Assert.AreEqual(0, fixture.Path.Count);
        }

        [Test]
        public void TryGetPathFacingRotation_FacesAlongHorizontalPipe_WhenPlacedOnPipe()
        {
            var board = CreateGameObject("Path Board").AddComponent<PathBuildBoard>();
            PlaceHorizontal(board, 1, 1, 3);

            Assert.IsTrue(board.TryGetPathFacingRotation(board.GetCellTopPosition(new Vector2Int(1, 1)),
                out var rotation));

            AssertFaces(rotation, Vector3.right);
        }

        [Test]
        public void TryGetPathFacingRotation_FacesAlongVerticalPipe_WhenPlacedOnPipe()
        {
            var board = CreateGameObject("Path Board").AddComponent<PathBuildBoard>();
            PlaceVertical(board, 1, 1, 3);

            Assert.IsTrue(board.TryGetPathFacingRotation(board.GetCellTopPosition(new Vector2Int(1, 1)),
                out var rotation));

            AssertFaces(rotation, Vector3.forward);
        }

        [Test]
        public void TryGetPathFacingRotation_ReturnsFalse_WhenPlacedOffPipe()
        {
            var board = CreateGameObject("Path Board").AddComponent<PathBuildBoard>();
            PlaceVertical(board, 1, 1, 3);

            Assert.IsFalse(board.TryGetPathFacingRotation(board.GetCellTopPosition(new Vector2Int(2, 1)),
                out _));
        }

        [UnityTest]
        public IEnumerator EndPhase_InvalidPath_KeepsCardPhase()
        {
            var fixture = CreateTurnFixture(false);
            LogAssert.Expect(LogType.Warning,
                "Cannot begin wave: No placed path cell touches the lower endpoint square.");

            fixture.TurnController.EndPhase();
            yield return null;

            Assert.AreEqual(GamePhase.Card, fixture.TurnController.currentPhase);
        }

        [UnityTest]
        public IEnumerator EndPhase_InvalidPath_DoesNotStartSpawnerCoroutine()
        {
            var fixture = CreateTurnFixture(false);
            LogAssert.Expect(LogType.Warning,
                "Cannot begin wave: No placed path cell touches the lower endpoint square.");

            fixture.TurnController.EndPhase();
            yield return null;

            Assert.IsNull(GetField<Coroutine>(fixture.Spawner, "_spawnCoroutine"));
        }

        [UnityTest]
        public IEnumerator EndPhase_ValidPath_TransitionsToTowerPhase()
        {
            var fixture = CreateTurnFixture(true);

            fixture.TurnController.EndPhase();
            yield return null;

            Assert.AreEqual(GamePhase.Tower, fixture.TurnController.currentPhase);
        }

        [UnityTest]
        public IEnumerator EndPhase_TowerToCard_DoesNotThrowWhenInfoBarTextIsUnset()
        {
            var fixture = CreateTurnFixture(true);
            fixture.TurnController.currentPhase = GamePhase.Tower;

            fixture.TurnController.EndPhase();
            yield return null;

            Assert.AreEqual(GamePhase.Card, fixture.TurnController.currentPhase);
        }

        [Test]
        public void OnMouseDown_PathPlacementAddsInfrastructureValue()
        {
            var board = CreateGameObject("Path Board").AddComponent<PathBuildBoard>();
            var gm = CreateGameObject("Game Master").AddComponent<GameMaster>();
            var piece = new PathPiecePlaceable("Pipe", "", 1, 2, null, 3);
            var targetCell = GetCell(board, 1, 1);

            gm.turnController.currentPhase = GamePhase.Card;
            gm.placementInventory.Add(piece);

            targetCell.SendMessage("OnMouseDown");

            Assert.AreEqual(3, gm.turnController.infrastructureValue);
            Assert.AreEqual(1, gm.turnController.moveCount);
            Assert.IsNull(gm.PendingPlacement);
        }

    private PathFixture CreatePathFixture(int lowerColumn = 1, int upperColumn = 1, int lowerRow = -1, int upperRow = 10)
        {
            var board = CreateGameObject("Path Board").AddComponent<PathBuildBoard>();
            var path = CreateGameObject("Waypoint Path").AddComponent<WaypointPath>();
            var lower = CreateGameObject("Lower Endpoint").transform;
            var upper = CreateGameObject("Upper Endpoint").transform;

        lower.position = board.GetPathWaypointPosition(new Vector2Int(lowerColumn, lowerRow));
        upper.position = board.GetPathWaypointPosition(new Vector2Int(upperColumn, upperRow));

            SetField(path, "pathBuildBoard", board);
            SetField(path, "startPoint", lower);
            SetField(path, "endPoint", upper);

            return new PathFixture(board, path, lower, upper);
        }

        private TurnFixture CreateTurnFixture(bool validPath)
        {
            var pathFixture = validPath ? CreatePathFixture() : CreatePathFixture(0);
            PlaceVertical(pathFixture.Board, 1, 0, 10);

            var gm = CreateGameObject("Game Master").AddComponent<GameMaster>();
            var turnController = gm.GetComponent<TurnController>();
            turnController.enabled = false;
            var placementInventory = gm.GetComponent<PlacementInventory>();
            var interfaceManager = CreateInterfaceManager();
            var mainCamera = CreateGameObject("Main Camera").AddComponent<Camera>();
            var topDownCamera = CreateGameObject("Top Camera").AddComponent<Camera>();
            var spawner = CreateGameObject("Spawner").AddComponent<EntitySpawner>();

            mainCamera.gameObject.SetActive(true);
            topDownCamera.gameObject.SetActive(false);

            SetField(spawner, "path", pathFixture.Path);
            SetField(spawner, "spawnPoint", CreateGameObject("Spawn Point").transform);
            spawner.spawnInterval = 1000f;

            gm.turnController = turnController;
            gm.placementInventory = placementInventory;
            gm.interfaceManager = interfaceManager;
            gm.mainCamera = mainCamera;
            gm.topDownCamera = topDownCamera;
            gm.entitySpawners = new List<EntitySpawner> { spawner };

            SetField(turnController, "_gm", gm);
            turnController.currentPhase = GamePhase.Card;
            turnController.waveDuration = 1000f;

            return new TurnFixture(turnController, spawner);
        }

        private InterfaceManager CreateInterfaceManager()
        {
            var manager = CreateGameObject("Interface Manager").AddComponent<InterfaceManager>();
            SetField(manager, "quitButton", CreateUiObject<Button>("Quit Button"));
            SetField(manager, "nextButton", CreateUiObject<Button>("Next Button"));
            SetField(manager, "openShopButton", CreateUiObject<Button>("Open Shop Button"));
            SetField(manager, "closeShopButton", CreateUiObject<Button>("Close Shop Button"));
            SetField(manager, "mTowerUpgrades", CreateUiObject<Image>("Middle Tower Upgrades"));
            SetField(manager, "rTowerUpgrades", CreateUiObject<Image>("Right Tower Upgrades"));
            SetField(manager, "lTowerUpgrades", CreateUiObject<Image>("Left Tower Upgrades"));
            SetField(manager, "handContainer", CreateGameObject("Hand Container").transform);
            return manager;
        }

        private T CreateUiObject<T>(string name) where T : Component
        {
            return CreateGameObject(name).AddComponent<T>();
        }

        private void PlaceVertical(PathBuildBoard board, int column, int row, int length)
        {
            var piece = new PathPiecePlaceable("Pipe", "", 1, length, null,0);
            piece.ToggleOrientation();
            Assert.IsTrue(GetCell(board, column, row).TryPlace(piece));
        }

        private void PlaceHorizontal(PathBuildBoard board, int column, int row, int length)
        {
            var piece = new PathPiecePlaceable("Pipe", "", 1, length, null, 0);
            Assert.IsTrue(GetCell(board, column, row).TryPlace(piece));
        }

        private static void AssertFaces(Quaternion rotation, Vector3 expectedDirection)
        {
            Assert.Greater(Vector3.Dot(rotation * Vector3.forward, expectedDirection.normalized), 0.99f);
        }

        private PathBuildCell GetCell(PathBuildBoard board, int column, int row)
        {
            var child = board.transform.Find($"Path Cell {column},{row}");
            Assert.IsTrue(child, $"Missing generated cell {column},{row}");
            return child.GetComponent<PathBuildCell>();
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

        private static T GetField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field {fieldName} on {target.GetType().Name}");
            return (T)field.GetValue(target);
        }

        private readonly struct PathFixture
        {
            public PathFixture(PathBuildBoard board, WaypointPath path, Transform lower, Transform upper)
            {
                Board = board;
                Path = path;
                Lower = lower;
                Upper = upper;
            }

            public PathBuildBoard Board { get; }
            public WaypointPath Path { get; }
            public Transform Lower { get; }
            public Transform Upper { get; }
        }

        private readonly struct TurnFixture
        {
            public TurnFixture(TurnController turnController, EntitySpawner spawner)
            {
                TurnController = turnController;
                Spawner = spawner;
            }

            public TurnController TurnController { get; }
            public EntitySpawner Spawner { get; }
        }
    }
}
