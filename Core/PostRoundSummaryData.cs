using System.Collections.Generic;

namespace _project.Scripts.Core
{
    public readonly struct CesspitSummary
    {
        public CesspitSummary(int count, int fullCount, float fullness, float capacity)
        {
            Count = count;
            FullCount = fullCount;
            Fullness = fullness;
            Capacity = capacity;
        }

        public int Count { get; }
        public int FullCount { get; }
        public float Fullness { get; }
        public float Capacity { get; }
    }

    public sealed class PostRoundSummaryData
    {
        public PostRoundSummaryData(int roundNumber, int moveCount, PostWaveGrowthResult growthResult,
            float nextWaveSpawnRateMultiplier, IReadOnlyList<string> unlockedItems, CesspitSummary cesspits)
        {
            RoundNumber = roundNumber;
            MoveCount = moveCount;
            GrowthResult = growthResult;
            NextWaveSpawnRateMultiplier = nextWaveSpawnRateMultiplier;
            UnlockedItems = unlockedItems ?? new List<string>();
            Cesspits = cesspits;
        }

        public int RoundNumber { get; }
        public int MoveCount { get; }
        public PostWaveGrowthResult GrowthResult { get; }
        public float NextWaveSpawnRateMultiplier { get; }
        public IReadOnlyList<string> UnlockedItems { get; }
        public CesspitSummary Cesspits { get; }
    }
}
