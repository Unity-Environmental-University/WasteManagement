using System.Collections.Generic;
using System.Linq;
using _project.Scripts.Core;
using NUnit.Framework;
using UnityEngine;

namespace _project.Scripts.Tests
{
    public class PopulationManagerTests
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
        public void ApplyInfrastructurePopulationGrowth_LowInfrastructure_DoesNotAddStartingPopulationRepeatedly()
        {
            var populationManager = CreatePopulationManager();

            populationManager.ApplyInfrastructurePopulationGrowth(0);
            populationManager.ApplyInfrastructurePopulationGrowth(0);

            Assert.AreEqual(4, populationManager.GetPopulationSize());
            Assert.AreEqual(1, populationManager.GetLevelByPopulationSize());
        }

        [Test]
        public void ApplyInfrastructurePopulationGrowth_MediumInfrastructure_AdvancesToLevelTwo()
        {
            var populationManager = CreatePopulationManager();

            populationManager.ApplyInfrastructurePopulationGrowth(11);

            Assert.AreEqual(11, populationManager.GetPopulationSize());
            Assert.AreEqual(2, populationManager.GetLevelByPopulationSize());
        }

        [Test]
        public void ApplyInfrastructurePopulationGrowth_HighInfrastructure_AdvancesToLevelThree()
        {
            var populationManager = CreatePopulationManager();

            populationManager.ApplyInfrastructurePopulationGrowth(26);

            Assert.AreEqual(21, populationManager.GetPopulationSize());
            Assert.AreEqual(3, populationManager.GetLevelByPopulationSize());
        }

        [Test]
        public void ApplyInfrastructurePopulationGrowth_LowerInfrastructure_DoesNotShrinkPopulation()
        {
            var populationManager = CreatePopulationManager();

            populationManager.ApplyInfrastructurePopulationGrowth(26);
            populationManager.ApplyInfrastructurePopulationGrowth(0);

            Assert.AreEqual(21, populationManager.GetPopulationSize());
            Assert.AreEqual(3, populationManager.GetLevelByPopulationSize());
        }

        [Test]
        public void GetIssueSpawnRateMultiplier_StartingPopulation_IsBaseRate()
        {
            var populationManager = CreatePopulationManager();

            Assert.AreEqual(1f, populationManager.GetIssueSpawnRateMultiplier());
        }

        [Test]
        public void GetIssueSpawnRateMultiplier_GrowsWithPopulation()
        {
            var populationManager = CreatePopulationManager();

            populationManager.ApplyInfrastructurePopulationGrowth(11);

            // Pop 11 vs starting 4 at 0.1 growth per resident = 1.7x.
            Assert.AreEqual(1.7f, populationManager.GetIssueSpawnRateMultiplier(), 0.0001f);
        }

        [Test]
        public void GetIssueSpawnRateMultiplier_PopulationBelowStarting_DoesNotDropBelowBaseRate()
        {
            var populationManager = CreatePopulationManager();

            populationManager.ChangePopulationSize(-10);

            Assert.AreEqual(1f, populationManager.GetIssueSpawnRateMultiplier());
        }

        private PopulationManager CreatePopulationManager()
        {
            var go = new GameObject("Population Manager");
            _created.Add(go);
            return go.AddComponent<PopulationManager>();
        }
    }
}