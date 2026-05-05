using UnityEngine;

namespace _project.Scripts.Core
{
    public class PopulationManager : MonoBehaviour
    {
        [SerializeField] private int startingPopSize = 4;
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
        
        public int GetLevelByPopulationSize()
        {
            EnsureStartingPopulation();
            var size = _populationSize;
            return size switch
            {
                <= 10 => 1,
                <= 20 => 2,
                <= 25 => 3,
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
