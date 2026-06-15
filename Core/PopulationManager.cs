using UnityEngine;

namespace _project.Scripts.Core
{
    public readonly struct PostWaveGrowthResult
    {
        public PostWaveGrowthResult(int populationBefore, int populationAfter, int levelBefore, int levelAfter,
            int appliedGrowth, float rawGrowth, float pollution, float stink)
        {
            PopulationBefore = populationBefore;
            PopulationAfter = populationAfter;
            LevelBefore = levelBefore;
            LevelAfter = levelAfter;
            AppliedGrowth = appliedGrowth;
            RawGrowth = rawGrowth;
            Pollution = pollution;
            Stink = stink;
        }

        public int PopulationBefore { get; }
        public int PopulationAfter { get; }
        public int LevelBefore { get; }
        public int LevelAfter { get; }
        public int AppliedGrowth { get; }
        public float RawGrowth { get; }
        public float Pollution { get; }
        public float Stink { get; }
        public bool LeveledUp => LevelAfter > LevelBefore;

        /// <summary>An empty result for when no population manager is available, reporting only the known level.</summary>
        public static PostWaveGrowthResult None(int level)
        {
            return new PostWaveGrowthResult(0, 0, level, level, 0, 0f, 0f, 0f);
        }
    }

    public class PopulationManager : MonoBehaviour
    {
        [SerializeField] private int startingPopSize = 4;

        [Header("Wave Pressure")]
        [SerializeField] private float startingSpawnRateMultiplier = 0.7f;
        [SerializeField] private float spawnRateGrowthPerPop = 0.05f;
        [SerializeField] private float startingWaveDurationMultiplier = 0.6f;
        [SerializeField] private float waveDurationGrowthPerPop = 0.04f;

        [Header("Post-Wave Growth")]
        [SerializeField] private int baseGrowthPerWave = 3;
        [SerializeField] private float infrastructureGrowthBonus = 0.1f;
        [SerializeField] private float pollutionGrowthPenalty = 1f;
        [SerializeField] private float stinkGrowthPenalty = 0.5f;

        private int _populationSize;
        private float _wavePollution;

        /// <summary>
        ///     Town stink level (planned feature). Reduces or halts growth as it rises.
        /// </summary>
        public float StinkValue { get; set; }

        private void Awake()
        {
            EnsureStartingPopulation();
        }

        public int GetPopulationSize() => _populationSize;

        private void SetPopulationSize(int populationSize)
        {
            _populationSize = Mathf.Max(0, populationSize);
        }

        public void ChangePopulationSize(int delta)
        {
            SetPopulationSize(_populationSize + delta);
        }

        /// <summary>
        ///     Records pollution from waste that reached the lake during the current wave.
        ///     Consumed (and reset) by the next <see cref="ApplyPostWaveGrowth" />.
        /// </summary>
        public void RecordLakePollution(float amount)
        {
            _wavePollution += Mathf.Max(0f, amount);
        }

        public float GetWavePollution() => _wavePollution;

        /// <summary>
        ///     Grows the population after a wave: a steady base rate plus a small
        ///     infrastructure bonus, greatly reduced by pollution that reached the lake
        ///     this wave and reduced by town stink. Growth is halted (never negative)
        ///     when penalties outweigh the gains.
        /// </summary>
        public PostWaveGrowthResult ApplyPostWaveGrowth(int infrastructureValue)
        {
            EnsureStartingPopulation();
            var populationBefore = _populationSize;
            var levelBefore = CalculateLevelByPopulationSize(populationBefore);
            var pollution = _wavePollution;

            var growth = baseGrowthPerWave
                         + infrastructureValue * infrastructureGrowthBonus
                         - pollution * pollutionGrowthPenalty
                         - StinkValue * stinkGrowthPenalty;

            _wavePollution = 0f;
            var appliedGrowth = Mathf.Max(0, Mathf.FloorToInt(growth));
            SetPopulationSize(_populationSize + appliedGrowth);

            return new PostWaveGrowthResult(
                populationBefore,
                _populationSize,
                levelBefore,
                CalculateLevelByPopulationSize(_populationSize),
                appliedGrowth,
                growth,
                pollution,
                StinkValue);
        }

        /// <summary>
        ///     Spawn-rate multiplier for issue spawners: lower at the starting population,
        ///     growing by <see cref="spawnRateGrowthPerPop" /> per resident above it.
        /// </summary>
        public float GetIssueSpawnRateMultiplier()
        {
            return GetPopulationScaledValue(startingSpawnRateMultiplier, spawnRateGrowthPerPop, 0.01f);
        }

        public float GetScaledWaveDuration(float baseDuration)
        {
            return Mathf.Max(0f, baseDuration) *
                   GetPopulationScaledValue(startingWaveDurationMultiplier, waveDurationGrowthPerPop, 0f);
        }

        private float GetPopulationScaledValue(float startingValue, float growthPerPop, float minimum)
        {
            EnsureStartingPopulation();
            return Mathf.Max(minimum, startingValue + PopulationAboveStart() * growthPerPop);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // startingPopSize must stay within level 1 (≤10).
            startingPopSize = Mathf.Clamp(startingPopSize, 1, 10);
            startingSpawnRateMultiplier = Mathf.Max(startingSpawnRateMultiplier, 0.01f);
            spawnRateGrowthPerPop = Mathf.Max(spawnRateGrowthPerPop, 0f);
            startingWaveDurationMultiplier = Mathf.Max(startingWaveDurationMultiplier, 0f);
            waveDurationGrowthPerPop = Mathf.Max(waveDurationGrowthPerPop, 0f);
            baseGrowthPerWave = Mathf.Max(baseGrowthPerWave, 0);
            infrastructureGrowthBonus = Mathf.Max(infrastructureGrowthBonus, 0f);
            pollutionGrowthPenalty = Mathf.Max(pollutionGrowthPenalty, 0f);
            stinkGrowthPenalty = Mathf.Max(stinkGrowthPenalty, 0f);
        }
#endif

        public int GetLevelByPopulationSize()
        {
            EnsureStartingPopulation();
            return CalculateLevelByPopulationSize(_populationSize);
        }

        private static int CalculateLevelByPopulationSize(int populationSize)
        {
            return populationSize switch
            {
                <= 10 => 1,
                <= 20 => 2,
                _ => 3
            };
        }

        private int PopulationAboveStart()
        {
            return Mathf.Max(0, _populationSize - startingPopSize);
        }

        private void EnsureStartingPopulation()
        {
            if (_populationSize < startingPopSize)
                _populationSize = startingPopSize;
        }
    }
}
