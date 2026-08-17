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
        // Level 1 is an onboarding ramp. It should be challenging to lose all momentum
        // before the player has unlocked the level 2 tools.
        private const float LevelOneWavePressureMultiplier = 0.5f;
        private const int LevelOneGrowthBonus = 4;
        private const float LevelOnePenaltyMultiplier = 0.25f;
        private const int LevelOneMinimumGrowth = 2;

        [SerializeField] private int startingPopSize = 4;

        [Header("Level Thresholds")]
        [SerializeField] private int levelTwoPopulationThreshold = 10;
        [SerializeField] private int levelThreePopulationThreshold = 16;

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

        [Header("Stink")]
        [SerializeField] private float baseStinkValue;

        private int _populationSize;
        private float _wavePollution;

        /// <summary>
        ///     The current town stink level. Includes the base value plus active lake and utility stink sources.
        /// </summary>
        public float StinkValue
        {
            get => GetCurrentStink();
            set => baseStinkValue = Mathf.Max(0f, value);
        }

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

        public float GetCurrentStink()
        {
            return StinkSourceRegistry.GetCurrentStink(baseStinkValue);
        }

        /// <summary>
        ///     Grows the population after a wave: a steady base rate plus a small
        ///     infrastructure bonus, reduced by pollution that reached the lake this wave
        ///     and by town stink. Level 1 adds an onboarding bonus, softens those penalties,
        ///     and guarantees some progress; later levels can halt growth when penalties
        ///     outweigh the gains.
        /// </summary>
        public PostWaveGrowthResult ApplyPostWaveGrowth(int infrastructureValue)
        {
            EnsureStartingPopulation();
            var populationBefore = _populationSize;
            var levelBefore = CalculateLevelByPopulationSize(populationBefore);
            var pollution = _wavePollution;
            var stink = GetCurrentStink();

            var isLevelOne = levelBefore == 1;
            var penaltyMultiplier = isLevelOne ? LevelOnePenaltyMultiplier : 1f;
            var growth = baseGrowthPerWave
                         + (isLevelOne ? LevelOneGrowthBonus : 0)
                         + infrastructureValue * infrastructureGrowthBonus
                         - pollution * pollutionGrowthPenalty * penaltyMultiplier
                         - stink * stinkGrowthPenalty * penaltyMultiplier;

            _wavePollution = 0f;
            var minimumGrowth = isLevelOne ? LevelOneMinimumGrowth : 0;
            var appliedGrowth = Mathf.Max(minimumGrowth, Mathf.FloorToInt(growth));
            SetPopulationSize(_populationSize + appliedGrowth);

            return new PostWaveGrowthResult(
                populationBefore,
                _populationSize,
                levelBefore,
                CalculateLevelByPopulationSize(_populationSize),
                appliedGrowth,
                growth,
                pollution,
                stink);
        }

        /// <summary>
        ///     Spawn-rate multiplier for issue spawners: lower at the starting population,
        ///     growing by <see cref="spawnRateGrowthPerPop" /> per resident above it.
        /// </summary>
        public float GetIssueSpawnRateMultiplier()
        {
            var multiplier = GetPopulationScaledValue(startingSpawnRateMultiplier, spawnRateGrowthPerPop, 0.01f);
            return GetLevelByPopulationSize() == 1
                ? multiplier * LevelOneWavePressureMultiplier
                : multiplier;
        }

        public float GetScaledWaveDuration(float baseDuration)
        {
            var duration = Mathf.Max(0f, baseDuration) *
                           GetPopulationScaledValue(startingWaveDurationMultiplier, waveDurationGrowthPerPop, 0f);
            return GetLevelByPopulationSize() == 1
                ? duration * LevelOneWavePressureMultiplier
                : duration;
        }

        private float GetPopulationScaledValue(float startingValue, float growthPerPop, float minimum)
        {
            EnsureStartingPopulation();
            return Mathf.Max(minimum, startingValue + PopulationAboveStart() * growthPerPop);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            levelTwoPopulationThreshold = Mathf.Max(1, levelTwoPopulationThreshold);
            levelThreePopulationThreshold = Mathf.Max(levelTwoPopulationThreshold + 1, levelThreePopulationThreshold);
            // startingPopSize must stay within level 1.
            startingPopSize = Mathf.Clamp(startingPopSize, 1, levelTwoPopulationThreshold);
            startingSpawnRateMultiplier = Mathf.Max(startingSpawnRateMultiplier, 0.01f);
            spawnRateGrowthPerPop = Mathf.Max(spawnRateGrowthPerPop, 0f);
            startingWaveDurationMultiplier = Mathf.Max(startingWaveDurationMultiplier, 0f);
            waveDurationGrowthPerPop = Mathf.Max(waveDurationGrowthPerPop, 0f);
            baseGrowthPerWave = Mathf.Max(baseGrowthPerWave, 0);
            infrastructureGrowthBonus = Mathf.Max(infrastructureGrowthBonus, 0f);
            pollutionGrowthPenalty = Mathf.Max(pollutionGrowthPenalty, 0f);
            stinkGrowthPenalty = Mathf.Max(stinkGrowthPenalty, 0f);
            baseStinkValue = Mathf.Max(baseStinkValue, 0f);
        }
#endif

        public int GetLevelByPopulationSize()
        {
            EnsureStartingPopulation();
            return CalculateLevelByPopulationSize(_populationSize);
        }

        private int CalculateLevelByPopulationSize(int populationSize)
        {
            if (populationSize <= levelTwoPopulationThreshold) return 1;
            if (populationSize <= levelThreePopulationThreshold) return 2;
            return 3;
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
