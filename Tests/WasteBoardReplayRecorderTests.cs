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

        [Test]
        public void SquareContentData_IncludesLimeSprinklerRange()
        {
            var sprinkler = Place<LimeSprinkler>(new Vector2Int(2, 2));

            var data = SnapshotData(sprinkler);

            Assert.AreEqual("1", data["range-cells"]);
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

        [Test]
        public void SnapshotSignature_ChangesWithCesspitFullness()
        {
            var cesspit = Place<Cesspit>(new Vector2Int(2, 2));
            cesspit.maxFullness = 8f;
            cesspit.fullness = 2f;
            var before = SnapshotSignature(cesspit);

            cesspit.fullness = 6f;

            Assert.AreNotEqual(before, SnapshotSignature(cesspit));
        }

        [Test]
        public void SnapshotSignature_ChangesWithWasteSifterHealth()
        {
            var sifter = Place<WasteSifter>(new Vector2Int(2, 2));
            sifter.maxHealth = 40f;
            sifter.health = 30f;
            var before = SnapshotSignature(sifter);

            sifter.health = 10f;

            Assert.AreNotEqual(before, SnapshotSignature(sifter));
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

            string Layer(bool isolated)
            {
                var piece = new PathBuildBoard.PlacedPathPiece
                {
                    id = 1, length = 1, isolated = isolated, cells = { new Vector2Int(2, 2) }
                };
                var data = (Dictionary<string, string>)pathData.Invoke(null,
                    new[] { snapshot.Invoke(null, new object[] { piece }), false });
                return data["layer"];
            }

            Assert.AreEqual("isolated", Layer(true));
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

        private string SnapshotSignature(MonoBehaviour behaviour)
        {
            var snapshot = Snapshot(behaviour);
            var property = snapshot.GetType().GetProperty("Signature",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(property);
            return (string)property.GetValue(snapshot);
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
