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

            // Only show the icon when the item actually has art; otherwise an empty Image draws a
            // solid box on the plaque. Items without an icon fall back to a clean name-only tile.
            if (displayImage)
            {
                displayImage.sprite = item.DisplaySprite;
                displayImage.enabled = item.DisplaySprite;
            }

            // Tint the shop item's frame/background rather than its icon. Preserve the
            // complete Button color block because its transition also drives this graphic.
            _selectionGraphic = buyButton.targetGraphic ? buyButton.targetGraphic : GetComponent<Graphic>();
            if (_selectionGraphic) _defaultTint = _selectionGraphic.color;
            _defaultButtonColors = buyButton.colors;

            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(OnBuyPressed);
            buyButton.interactable = ShopManager.HasAccess(item);

            // Placeable tiles are persistent palette tools: bind for selection highlighting and
            // reflect whatever is currently armed. One-shot items (cards) need no selection state.
            var inventory = GameMaster.Instance ? GameMaster.Instance.placementInventory : null;
            if (!inventory || PlaceableItem == null) return;
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
                ArmOrDisarmPlaceable();
                return;
            }

            // One-shot, non-placeable items (cards) keep the original buy-and-remove behavior.
            ShopItem.Purchase();
            if (!ShopItem.RemoveAfterPurchase) return;
            ShopManager.Instance.MarkPurchased(ShopItem);
            ShopManager.Instance.RemoveShopItem(gameObject);
        }

        /// <summary>
        ///     Arms this tile's placeable as the persistent, infinite-supply tool, or disarms it when
        ///     it is already armed. The tile itself is never removed — it stays in the palette so the
        ///     player can place as many copies as their infrastructure budget allows.
        /// </summary>
        private void ArmOrDisarmPlaceable()
        {
            var inventory = GameMaster.Instance ? GameMaster.Instance.placementInventory : null;
            if (!inventory) return;

            BindInventory(inventory);

            if (inventory.SelectedItem == PlaceableItem)
            {
                inventory.ClearSelection();
                return;
            }

            inventory.SetActiveTool(PlaceableItem);
            GameMaster.Instance.pathBuildBoard?.ClearActivePiece();
        }

        private void BindInventory(PlacementInventory inventory)
        {
            if (_placementInventory) return;

            _placementInventory = inventory;
            _placementInventory.SelectionChanged += HandleSelectionChanged;
        }

        private void UnbindInventory()
        {
            if (_placementInventory == null) return;

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
    }
}
