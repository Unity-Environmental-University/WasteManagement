using UnityEngine;

namespace _project.Scripts.Core
{
    public class PopulationManager : MonoBehaviour
    {
        [SerializeField] private int startingPopSize = 4;
        [SerializeField] private int mediumInfrastructurePopSize = 11;
        [SerializeField] private int largeInfrastructurePopSize = 21;
        private int _populationSize;

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

        public void ApplyInfrastructurePopulationGrowth(int infrastructureValue)
        {
            var targetPopulation = infrastructureValue switch
            {
                <= 10 => startingPopSize,
                <= 20 => mediumInfrastructurePopSize,
                _ => largeInfrastructurePopSize
            };

            SetPopulationSize(Mathf.Max(_populationSize, targetPopulation));
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // startingPopSize must stay within level 1 (≤10); medium must cross into level 2 (>10);
            // large must cross into level 3 (>20).
            startingPopSize = Mathf.Clamp(startingPopSize, 1, 10);
            mediumInfrastructurePopSize = Mathf.Max(mediumInfrastructurePopSize, 11);
            largeInfrastructurePopSize = Mathf.Max(largeInfrastructurePopSize, 21);
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
