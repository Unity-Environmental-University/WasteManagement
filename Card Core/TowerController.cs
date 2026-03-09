using UnityEngine;

namespace _project.Scripts.Card_Core
{
    public class TowerController : MonoBehaviour
    {
        //TODO Make these Cards
        public GameObject[] upgrades = new GameObject[6];

        public bool ValidateUpgrades()
        {
            return upgrades.Length <= 5;
        }
    }
}