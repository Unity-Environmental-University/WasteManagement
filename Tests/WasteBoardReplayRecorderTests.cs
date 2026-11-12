using System.Collections.Generic;
using System.Reflection;
using _project.Scripts.Core;
using _project.Scripts.Object_Scripts;
using NUnit.Framework;
using UnityEngine;

namespace _project.Scripts.Tests
{
    public class WasteBoardReplayRecorderTests : PlayModeTestBase
    {
        private PathBuildBoard _board;
        private WasteBoardReplayRecorder _recorder;

        [SetUp]
        public void SetUp()
        {
            _board = CreateGameObject("Board").AddComponent<PathBuildBoard>();
            _recorder = CreateGameObject("Recorder").AddComponent<WasteBoardReplayRecorder>();
            SetField(_recorder, "_board", _board);
        }

        // The sprinkler's square grows 3x3 -> 5x5 -> 9x9 with the town level, so the reported reach
        // (cells on each side) must follow it rather than stay at the level-one value.
        [TestCase(1, "1")]
        [TestCase(2, "2")]
        [TestCase(3, "4")]
        public void SquareContentData_IncludesLimeSprinklerRange_ForTheTownLevel(int level, string expected)
        {
            var gm = CreateGameObject("Game Master").AddComponent<GameMaster>();
            gm.turnController.currentLevel = level;
            var sprinkler = Place<LimeSprinkler>(new Vector2Int(2, 2));

            var data = SnapshotData(sprinkler);

            Assert.AreEqual(expected, data["range-cells"]);
        }

        [Test]
        public void SquareContentData_IncludesCesspitFullnessPercent()
        {
            var cesspit = Place<Cesspit>(new Vector2Int(2, 2));
            cesspit.maxFullness = 8f;
            cesspit.fullness = 2f;

            var data = SnapshotData(cesspit);

            Assert.AreEqual("25", data["fullness-percent"]);
        }

        [Test]
        public void SquareContentData_IncludesWasteSifterHealthPercent()
        {
            var sifter = Place<WasteSifter>(new Vector2Int(2, 2));
            sifter.maxHealth = 40f;
            sifter.health = 10f;

            var data = SnapshotData(sifter);

            Assert.AreEqual("25", data["health-percent"]);
        }

        // StateEquals is what the periodic sync uses to decide whether to re-send a square, so a
        // field it skips would silently freeze that value in the replay.
        [Test]
        public void StateEquals_DetectsCesspitFullnessChange()
        {
            var cesspit = Place<Cesspit>(new Vector2Int(2, 2));
            cesspit.maxFullness = 8f;
            cesspit.fullness = 2f;
            var before = Snapshot(cesspit);
            Assert.IsTrue(StateEquals(before, Snapshot(cesspit)), "An unchanged pit must not re-send.");

            cesspit.fullness = 6f;

            Assert.IsFalse(StateEquals(before, Snapshot(cesspit)));
        }

        [Test]
        public void StateEquals_DetectsWasteSifterHealthChange()
        {
            var sifter = Place<WasteSifter>(new Vector2Int(2, 2));
            sifter.maxHealth = 40f;
            sifter.health = 30f;
            var before = Snapshot(sifter);
            Assert.IsTrue(StateEquals(before, Snapshot(sifter)), "An unchanged sifter must not re-send.");

            sifter.health = 10f;

            Assert.IsFalse(StateEquals(before, Snapshot(sifter)));
        }

        [Test]
        public void PathData_RecordsWhichLayerThePieceIsOn()
        {
            var flags = BindingFlags.Static | BindingFlags.NonPublic;
            var snapshot = typeof(WasteBoardReplayRecorder).GetMethod("Snapshot", flags,
                null, new[] { typeof(PathBuildBoard.PlacedPathPiece) }, null);
            var pathData = typeof(WasteBoardReplayRecorder).GetMethod("PathData", flags);
            Assert.IsNotNull(snapshot);
            Assert.IsNotNull(pathData);

            string Layer(bool elevated)
            {
                var piece = new PathBuildBoard.PlacedPathPiece
                {
                    id = 1, length = 1, elevated = elevated, cells = { new Vector2Int(2, 2) }
                };
                var data = (Dictionary<string, string>)pathData.Invoke(null,
                    new[] { snapshot.Invoke(null, new object[] { piece }), false });
                return data["layer"];
            }

            Assert.AreEqual("elevated", Layer(true));
            Assert.AreEqual("regular", Layer(false), "Crossing pieces in one cell stay distinguishable.");
        }

        private Dictionary<string, string> SnapshotData(MonoBehaviour behaviour)
        {
            var snapshot = Snapshot(behaviour);
            var dataMethod = typeof(WasteBoardReplayRecorder).GetMethod("SquareContentData",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(dataMethod);
            return (Dictionary<string, string>)dataMethod.Invoke(_recorder, new[] { snapshot, false });
        }

        private static bool StateEquals(object snapshot, object other)
        {
            var method = snapshot.GetType().GetMethod("StateEquals", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(method);
            return (bool)method.Invoke(snapshot, new[] { other });
        }

        private object Snapshot(MonoBehaviour behaviour)
        {
            var snapshotMethod = typeof(WasteBoardReplayRecorder).GetMethod("TrySnapshotSquareContent",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(snapshotMethod);

            var arguments = new object[] { behaviour, null };
            Assert.IsTrue((bool)snapshotMethod.Invoke(_recorder, arguments));
            return arguments[1];
        }

        private T Place<T>(Vector2Int cell) where T : MonoBehaviour
        {
            var component = CreateGameObject(typeof(T).Name).AddComponent<T>();
            component.transform.position = _board.GetCellTopPosition(cell);
            return component;
        }
    }
}
