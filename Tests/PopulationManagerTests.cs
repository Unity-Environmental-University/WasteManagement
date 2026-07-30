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
        public void ApplyPostWaveGrowth_CleanFirstWave_ReachesLevelTwo()
        {
            var populationManager = CreatePopulationManager();

            var result = populationManager.ApplyPostWaveGrowth(0);

            // Starting pop 4 + base growth 3 + level-one onboarding bonus 4.
            Assert.AreEqual(11, populationManager.GetPopulationSize());
            Assert.AreEqual(2, populationManager.GetLevelByPopulationSize());
            Assert.IsTrue(result.LeveledUp);
        }

        [Test]
        public void ApplyPostWaveGrowth_LevelTwo_ReturnsToNormalGrowth()
        {
            var populationManager = CreatePopulationManager();

            populationManager.ApplyPostWaveGrowth(0);
            populationManager.ApplyPostWaveGrowth(0);

            Assert.AreEqual(14, populationManager.GetPopulationSize());
            Assert.AreEqual(2, populationManager.GetLevelByPopulationSize());
        }

        [Test]
        public void ApplyPostWaveGrowth_Infrastructure_AddsOnlySmallBonus()
        {
            var populationManager = CreatePopulationManager();

            populationManager.ApplyPostWaveGrowth(20);

            // Base 3 + level-one bonus 4 + 20 infra * 0.1 bonus = 9.
            Assert.AreEqual(13, populationManager.GetPopulationSize());
        }

        [Test]
        public void ApplyPostWaveGrowth_LevelOneLakePollution_HasGentlePenalty()
        {
            var populationManager = CreatePopulationManager();

            populationManager.RecordLakePollution(2f);
            populationManager.ApplyPostWaveGrowth(0);

            // Level-one penalties are quarter strength: 3 base + 4 bonus - 0.5 pollution = 6.5.
            Assert.AreEqual(10, populationManager.GetPopulationSize());
        }

        [Test]
        public void ApplyPostWaveGrowth_HeavyPollution_StillMakesMinimumLevelOneProgress()
        {
            var populationManager = CreatePopulationManager();

            populationManager.RecordLakePollution(50f);
            populationManager.ApplyPostWaveGrowth(0);

            Assert.AreEqual(6, populationManager.GetPopulationSize());
        }

        [Test]
        public void ApplyPostWaveGrowth_PollutionResetsBetweenWaves()
        {
            var populationManager = CreatePopulationManager();

            populationManager.RecordLakePollution(50f);
            populationManager.ApplyPostWaveGrowth(0);
            populationManager.ApplyPostWaveGrowth(0);

            // The polluted wave still adds 2; the following clean wave reaches level 2.
            Assert.AreEqual(13, populationManager.GetPopulationSize());
        }

        [Test]
        public void ApplyPostWaveGrowth_LevelOneStink_HasGentlePenalty()
        {
            var populationManager = CreatePopulationManager();

            populationManager.StinkValue = 4f;
            populationManager.ApplyPostWaveGrowth(0);

            // Level-one penalties are quarter strength: 3 base + 4 bonus - 0.5 stink = 6.5.
            Assert.AreEqual(10, populationManager.GetPopulationSize());
        }

        [Test]
        public void GetCurrentStink_ActiveSources_AddToBaseStink()
        {
            var populationManager = CreatePopulationManager();
            populationManager.StinkValue = 1f;
            CreateStinkSource(2f);
            CreateStinkSource(3f);

            Assert.AreEqual(6f, populationManager.GetCurrentStink(), 0.0001f);
        }

        [Test]
        public void GetCurrentStink_ReductionsCannotGoBelowZero()
        {
            var populationManager = CreatePopulationManager();
            populationManager.StinkValue = 1f;
            CreateStinkSource(-5f);

            Assert.AreEqual(0f, populationManager.GetCurrentStink(), 0.0001f);
        }

        [Test]
        public void ApplyPostWaveGrowth_UtilityStinkSources_ReduceGrowth()
        {
            var populationManager = CreatePopulationManager();
            CreateStinkSource(4f);

            var result = populationManager.ApplyPostWaveGrowth(0);

            // Level-one penalties are quarter strength: 3 base + 4 bonus - 0.5 stink = 6.5.
            Assert.AreEqual(10, populationManager.GetPopulationSize());
            Assert.AreEqual(4f, result.Stink, 0.0001f);
        }

        [Test]
        public void GetIssueSpawnRateMultiplier_StartingPopulation_IsLighterThanBaseline()
        {
            var populationManager = CreatePopulationManager();

            Assert.AreEqual(0.35f, populationManager.GetIssueSpawnRateMultiplier(), 0.0001f);
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
        public void GetIssueSpawnRateMultiplier_PopulationBelowStarting_UsesLevelOneAssistance()
        {
            var populationManager = CreatePopulationManager();

            populationManager.ChangePopulationSize(-10);

            Assert.AreEqual(0.35f, populationManager.GetIssueSpawnRateMultiplier(), 0.0001f);
        }

        [Test]
        public void GetScaledWaveDuration_StartingPopulation_IsShorterThanBaseDuration()
        {
            var populationManager = CreatePopulationManager();

            Assert.AreEqual(9f, populationManager.GetScaledWaveDuration(30f), 0.0001f);
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

        private void CreateStinkSource(float currentStink)
        {
            var go = new GameObject("Stink Source");
            _created.Add(go);
            go.AddComponent<TestStinkSource>().CurrentStink = currentStink;
        }

        private sealed class TestStinkSource : MonoBehaviour, IStinkSource
        {
            public float CurrentStink { get; set; }

            private void OnEnable()
            {
                StinkSourceRegistry.Register(this);
            }

            private void OnDisable()
            {
                StinkSourceRegistry.Unregister(this);
            }
        }
    }
}
