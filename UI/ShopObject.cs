using _project.Scripts.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace _project.Scripts.UI
{
    public class ShopObject : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [FormerlySerializedAs("costText")]
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Image displayImage;
        [SerializeField] private Button buyButton;
        private bool Debugging => GameMaster.Instance.debugging;

        private IShopItem ShopItem { get; set; }

        public void Setup(IShopItem item)
        {
            ShopItem = item;
            titleText.text = item.DisplayName;
            levelText.text = $"Level {item.RequiredLevel}";
            if (descriptionText) descriptionText.text = item.Description;
            if (displayImage && item.DisplaySprite) displayImage.sprite = item.DisplaySprite;

            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(OnBuyPressed);
            buyButton.interactable = ShopManager.HasAccess(item);
        }

        private void OnBuyPressed()
        {
            if (!ShopManager.HasAccess(ShopItem))
            {
                if (Debugging)
                    Debug.Log($"[ShopObject] {ShopItem.DisplayName} requires level {ShopItem.RequiredLevel}.");
                return;
            }

            ShopItem.Purchase();
            if (ShopItem.RemoveAfterPurchase)
                ShopManager.Instance.RemoveShopItem(gameObject);
        }
    }
}
