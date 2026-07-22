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
        private PlacementInventory _placementInventory;
        private bool Debugging => GameMaster.Instance.debugging;

        private IShopItem ShopItem { get; set; }

        private IPlaceable PlaceableItem => ShopItem as IPlaceable;

        private void OnDestroy()
        {
            UnbindInventory();
        }

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

            // If this UI was generated for an already-queued item (reused on shop reopen),
            // bind now so it removes itself when the item is placed, even before any buy press.
            var inventory = GameMaster.Instance ? GameMaster.Instance.placementInventory : null;
            if (inventory && PlaceableItem != null && inventory.Contains(PlaceableItem))
                BindInventory(inventory);
        }

        private void OnBuyPressed()
        {
            if (!ShopManager.HasAccess(ShopItem))
            {
                if (Debugging)
                    Debug.Log($"[ShopObject] {ShopItem.DisplayName} requires level {ShopItem.RequiredLevel}.");
                return;
            }

            if (PlaceableItem != null)
            {
                QueueOrSelectPlaceable();
                return;
            }

            ShopItem.Purchase();
            if (!ShopItem.RemoveAfterPurchase) return;
            ShopManager.Instance.MarkPurchased(ShopItem);
            ShopManager.Instance.RemoveShopItem(gameObject);
        }

        private void QueueOrSelectPlaceable()
        {
            var inventory = GameMaster.Instance ? GameMaster.Instance.placementInventory : null;
            if (!inventory) return;

            BindInventory(inventory);

            if (inventory.SelectItem(PlaceableItem))
            {
                GameMaster.Instance.pathBuildBoard?.ClearActivePiece();
                return;
            }

            ShopItem.Purchase();
            ShopManager.Instance.MarkPurchased(ShopItem);
            inventory.SelectItem(PlaceableItem);
            GameMaster.Instance.pathBuildBoard?.ClearActivePiece();
        }

        private void BindInventory(PlacementInventory inventory)
        {
            if (_placementInventory) return;

            _placementInventory = inventory;
            _placementInventory.InventoryChanged += HandleInventoryChanged;
        }

        private void UnbindInventory()
        {
            if (_placementInventory == null) return;

            _placementInventory.InventoryChanged -= HandleInventoryChanged;
            _placementInventory = null;
        }

        private void HandleInventoryChanged()
        {
            if (PlaceableItem == null || _placementInventory == null) return;
            if (_placementInventory.Contains(PlaceableItem)) return;

            if (ShopManager.Instance)
                ShopManager.Instance.RemoveShopItem(gameObject);
        }
    }
}
