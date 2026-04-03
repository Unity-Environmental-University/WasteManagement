using System;
using System.Linq;
using _project.Scripts.Object_Scripts;
using _project.Scripts.UI;
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

        [SerializeField] private HealthBar healthBar;

        private static bool Debugging => GameMaster.Instance.debugging;

        private void OnEnable() => IssueObject.OnReachedEnd += OnIssueReachedEnd;
        private void OnDisable() => IssueObject.OnReachedEnd -= OnIssueReachedEnd;

        private void OnIssueReachedEnd(IssueObject issue)
        {
            ProcessLoad(issue);
        }

        public ICard[] GetCurrentUpgrades() => _upgrades;
        public float maintenanceHealth = BaseHealth;
        public float maintenanceRegen = BaseMaintenanceRegen;
        public float organicProcessPower = BaseOrganicProcessPower;
        public float chemicalProcessPower = BaseChemicalProcessPower;

        public bool AddUpgrade(ICard upgrade)
        {
            if (_upgrades.Contains(upgrade) || _upgrades.Count(u => u != null) >= 6)
                return false;
            for (var i = 0; i < _upgrades.Length; i++)
            {
                if (_upgrades[i] != null) continue;
                _upgrades[i] = upgrade;

                upgrade.ProcessEffect(this);

                GameMaster.Instance.selectedCard = null;
                if (!Debugging) return true;
                Debug.Log(
                    $"[{name}] AddUpgrade: {upgrade.Name} | organic: {organicProcessPower:F2} | chemical: {chemicalProcessPower:F2} | regen: {maintenanceRegen:F2}");
                Debug.Log(
                    $"[{name}] Upgrades filled: {_upgrades.Count(u => u != null)}/6 | valid: {ValidateUpgrades()}");
                return true;
            }

            return false;
        }

        private bool ValidateUpgrades()
        {
            return _upgrades.Count(u => u != null) <= 6;
        }

        private void ProcessLoad(IssueObject issueObject)
        {
            var iType = issueObject.GetIssueType();
            var maintenanceDmg = GetProcessPowerByType(iType);
            var healthBefore = maintenanceHealth;

            maintenanceHealth -= maintenanceDmg;
            maintenanceHealth = Mathf.Clamp(maintenanceHealth, 0f, BaseHealth);

            healthBar?.SetHealth(maintenanceHealth, BaseHealth);

            if (Debugging)
                Debug.Log(
                    $"[{name}] ProcessLoad — type: {iType} | dmg: {maintenanceDmg:F2} | health: {healthBefore:F2} → {maintenanceHealth:F2} / {BaseHealth}");

            if (maintenanceHealth <= 0f) DeactivateTower();
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

        private void DeactivateTower()
        {
            Debug.LogWarning($"[{name}] Tower Deactivated!");
            GameMaster.Instance.turnController.GameLost();
        }
    }
}