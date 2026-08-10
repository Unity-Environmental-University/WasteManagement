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

        [Tooltip("Tint applied to the item box while it is the pending placement selection.")]
        [SerializeField] private Color selectedTint = new(1f, 0.62f, 0.18f, 1f);

        private PlacementInventory _placementInventory;
        private Graphic _selectionGraphic;
        private Color _defaultTint;
        private ColorBlock _defaultButtonColors;
        private bool _isSelected;
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

            // Tint the shop item's frame/background rather than its icon. Preserve the
            // complete Button color block because its transition also drives this graphic.
            _selectionGraphic = buyButton.targetGraphic ? buyButton.targetGraphic : GetComponent<Graphic>();
            if (_selectionGraphic) _defaultTint = _selectionGraphic.color;
            _defaultButtonColors = buyButton.colors;

            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(OnBuyPressed);
            buyButton.interactable = ShopManager.HasAccess(item);

            // If this UI was generated for an already-queued item (reused on shop reopen),
            // bind now so it removes itself when the item is placed, even before any buy press.
            var inventory = GameMaster.Instance ? GameMaster.Instance.placementInventory : null;
            if (!inventory || PlaceableItem == null || !inventory.Contains(PlaceableItem)) return;
            BindInventory(inventory);
            HandleSelectionChanged(inventory.SelectedItem);
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
            _placementInventory.SelectionChanged += HandleSelectionChanged;
        }

        private void UnbindInventory()
        {
            if (_placementInventory == null) return;

            _placementInventory.InventoryChanged -= HandleInventoryChanged;
            _placementInventory.SelectionChanged -= HandleSelectionChanged;
            _placementInventory = null;
        }

        /// <summary>Orange box while this item is the pending selection, so the player knows what they are placing.</summary>
        private void HandleSelectionChanged(IPlaceable selected)
        {
            var isSelected = selected != null && selected == PlaceableItem;
            if (isSelected == _isSelected) return;
            _isSelected = isSelected;

            var colors = _defaultButtonColors;
            if (isSelected)
            {
                colors.normalColor = selectedTint;
                colors.highlightedColor = selectedTint;
                colors.pressedColor = selectedTint;
                colors.selectedColor = selectedTint;
            }

            // Button ColorTint multiplies its transition color by the Graphic's base
            // color. Make the base RGB neutral while selected so orange is not mixed
            // with blue, then restore the original blue base when deselected.
            if (_selectionGraphic)
                _selectionGraphic.color = isSelected
                    ? new Color(1f, 1f, 1f, _defaultTint.a)
                    : _defaultTint;
            buyButton.colors = colors;
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
