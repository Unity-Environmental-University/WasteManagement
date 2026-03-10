using UnityEngine;

namespace _project.Scripts.Card_Core
{
    public class TowerController : MonoBehaviour
    {
        public ICard[] upgrades = new ICard[6];

        public bool ValidateUpgrades()
        {
            return upgrades.Length <= 5;
        }
    }
}