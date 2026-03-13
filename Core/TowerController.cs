using System.Linq;
using UnityEngine;

namespace _project.Scripts.Core
{
    public class TowerController : MonoBehaviour
    {
        private readonly ICard[] _upgrades = new ICard[6];
        
        public ICard[] GetCurrentUpgrades() => _upgrades;
        
        public void AddUpgrade(ICard upgrade)
        {
            if (_upgrades == null || _upgrades.Contains(upgrade) || _upgrades.Count(u => u != null) >= 6)
                return;
            for (var i = 0; i < _upgrades.Length; i++)
            {
                if (_upgrades[i] != null) continue;
                _upgrades[i] = upgrade;

                //Todo move the card back down
                GameMaster.Instance.SelectedCard = null;
                Debug.Log("Adding upgrade: " + upgrade);
                Debug.Log("Upgrades: " + _upgrades.Length);
                return;
            }
        }
        
        public bool ValidateUpgrades()
        {
            return _upgrades.Length <= 5;
        }
    }
}