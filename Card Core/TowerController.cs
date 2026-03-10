using System.Linq;
using UnityEngine;

namespace _project.Scripts.Card_Core
{
    public class TowerController : MonoBehaviour
    {
        private readonly ICard[] _upgrades = new ICard[6];
        
        public ICard[] GetCurrentUpgrades() => _upgrades;
        
        public ICard[] AddUpgrade(ICard upgrade)
        {
            if (_upgrades == null || _upgrades.Contains(upgrade) || _upgrades.Length >= 6)
                return _upgrades;
            for (var i = 0; i < _upgrades.Length; i++)
            {
                if (_upgrades[i] != null) continue;
                _upgrades[i] = upgrade;
                return _upgrades;
            }
            return _upgrades;
        }
        
        public bool ValidateUpgrades()
        {
            return _upgrades.Length <= 5;
        }
    }
}