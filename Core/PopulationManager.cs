using UnityEngine;

namespace _project.Scripts.Core
{
    public class PopulationManager : MonoBehaviour
    {
        [SerializeField] private int startingPopSize = 4;
        private int _populationSize;

        public void Start()
        {
            if (_populationSize < startingPopSize) 
                _populationSize = startingPopSize;
        }

        public int GetPopulationSize() => _populationSize;

        public void SetPopulationSize(int populationSize)
        {
            _populationSize = Mathf.Max(0, populationSize);
        }

        public void ChangePopulationSize(int delta)
        {
            SetPopulationSize(_populationSize + delta);
        }
        
        public int GetLevelByPopulationSize()
        {
            var size = _populationSize;
            return size switch
            {
                <= 10 => 1,
                <= 20 => 2,
                <= 25 => 3,
                _ => 0
            };
        }
    }
}
