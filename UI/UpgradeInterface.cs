using _project.Scripts.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _project.Scripts.UI
{
    public class UpgradeInterface : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private Image highlight;
        [SerializeField] private TowerController towerController;
        [SerializeField] private Image[] upgradeSlots;
        [SerializeField] private CardSpriteLibrary spriteLibrary;
        [SerializeField] private TextMeshProUGUI statsPanel;

        private void Start()
        {
            if (towerController == null) Debug.LogError("Tower Controller not found!");
            RefreshDisplay();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            statsPanel.text = BuildStatsString();
            highlight.gameObject.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            highlight.gameObject.SetActive(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            var gm = GameMaster.Instance;
            var selected = gm.selectedCard;
            if (selected == null) return;

            if (!towerController.AddUpgrade(selected.InterFaceCard)) return;
            gm.deckManager.DiscardCard(selected.InterFaceCard);
            Destroy(selected.gameObject);

            RefreshDisplay();
            statsPanel.text = BuildStatsString();
        }

        private void RefreshDisplay()
        {
            if (upgradeSlots == null || towerController == null) return;

            var upgrades = towerController.GetCurrentUpgrades();
            for (var i = 0; i < upgradeSlots.Length; i++)
            {
                if (upgradeSlots[i] == null) continue;
                var upgrade = i < upgrades.Length ? upgrades[i] : null;
                upgradeSlots[i].sprite = upgrade != null ? spriteLibrary?.GetSprite(upgrade.Name) : null;
                upgradeSlots[i].enabled = upgrade != null;
            }
        }

        private string BuildStatsString()
        {
            var tc = towerController;
            return $"HP: {tc.maintenanceHealth:F0}  Regen: {tc.maintenanceRegen:F1}  Organic: {tc.organicProcessPower:F2}  Chemical: {tc.chemicalProcessPower:F2}";
        }
    }
}