using System.Collections;
using System.Collections.Generic;
using _project.Scripts.Core;
using _project.Scripts.Object_Scripts;
using _project.Scripts.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace _project.Scripts.Tests
{
    /// <summary>Phase transitions, the wave timer, and what they start and stop along the way.</summary>
    public class TurnFlowTests : PathTestBase
    {
        [UnityTest]
        public IEnumerator EndPhase_InvalidPath_StaysInCardPhaseWithoutSpawning()
        {
            var fixture = CreateTurnFixture(false);
            LogAssert.Expect(LogType.Warning,
                "Cannot begin wave: No placed path cell touches the lower endpoint square.");

            fixture.TurnController.EndPhase();
            yield return null;

            Assert.AreEqual(GamePhase.Card, fixture.TurnController.currentPhase);
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
        public IEnumerator EndPhase_TowerToCard_ToleratesUnassignedSectionLineText()
        {
            var fixture = CreateTurnFixture(true);
            fixture.TurnController.currentPhase = GamePhase.Tower;

            fixture.TurnController.EndPhase();
            yield return null;

            Assert.AreEqual(GamePhase.Card, fixture.TurnController.currentPhase);
        }

        [UnityTest]
        public IEnumerator WaveTimer_WaitsForActiveIssuesBeforeApplyingPostWaveGrowth()
        {
            var fixture = CreateTurnFixture(true);
            fixture.TurnController.waveDuration = 0f;

            fixture.TurnController.EndPhase();
            var issue = CreateIssue("Late Issue", fixture.Path);
            issue.SetMoveSpeed(0f);

            yield return null;

            Assert.AreEqual(GamePhase.Tower, fixture.TurnController.currentPhase);
            fixture.GameMaster.popManager.RecordLakePollution(3f);

            Object.Destroy(issue.gameObject);
            yield return null;
            yield return null;

            Assert.AreEqual(GamePhase.Card, fixture.TurnController.currentPhase);
            Assert.AreEqual(10, fixture.GameMaster.popManager.GetPopulationSize());
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
            fixture.TurnController.currentLevel = 3;
            var cesspit = CreateFullCesspit();

            yield return null;
            Assert.IsNotNull(GetField<Coroutine>(cesspit, "_runawayCoroutine"));

            fixture.TurnController.EndPhase();
            yield return null;
            yield return null;

            Assert.AreEqual(GamePhase.Card, fixture.TurnController.currentPhase);
            Assert.IsNull(GetField<Coroutine>(cesspit, "_runawayCoroutine"));
        }

        [UnityTest]
        public IEnumerator EndPhase_TowerToSummary_StopsCesspitRunawaysImmediately()
        {
            var fixture = CreateTurnFixture(true);
            fixture.TurnController.currentLevel = 3;
            var cesspit = CreateFullCesspit();

            yield return null;
            fixture.TurnController.EndPhase();

            Assert.IsNotNull(GetField<Coroutine>(cesspit, "_runawayCoroutine"));
            Assert.IsNotNull(GetField<Coroutine>(fixture.Spawner, "_spawnCoroutine"));

            fixture.TurnController.EndPhase();

            Assert.IsNull(GetField<Coroutine>(cesspit, "_runawayCoroutine"));
            Assert.IsNull(GetField<Coroutine>(fixture.Spawner, "_spawnCoroutine"));
        }

        [UnityTest]
        public IEnumerator Cesspit_UsesIssuePathDestinationForRunaways()
        {
            var fixture = CreateTurnFixture(true);
            var cesspit = CreateGameObject("Cesspit").AddComponent<Cesspit>();
            SetField(cesspit, "runawayDestination", CreateGameObject("Old Runaway Destination").transform);

            yield return null;

            Assert.AreSame(fixture.Path.Destination, GetField<Transform>(cesspit, "runawayDestination"));
        }

        private Cesspit CreateFullCesspit()
        {
            var cesspit = CreateGameObject("Cesspit").AddComponent<Cesspit>();
            cesspit.maxFullness = 1f;
            cesspit.fullness = 1f;
            return cesspit;
        }

        private TurnFixture CreateTurnFixture(bool validPath)
        {
            var pathFixture = validPath ? CreatePathFixture() : CreatePathFixture(0);
            PlaceVertical(pathFixture.Board, 1, 0, 10);

            var gm = CreateGameObject("Game Master").AddComponent<GameMaster>();
            var turnController = gm.GetComponent<TurnController>();
            turnController.enabled = false;
            var mainCamera = CreateGameObject("Main Camera").AddComponent<Camera>();
            var topDownCamera = CreateGameObject("Top Camera").AddComponent<Camera>();
            topDownCamera.gameObject.SetActive(false);

            var spawner = CreateGameObject("Spawner").AddComponent<EntitySpawner>();
            SetField(spawner, "path", pathFixture.Path);
            SetField(spawner, "spawnPoint", CreateGameObject("Spawn Point").transform);
            spawner.spawnInterval = 1000f;

            gm.turnController = turnController;
            gm.placementInventory = gm.GetComponent<PlacementInventory>();
            gm.popManager = gm.gameObject.AddComponent<PopulationManager>();
            gm.interfaceManager = CreateInterfaceManager();
            gm.cameraController = gm.gameObject.AddComponent<CameraController>();
            SetField(gm.cameraController, "mainCamera", mainCamera);
            SetField(gm.cameraController, "secondaryCamera", topDownCamera);
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
            SetField(manager, "quitButton", CreateGameObject("Quit Button").AddComponent<Button>());
            SetField(manager, "nextButton", CreateGameObject("Next Button").AddComponent<Button>());
            SetField(manager, "openShopButton", CreateGameObject("Open Shop Button").AddComponent<Button>());
            SetField(manager, "closeShopButton", CreateGameObject("Close Shop Button").AddComponent<Button>());
            SetField(manager, "mTowerUpgrades", CreateGameObject("Middle Tower Upgrades").AddComponent<Image>());
            SetField(manager, "rTowerUpgrades", CreateGameObject("Right Tower Upgrades").AddComponent<Image>());
            SetField(manager, "lTowerUpgrades", CreateGameObject("Left Tower Upgrades").AddComponent<Image>());
            SetField(manager, "handContainer", CreateGameObject("Hand Container").transform);
            return manager;
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
