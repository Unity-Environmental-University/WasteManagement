using _project.Scripts.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _project.Scripts.UI
{
    public class ShopObject : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Image displayImage;
        [SerializeField] private Button buyButton;
        private bool Debugging => GameMaster.Instance.debugging;

        private IShopItem ShopItem { get; set; }

        public void Setup(IShopItem item)
        {
            ShopItem = item;
            titleText.text = item.DisplayName;
            costText.text = $"{item.Cost} tokens";
            if (descriptionText) descriptionText.text = item.Description;
            if (displayImage && item.DisplaySprite) displayImage.sprite = item.DisplaySprite;

            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(OnBuyPressed);
        }

        private void OnBuyPressed()
        {
            if (!ScoreManager.CanAfford(ShopItem.Cost))
            {
                if (Debugging)
                    Debug.Log($"[ShopObject] Cannot afford {ShopItem.DisplayName} ({ShopItem.Cost} tokens).");
                return;
            }

            ShopItem.Purchase();
            if (ShopItem.RemoveAfterPurchase)
                ShopManager.Instance.RemoveShopItem(gameObject);
        }
    }
}
