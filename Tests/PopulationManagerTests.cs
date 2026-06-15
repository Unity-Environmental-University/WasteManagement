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
        public void ApplyPostWaveGrowth_CleanWave_GrowsByBaseRate()
        {
            var populationManager = CreatePopulationManager();

            populationManager.ApplyPostWaveGrowth(0);

            // Starting pop 4 + base growth 3.
            Assert.AreEqual(7, populationManager.GetPopulationSize());
        }

        [Test]
        public void ApplyPostWaveGrowth_CleanWaves_GrowConsistently()
        {
            var populationManager = CreatePopulationManager();

            populationManager.ApplyPostWaveGrowth(0);
            populationManager.ApplyPostWaveGrowth(0);
            populationManager.ApplyPostWaveGrowth(0);

            Assert.AreEqual(13, populationManager.GetPopulationSize());
            Assert.AreEqual(2, populationManager.GetLevelByPopulationSize());
        }

        [Test]
        public void ApplyPostWaveGrowth_Infrastructure_AddsOnlySmallBonus()
        {
            var populationManager = CreatePopulationManager();

            populationManager.ApplyPostWaveGrowth(20);

            // Base 3 + 20 infra * 0.1 bonus = 5.
            Assert.AreEqual(9, populationManager.GetPopulationSize());
        }

        [Test]
        public void ApplyPostWaveGrowth_LakePollution_GreatlyReducesGrowth()
        {
            var populationManager = CreatePopulationManager();

            populationManager.RecordLakePollution(2f);
            populationManager.ApplyPostWaveGrowth(0);

            // Base 3 - 2 pollution = 1.
            Assert.AreEqual(5, populationManager.GetPopulationSize());
        }

        [Test]
        public void ApplyPostWaveGrowth_HeavyPollution_HaltsGrowthButNeverShrinks()
        {
            var populationManager = CreatePopulationManager();

            populationManager.RecordLakePollution(50f);
            populationManager.ApplyPostWaveGrowth(0);

            Assert.AreEqual(4, populationManager.GetPopulationSize());
        }

        [Test]
        public void ApplyPostWaveGrowth_PollutionResetsBetweenWaves()
        {
            var populationManager = CreatePopulationManager();

            populationManager.RecordLakePollution(50f);
            populationManager.ApplyPostWaveGrowth(0);
            populationManager.ApplyPostWaveGrowth(0);

            // The polluted wave halts growth; the following clean wave grows normally.
            Assert.AreEqual(7, populationManager.GetPopulationSize());
        }

        [Test]
        public void ApplyPostWaveGrowth_Stink_ReducesGrowth()
        {
            var populationManager = CreatePopulationManager();

            populationManager.StinkValue = 4f;
            populationManager.ApplyPostWaveGrowth(0);

            // Base 3 - 4 stink * 0.5 penalty = 1.
            Assert.AreEqual(5, populationManager.GetPopulationSize());
        }

        [Test]
        public void GetIssueSpawnRateMultiplier_StartingPopulation_IsLighterThanBaseline()
        {
            var populationManager = CreatePopulationManager();

            Assert.AreEqual(0.7f, populationManager.GetIssueSpawnRateMultiplier(), 0.0001f);
        }

        [Test]
        public void GetIssueSpawnRateMultiplier_GrowsWithPopulation()
        {
            var populationManager = CreatePopulationManager();

            IncreasePopulationAboveStart(populationManager);

            // Pop 11 vs starting 4 at 0.05 growth per resident from a 0.7x starting rate = 1.05x.
            Assert.AreEqual(1.05f, populationManager.GetIssueSpawnRateMultiplier(), 0.0001f);
        }

        [Test]
        public void GetIssueSpawnRateMultiplier_PopulationBelowStarting_DoesNotDropBelowStartingRate()
        {
            var populationManager = CreatePopulationManager();

            populationManager.ChangePopulationSize(-10);

            Assert.AreEqual(0.7f, populationManager.GetIssueSpawnRateMultiplier(), 0.0001f);
        }

        [Test]
        public void GetScaledWaveDuration_StartingPopulation_IsShorterThanBaseDuration()
        {
            var populationManager = CreatePopulationManager();

            Assert.AreEqual(18f, populationManager.GetScaledWaveDuration(30f), 0.0001f);
        }

        [Test]
        public void GetScaledWaveDuration_GrowsWithPopulation()
        {
            var populationManager = CreatePopulationManager();

            IncreasePopulationAboveStart(populationManager);

            // Pop 11 vs starting 4 at 0.04 growth per resident from a 0.6x starting duration = 0.88x.
            Assert.AreEqual(26.4f, populationManager.GetScaledWaveDuration(30f), 0.0001f);
        }

        private static void IncreasePopulationAboveStart(PopulationManager populationManager)
        {
            populationManager.GetLevelByPopulationSize();
            populationManager.ChangePopulationSize(7);
        }

        private PopulationManager CreatePopulationManager()
        {
            var go = new GameObject("Population Manager");
            _created.Add(go);
            return go.AddComponent<PopulationManager>();
        }
    }
}
