using _project.Scripts.Core;
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

        private void Start()
        {
            if (towerController == null) Debug.LogError("Tower Controller not found!");
            RefreshDisplay();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
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
            if (selected is null) return;

            towerController.AddUpgrade(selected.interFaceCard);
            gm.deckManager.DiscardCard(selected.interFaceCard);
            Destroy(selected.gameObject);

            RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            if (upgradeSlots == null || towerController == null) return;

            var upgrades = towerController.GetCurrentUpgrades();
            for (var i = 0; i < upgradeSlots.Length; i++)
            {
                if (upgradeSlots[i] == null) continue;
                var upgrade = i < upgrades.Length ? upgrades[i] : null;
                upgradeSlots[i].sprite = upgrade?.CardImage?.sprite;
                upgradeSlots[i].enabled = upgrade != null;
            }
        }
    }
}