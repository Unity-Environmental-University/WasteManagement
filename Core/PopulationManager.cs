using UnityEngine;

namespace _project.Scripts.Core
{
    public class PopulationManager : MonoBehaviour
    {
        [SerializeField] private int startingPopSize = 4;
        [SerializeField] private float spawnRateGrowthPerPop = 0.1f;

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
        public void ApplyPostWaveGrowth(int infrastructureValue)
        {
            EnsureStartingPopulation();

            var growth = baseGrowthPerWave
                         + infrastructureValue * infrastructureGrowthBonus
                         - _wavePollution * pollutionGrowthPenalty
                         - StinkValue * stinkGrowthPenalty;

            _wavePollution = 0f;
            SetPopulationSize(_populationSize + Mathf.Max(0, Mathf.FloorToInt(growth)));
        }

        /// <summary>
        ///     Spawn-rate multiplier for issue spawners: 1x at the starting population,
        ///     growing by <see cref="spawnRateGrowthPerPop" /> per resident above it.
        /// </summary>
        public float GetIssueSpawnRateMultiplier()
        {
            EnsureStartingPopulation();
            return Mathf.Max(1f, 1f + (_populationSize - startingPopSize) * spawnRateGrowthPerPop);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // startingPopSize must stay within level 1 (≤10).
            startingPopSize = Mathf.Clamp(startingPopSize, 1, 10);
            spawnRateGrowthPerPop = Mathf.Max(spawnRateGrowthPerPop, 0f);
            baseGrowthPerWave = Mathf.Max(baseGrowthPerWave, 0);
            infrastructureGrowthBonus = Mathf.Max(infrastructureGrowthBonus, 0f);
            pollutionGrowthPenalty = Mathf.Max(pollutionGrowthPenalty, 0f);
            stinkGrowthPenalty = Mathf.Max(stinkGrowthPenalty, 0f);
        }
#endif

        public int GetLevelByPopulationSize()
        {
            EnsureStartingPopulation();
            var size = _populationSize;
            return size switch
            {
                <= 10 => 1,
                <= 20 => 2,
                _ => 3
            };
        }

        private void EnsureStartingPopulation()
        {
            if (_populationSize < startingPopSize)
                _populationSize = startingPopSize;
        }
    }
}
