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

        private GameObject CreateGameObject(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }
    }
}
