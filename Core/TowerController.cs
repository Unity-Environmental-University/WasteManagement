using System;
using System.Linq;
using _project.Scripts.Object_Scripts;
using UnityEngine;

namespace _project.Scripts.Core
{
    public class TowerController : MonoBehaviour
    {
        private readonly ICard[] _upgrades = new ICard[6];
        private const float BaseHealth = 100f;
        private const float BaseMaintenanceRegen = 15;
        private const float BaseOrganicProcessPower = 1f;
        private const float BaseChemicalProcessPower = 1f;

        public ICard[] GetCurrentUpgrades() => _upgrades;
        public float maintenanceHealth = BaseHealth;
        public float maintenanceRegen = BaseMaintenanceRegen;
        public float organicProcessPower = BaseOrganicProcessPower;
        public float chemicalProcessPower = BaseChemicalProcessPower;
        
        public void AddUpgrade(ICard upgrade)
        {
            if (_upgrades.Contains(upgrade) || _upgrades.Count(u => u != null) >= 6)
                return;
            for (var i = 0; i < _upgrades.Length; i++)
            {
                if (_upgrades[i] != null) continue;
                _upgrades[i] = upgrade;

                //Todo move the card back down
                GameMaster.Instance.selectedCard = null;
                Debug.Log("Adding upgrade: " + upgrade);
                Debug.Log("Upgrades: " + _upgrades.Length);
                Debug.Log("Are Upgrades Valid: " + ValidateUpgrades());
                return;
            }
        }

        private bool ValidateUpgrades()
        {
            return _upgrades.Count(u => u != null) <= 6;
        }

        // TODO: make this consider the type of issue and the tower's upgrades to determine the maintenance cost
        public void ProcessLoad(IssueObject issueObject)
        {
            var iType = issueObject.GetIssueType();
            var maintenanceDmg = GetProcessPowerByType(iType);

            maintenanceHealth -= maintenanceDmg;
            maintenanceHealth = Mathf.Clamp(maintenanceHealth, 0f, BaseHealth);
            if (maintenanceHealth <= 0f)
            {
                DeactivateTower();
            }
        }

        private float GetProcessPowerByType(IssueType type)
        {
            return type switch
            {
                IssueType.Organic => organicProcessPower,
                IssueType.Chemical => chemicalProcessPower,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }

        //TODO
        private void DeactivateTower()
        {
            Debug.LogWarning("Tower Deactivated!");
        }
    }
}