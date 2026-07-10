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

        [Test]
        public void TryGetPathFacingRotation_ReturnsFalse_WhenPlacedOutsideBoardNearOccupiedEdge()
        {
            var board = CreateGameObject("Path Board").AddComponent<PathBuildBoard>();
            PlaceVertical(board, 1, 0, 2);

            Assert.IsFalse(board.TryGetPathFacingRotation(board.GetCellTopPosition(new Vector2Int(1, -1)),
                out _));
        }

        [Test]
        public void IssueVisualOverride_PersistsAfterProcessingChangesSize()
        {
            var issue = CreatePrimitive("Runaway Issue").AddComponent<IssueObject>();
            var runawayColor = new Color(1f, 0.45f, 0f);

            issue.SetSize(3);
            issue.SetVisualOverride(runawayColor);
            issue.Process(1, "Test Process");

            Assert.AreEqual(runawayColor, issue.GetComponent<Renderer>().material.color);
        }

        [UnityTest]
        public IEnumerator IssueObject_OnPathCollision_MergesIntoNextSizeStage()
        {
            var fixture = CreatePathFixture();
            var issueA = CreatePrimitive("Issue A").AddComponent<IssueObject>();
            var issueB = CreatePrimitive("Issue B").AddComponent<IssueObject>();

            issueA.SetPath(fixture.Path);
            issueB.SetPath(fixture.Path);
            issueA.SetSize(1);
            issueB.SetSize(1);

            issueA.SendMessage("OnTriggerEnter", issueB.GetComponent<Collider>());
            yield return null;

            Assert.IsTrue(issueA);
            Assert.IsFalse(issueB);
            Assert.AreEqual(2f, issueA.ProcessCost);
            Assert.AreEqual(Vector3.one * 2f, issueA.transform.localScale);
        }

        [UnityTest]
        public IEnumerator IssueObject_BlockingMerge_PreservesSurvivorPathPosition()
        {
            var fixture = CreatePathFixture();
            var issueA = CreatePrimitive("Blocking Issue A").AddComponent<IssueObject>();
            var issueB = CreatePrimitive("Blocking Issue B").AddComponent<IssueObject>();
            var survivorPosition = new Vector3(2f, 0f, 3f);

            issueA.SetPath(fixture.Path);
            issueB.SetPath(fixture.Path);
            issueA.SetSize(3);
            issueB.SetSize(3);
            issueA.transform.position = survivorPosition;
            issueB.transform.position = survivorPosition + new Vector3(0.25f, 0f, 0.25f);

            issueA.SendMessage("OnTriggerEnter", issueB.GetComponent<Collider>());
            yield return null;

            Assert.IsTrue(issueA.IsBlockingPipe);
            Assert.AreEqual(survivorPosition.x, issueA.transform.position.x, 0.0001f);
            Assert.AreEqual(survivorPosition.z, issueA.transform.position.z, 0.0001f);
        }

        [UnityTest]
        public IEnumerator IssueObject_DirectDestinationCollision_DoesNotMerge()
        {
            var fixture = CreatePathFixture();
            PlaceVertical(fixture.Board, 1, 0, 10);
            Assert.IsTrue(fixture.Path.Rebuild());

            var pathIssue = CreatePrimitive("Path Issue").AddComponent<IssueObject>();
            var runawayIssue = CreatePrimitive("Runaway Issue").AddComponent<IssueObject>();

            pathIssue.SetPath(fixture.Path);
            runawayIssue.SetDirectDestination(Vector3.forward);
            pathIssue.SetSize(1);
            runawayIssue.SetSize(1);

            pathIssue.SendMessage("OnTriggerEnter", runawayIssue.GetComponent<Collider>());
            yield return null;

            Assert.IsTrue(pathIssue);
            Assert.IsTrue(runawayIssue);
            Assert.AreEqual(1f, pathIssue.ProcessCost);
            Assert.AreEqual(1f, runawayIssue.ProcessCost);
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
        public void SifterPlace_ReturnsNull_WhenSlotIsOutsideBoardNearOccupiedEdge()
        {
            var gm = CreateGameObject("Game Master").AddComponent<GameMaster>();
            var board = CreateGameObject("Path Board").AddComponent<PathBuildBoard>();
            var prefab = CreatePrimitive("Sifter Prefab");
            var slot = CreateGameObject("Outside Slot").transform;
            var item = new SifterShopItem("Sifter", "", 1, prefab, null, 1);

            gm.pathBuildBoard = board;
            PlaceVertical(board, 1, 0, 2);
            slot.position = board.GetCellTopPosition(new Vector2Int(1, -1));

            Assert.IsNull(item.Place(slot));
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
        public IEnumerator WaveTimer_WaitsForActiveIssuesBeforeApplyingPostWaveGrowth()
        {
            var fixture = CreateTurnFixture(true);
            fixture.TurnController.waveDuration = 0f;

            fixture.TurnController.EndPhase();
            var issue = CreatePrimitive("Late Issue").AddComponent<IssueObject>();
            issue.SetPath(fixture.Path);
            issue.SetMoveSpeed(0f);

            yield return null;

            Assert.AreEqual(GamePhase.Tower, fixture.TurnController.currentPhase);
            fixture.GameMaster.popManager.RecordLakePollution(3f);

            Object.Destroy(issue.gameObject);
            yield return null;
            yield return null;

            Assert.AreEqual(GamePhase.Card, fixture.TurnController.currentPhase);
            Assert.AreEqual(4, fixture.GameMaster.popManager.GetPopulationSize());
            Assert.AreEqual(0f, fixture.GameMaster.popManager.GetWavePollution());
        }

        [UnityTest]
        public IEnumerator WaveTimer_RecoversLakeBeforeNextCardPhase()
        {
            var fixture = CreateTurnFixture(true);
            fixture.TurnController.waveDuration = 0f;
            var lake = CreatePrimitive("Lake").AddComponent<LakeController>();
            lake.health = 90f;

            fixture.TurnController.EndPhase();
            yield return null;
            yield return null;

            Assert.AreEqual(GamePhase.Card, fixture.TurnController.currentPhase);
            Assert.AreEqual(92f, lake.health, 0.0001f);
        }

        [Test]
        public void LakeController_RecoverForTurn_CapsAtFullHealth()
        {
            var lake = CreatePrimitive("Lake").AddComponent<LakeController>();
            lake.health = 99f;

            lake.RecoverForTurn();

            Assert.AreEqual(100f, lake.health, 0.0001f);
        }

        [UnityTest]
        public IEnumerator WaveTimer_StopsCesspitRunawaysWhenSpawnersStop()
        {
            var fixture = CreateTurnFixture(true);
            fixture.TurnController.waveDuration = 0f;
            var cesspit = CreateGameObject("Cesspit").AddComponent<Cesspit>();
            cesspit.maxFullness = 1f;
            cesspit.fullness = 1f;

            yield return null;
            Assert.IsNotNull(GetField<Coroutine>(cesspit, "_runawayCoroutine"));

            fixture.TurnController.EndPhase();
            yield return null;
            yield return null;

            Assert.AreEqual(GamePhase.Card, fixture.TurnController.currentPhase);
            Assert.IsNull(GetField<Coroutine>(cesspit, "_runawayCoroutine"));
        }

        [UnityTest]
        public IEnumerator Cesspit_UsesIssuePathDestinationForRunaways()
        {
            var fixture = CreateTurnFixture(true);
            var cesspit = CreateGameObject("Cesspit").AddComponent<Cesspit>();
            var oldDestination = CreateGameObject("Old Runaway Destination").transform;
            SetField(cesspit, "runawayDestination", oldDestination);

            yield return null;

            Assert.AreSame(fixture.Path.Destination, GetField<Transform>(cesspit, "runawayDestination"));
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
            board.SetActivePiece(piece);

            targetCell.SendMessage("OnMouseDown");

            Assert.AreEqual(3, gm.turnController.infrastructureValue);
            Assert.AreEqual(1, gm.turnController.moveCount);
            Assert.IsNull(gm.PendingPlacement);
            Assert.AreEqual(piece, board.ActivePiece);
        }

        [Test]
        public void OnMouseDown_BreakToolRemovesPathPieceAndCountsMove()
        {
            var board = CreateGameObject("Path Board").AddComponent<PathBuildBoard>();
            var gm = CreateGameObject("Game Master").AddComponent<GameMaster>();
            var piece = new PathPiecePlaceable("Pipe", "", 1, 2, null, 3);
            var targetCell = GetCell(board, 1, 1);

            gm.turnController.currentPhase = GamePhase.Card;
            board.SetActivePiece(piece);
            targetCell.SendMessage("OnMouseDown");

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
            var popManager = gm.gameObject.AddComponent<PopulationManager>();
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
            gm.popManager = popManager;
            gm.interfaceManager = interfaceManager;
            gm.mainCamera = mainCamera;
            gm.topDownCamera = topDownCamera;
            gm.pathBuildBoard = pathFixture.Board;
            gm.entitySpawners = new List<EntitySpawner> { spawner };

            SetField(turnController, "_gm", gm);
            turnController.currentPhase = GamePhase.Card;
            turnController.waveDuration = 1000f;

            return new TurnFixture(gm, turnController, spawner, pathFixture.Path);
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

        private GameObject CreatePrimitive(string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
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
            public TurnFixture(GameMaster gameMaster, TurnController turnController, EntitySpawner spawner,
                WaypointPath path)
            {
                GameMaster = gameMaster;
                TurnController = turnController;
                Spawner = spawner;
                Path = path;
            }

            public GameMaster GameMaster { get; }
            public TurnController TurnController { get; }
            public EntitySpawner Spawner { get; }
            public WaypointPath Path { get; }
        }
    }
}
