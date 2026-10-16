using System.Collections.Generic;
using _project.Scripts.UI;
using UnityEngine;

namespace _project.Scripts.Core
{
    public class ShopManager : MonoBehaviour
    {
        [Header("Shop UI")] [SerializeField] private GameObject shopPanel;

        [SerializeField] private Transform shopItemsParent;
        [SerializeField] private ShopObject shopItemPrefab;

        [Header("Shop Item Effect")]
        [Tooltip("Enabled while a shop item is hovered or an item remains selected.")]


        [SerializeField]
        private GameObject shopItemHoverEffect;
        [SerializeField] private Transform shopItemHoverEffectParent;
        [SerializeField]
        private List<GameObject> shopHoverPrefabs = new List<GameObject>();
        [SerializeField] private DynamicSpriteSO shopBackgroundSprite;

        [Header("Tower Item")] [SerializeField]
        private string towerDisplayName = "Processing Tower";

        [SerializeField] private string towerDescription = "Intercepts issue objects on the pipeline.";
        [SerializeField] private int towerRequiredLevel = 1;
        [SerializeField] private int towerInfraValue = 2;
        [SerializeField] private GameObject towerPrefab;
        [SerializeField] private DynamicSpriteSO towerSprite;

        [Header("Sifter Item")] [SerializeField]
        private string sifterDisplayName = "Waste Sifter";

        [SerializeField] private string sifterDescription = "Filters the pipeline, reducing issue size.";
        [SerializeField] private int sifterRequiredLevel = 1;
        [SerializeField] private int sifterInfraValue = 1;
        [SerializeField] private GameObject sifterPrefab;
        [SerializeField] private DynamicSpriteSO sifterSprite;

        [Header("Path Splitter Item")] [SerializeField]
        private string pathSplitterDisplayName = "Splitter Box";

        [SerializeField]
        private string pathSplitterDescription = "Splits issues evenly between two connected path options.";

        [SerializeField] private int pathSplitterRequiredLevel = 1;
        [SerializeField] private int pathSplitterInfraValue = 1;
        [SerializeField] private GameObject pathSplitterPrefab;
        [SerializeField] private DynamicSpriteSO pathSplitterSprite;

        [Header("Cesspit Item")] [SerializeField]
        private string cesspitDisplayName = "Cesspit";

        [SerializeField]
        private string cesspitDescription = "Stores overflow and leaks runaway issues toward the destination.";

        [SerializeField] private int cesspitRequiredLevel = 1;
        [SerializeField] private int cesspitInfraValue = 2;
        [SerializeField] private GameObject cesspitPrefab;
        [SerializeField] private DynamicSpriteSO cesspitSprite;

        [Header("Cesspit Cap Item")] [SerializeField]
        private bool offerCesspitCap = true;

        [SerializeField] private string cesspitCapDisplayName = "Cesspit Cap";
        [SerializeField] private string cesspitCapDescription =
            "Select, then click a cesspit to seal it and permanently stop runaways.";
        [SerializeField] private int cesspitCapRequiredLevel = 1;
        [SerializeField] private DynamicSpriteSO cesspitCapSprite;

        [Header("Bury Cesspit Item")] [SerializeField]
        private bool offerBuryCesspit = true;

        [SerializeField] private string buryCesspitDisplayName = "Bury Cesspit";

        [SerializeField] private string buryCesspitDescription =
            "Select, then click a cesspit to demolish it, leaving a debuff tile on its cell.";

        [SerializeField] private int buryCesspitRequiredLevel = 3;
        [SerializeField] private DynamicSpriteSO buryCesspitSprite;

        [Header("Treatment Tank Item")] [SerializeField]
        private string treatmentTankDisplayName = "Treatment Tank";

        [SerializeField] private string treatmentTankDescription =
            "Captures issues and emits clean effluent. Locks up when full.";

        [SerializeField] private int treatmentTankRequiredLevel = 4;
        [SerializeField] private int treatmentTankInfraValue = 8;
        [SerializeField] private GameObject treatmentTankPrefab;
        [SerializeField] private DynamicSpriteSO treatmentTankSprite;

        [Header("Lime Sprinkler Item")] [SerializeField]
        private string limeSprinklerDisplayName = "Lime Sprinkler";

        [SerializeField] private string limeSprinklerDescription =
            "Sprinkles lime over the pipeline. (Effect coming soon.)";

        [SerializeField] private int limeSprinklerRequiredLevel = 1;
        [SerializeField] private int limeSprinklerInfraValue = 2;
        [SerializeField] private GameObject limeSprinklerPrefab;
        [SerializeField] private DynamicSpriteSO limeSprinklerSprite;

        [Header("Path Tools")] [SerializeField]
        private int pipeInfraValue;

        [SerializeField]
        private string shortPipeDisplayName = "Short Pipe";

        [SerializeField] private string shortPipeDescription = "Straight pipe segment covering 2 cells.";
        [SerializeField] private int shortPipeRequiredLevel = 1;
        [SerializeField] private Sprite shortPipeSprite;
        [SerializeField] private string longPipeDisplayName = "Long Pipe";
        [SerializeField] private string longPipeDescription = "Straight pipe segment covering 3 cells.";
        [SerializeField] private int longPipeRequiredLevel = 1;
        [SerializeField] private Sprite longPipeSprite;
        [SerializeField] private string breakPipeDisplayName = "Break Pipe";
        [SerializeField] private string breakPipeDescription = "Removes an existing pipe segment from the board.";
        [SerializeField] private int breakPipeRequiredLevel = 1;
        [SerializeField] private Sprite breakPipeSprite;

        [Header("Card Items")] [SerializeField]
        private CardShopEntry[] cardEntries;

        [Header("Testing")] [SerializeField] private bool includeBlankTestItem = true;

        [SerializeField] private Sprite blankTestSprite;
        public static ShopManager Instance { get; private set; }
        private static bool Debugging => GameMaster.Instance && GameMaster.Instance.debugging;
        
        // Stock is retained while the player opens and closes the shop during one round.
        // It is rebuilt at the start of the next round, so every round gets a fresh shop.
        private readonly List<IShopItem> _stockItems = new();
        private readonly HashSet<IShopItem> _purchasedItems = new();
        private bool _stockCreated;
        private ShopObject _hoveredShopObject;
        private IShopItem _hoveredShopItem;
        private IShopItem _selectedShopItem;

        private static int CurrentLevel =>
            GameMaster.Instance && GameMaster.Instance.turnController ? 
                GameMaster.Instance.turnController.currentLevel : 1;

        private static int CurrentEra =>
            GameMaster.Instance && GameMaster.Instance.popManager ?
                GameMaster.Instance.popManager.GetEra(CurrentLevel) : 1;

        public bool CanSelectConveyorTool => CurrentLevel >= 1;
        public bool CanSelectShortPipeTool => CurrentLevel >= shortPipeRequiredLevel;
        public bool CanSelectLongPipeTool => CurrentLevel >= longPipeRequiredLevel;
        public bool CanSelectBreakPipeTool => CurrentLevel >= breakPipeRequiredLevel;

        private void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (shopPanel) shopPanel.SetActive(false);
            if (shopItemHoverEffect) shopItemHoverEffect.SetActive(false);
        }

        public void OpenShop()
        {
            // ShopManager currently lives on the ShopUI root in the main scene. If that root
            // was saved inactive, the GameMaster can still call this serialized reference,
            // but activating only the child panel leaves the entire shop invisible.
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            GenerateShopInventory();
            if (shopPanel) shopPanel.SetActive(true);

            // Never hide the normal controls unless the replacement UI can actually be seen.
            // This avoids leaving the player with a blank UI when a parent canvas/container
            // is inactive or the panel reference is missing.
            if (!shopPanel || !shopPanel.activeInHierarchy)
            {
                Debug.LogWarning("[ShopManager] Shop panel is not visible; keeping the regular UI active.", this);
                return;
            }

            GameMaster.Instance?.interfaceManager?.HideUIForShop();
            if (Debugging) Debug.Log("[ShopManager] Shop opened.");
        }

        /// <summary>
        ///     Discards the current round's shop stock. Call this only at a round boundary;
        ///     closing and reopening the shop within the same round must preserve purchases.
        /// </summary>
        public void ResetStockForNewRound()
        {
            _stockItems.Clear();
            _purchasedItems.Clear();
            _stockCreated = false;
        }

        public void CloseShop()
        {
            if (shopPanel) shopPanel.SetActive(false);
            _hoveredShopObject = null;
            _hoveredShopItem = null;
            _selectedShopItem = null;
            RefreshShopItemHoverEffect();
            GameMaster.Instance?.interfaceManager?.RecoverUIForShop();
            if (Debugging) Debug.Log("[ShopManager] Shop closed.");
        }

        public void SetHoveredShopObject(ShopObject shopObject, IShopItem item)
        {
            _hoveredShopObject = shopObject;
            _hoveredShopItem = item;
            RefreshShopItemHoverEffect();
        }

        public void ClearHoveredShopObject(ShopObject shopObject)
        {
            if (_hoveredShopObject != shopObject) return;

            _hoveredShopObject = null;
            _hoveredShopItem = null;
            RefreshShopItemHoverEffect();
        }

        public void SetSelectedShopItem(IShopItem item)
        {
            _selectedShopItem = item;
            RefreshShopItemHoverEffect();
        }

        private void RefreshShopItemHoverEffect()
        {
            if (shopItemHoverEffect)
                shopItemHoverEffect.SetActive(_hoveredShopObject || _selectedShopItem != null);

            var itemToDisplay = _hoveredShopItem ?? _selectedShopItem;
            if (shopItemHoverEffect && itemToDisplay != null &&
                shopItemHoverEffect.TryGetComponent<ItemDescription>(out var itemDescription))
                itemDescription.SetItemDescription(itemToDisplay.DisplayName, itemToDisplay.Description);
        }

        public void RemoveShopItem(GameObject shopItemGo)
        {
            if (shopItemGo) Destroy(shopItemGo);
        }

        /// <summary>Marks a finite-stock item as sold for the remainder of the current round.</summary>
        public void MarkPurchased(IShopItem item)
        {
            if (item is { RemoveAfterPurchase: true })
                _purchasedItems.Add(item);
        }

        public void SelectShortPipeTool()
        {
            SelectPathPieceTool(shortPipeDisplayName, shortPipeDescription, shortPipeRequiredLevel, 2, shortPipeSprite);
        }

        public void SelectLongPipeTool()
        {
            SelectPathPieceTool(longPipeDisplayName, longPipeDescription, longPipeRequiredLevel, 3, longPipeSprite);
        }

        public void SelectConveyorTool()
        {
            SelectPathPieceTool("Recycling Conveyor", "Two-cell recycling conveyor. R rotates.", 1, 2,
                shortPipeSprite, _project.Scripts.Core.PathKind.RecyclingBelt);
        }

        public void SelectBreakConveyorTool()
        {
            if (!HasPathToolAccess("Remove Conveyor", "Removes a conveyor segment.", 1)) return;
            var gm = GameMaster.Instance;
            gm.placementInventory?.ClearSelection();
            gm.pathBuildBoard?.SetActiveBreakTool(PathKind.RecyclingBelt);
        }

        public void SelectBreakPipeTool()
        {
            if (!HasPathToolAccess(breakPipeDisplayName, breakPipeDescription, breakPipeRequiredLevel)) return;

            var gm = GameMaster.Instance;
            var board = gm ? gm.pathBuildBoard : null;
            if (!board)
            {
                Debug.LogWarning("[ShopManager] No PathBuildBoard available; break tool ignored.");
                return;
            }

            gm.placementInventory?.ClearSelection();
            board.SetActiveBreakTool();
        }

        public void ClearPathTool()
        {
            GameMaster.Instance?.pathBuildBoard?.ClearActivePiece();
        }

        public void SelectPanTool()
        {
            var gm = GameMaster.Instance;
            if (!gm) return;

            // Drop any queued placement so a pan-drag can't land it on a slot.
            gm.placementInventory?.ClearSelection();
            gm.pathBuildBoard?.SetActivePanTool();
        }

        public static bool HasAccess(IShopItem item)
        {
            return item != null && CurrentLevel >= item.RequiredLevel;
        }

        public IReadOnlyList<string> GetUnlockNamesForLevelRange(int previousLevel, int currentLevel)
        {
            var unlocked = new List<string>();
            if (currentLevel <= previousLevel) return unlocked;

            var seenNames = new HashSet<string>();
            foreach (var item in CreateShopItems(1))
                AddUnlock(unlocked, seenNames, item, previousLevel, currentLevel);

            return unlocked;
        }

        private static void AddUnlock(ICollection<string> unlocked, ISet<string> seenNames, IShopItem item,
            int previousLevel, int currentLevel)
        {
            if (item == null) return;

            var displayName = item.DisplayName;
            if (string.IsNullOrWhiteSpace(displayName)) return;
            if (item.RequiredLevel <= previousLevel || item.RequiredLevel > currentLevel) return;
            if (!seenNames.Add(displayName)) return;

            unlocked.Add(displayName);
        }

        private void GenerateShopInventory()
        {
            ClearShop();

            EnsureStockCreated();
            foreach (var item in _stockItems)
            {
                // Placeable tools are persistent (infinite supply) and are never marked purchased.
                // Only one-shot items (cards) enter _purchasedItems and drop out of the palette
                // for the rest of the round.
                if (_purchasedItems.Contains(item)) continue;

                SpawnShopItem(item);
            }
        }

        private void EnsureStockCreated()
        {
            if (_stockCreated) return;

            if (includeBlankTestItem && blankTestSprite)
                _stockItems.Add(new BlankShopItem(blankTestSprite));

            _stockItems.AddRange(CreateShopItems(CurrentEra));
            ApplyEraVisuals(CurrentEra);
            _stockCreated = true;
        }

        private IEnumerable<IShopItem> CreateShopItems(int era)
        {
            if (towerPrefab)
                yield return new TowerShopItem(towerDisplayName, towerDescription, towerRequiredLevel, towerPrefab,
                    towerSprite.GetSpriteForEra(era), towerInfraValue);

            // Persistent, infinite-supply tools: one palette tile each. Supply is no longer gated by
            // a per-round stock count; the infrastructure budget spent per placement is the limiter.
            if (sifterPrefab)
                yield return new SifterShopItem(sifterDisplayName, sifterDescription, sifterRequiredLevel,
                    sifterPrefab, sifterSprite.GetSpriteForEra(era), sifterInfraValue);

            if (pathSplitterPrefab)
                yield return new PathSplitterShopItem(pathSplitterDisplayName, pathSplitterDescription,
                    pathSplitterRequiredLevel, pathSplitterPrefab, pathSplitterSprite.GetSpriteForEra(era), pathSplitterInfraValue);

            if (cesspitPrefab)
                yield return new CesspitShopItem(cesspitDisplayName, cesspitDescription, cesspitRequiredLevel,
                    cesspitPrefab, cesspitSprite.GetSpriteForEra(era), cesspitInfraValue);

            if (offerCesspitCap)
                yield return new CesspitCapShopItem(cesspitCapDisplayName, cesspitCapDescription,
                    cesspitCapRequiredLevel, cesspitCapSprite ? cesspitCapSprite.GetSpriteForEra(era) : null);

            if (offerBuryCesspit)
                yield return new BuryCesspitShopItem(buryCesspitDisplayName, buryCesspitDescription,
                    buryCesspitRequiredLevel, buryCesspitSprite ? buryCesspitSprite.GetSpriteForEra(era) : null);

            if (treatmentTankPrefab)
                yield return new TreatmentTankShopItem(treatmentTankDisplayName, treatmentTankDescription,
                    treatmentTankRequiredLevel, treatmentTankPrefab, treatmentTankSprite.GetSpriteForEra(era), treatmentTankInfraValue);

            if (limeSprinklerPrefab)
                yield return new LimeSprinklerShopItem(limeSprinklerDisplayName, limeSprinklerDescription,
                    limeSprinklerRequiredLevel, limeSprinklerPrefab, limeSprinklerSprite.GetSpriteForEra(era), limeSprinklerInfraValue);

            if (cardEntries == null) yield break;

            foreach (var entry in cardEntries)
            {
                var card = CreateCard(entry.cardType);
                if (card != null)
                    yield return new CardShopItem(card, entry.requiredLevel, entry.sprite, entry.infraValue);
            }
        }

        // Update the shop background and description panel based on the era
        private void ApplyEraVisuals(int era)
        {
            if (shopPanel && shopBackgroundSprite)
            {
                shopPanel.GetComponent<UnityEngine.UI.Image>().sprite = shopBackgroundSprite.GetSpriteForEra(era);
                shopPanel.transform.GetChild(0).GetComponent<UnityEngine.UI.Image>().sprite = shopBackgroundSprite.GetSpriteForEra(era);
            }

            if (era > shopHoverPrefabs.Count || !shopHoverPrefabs[era - 1]) return;

            var oldHoverPrefab = shopItemHoverEffect;
            shopItemHoverEffect = Instantiate(shopHoverPrefabs[era - 1], shopItemHoverEffectParent);
            if (oldHoverPrefab != null)
            {
                CopyRectTransformValues(oldHoverPrefab.GetComponent<RectTransform>(),
                    shopItemHoverEffect.GetComponent<RectTransform>());
                Destroy(oldHoverPrefab);
            }

            // Hide the new panel until an item is hovered or selected
            RefreshShopItemHoverEffect();
        }

        private void SpawnShopItem(IShopItem item)
        {
            if (!HasAccess(item)) return;
            if (!shopItemPrefab || !shopItemsParent) return;
            var ui = Instantiate(shopItemPrefab, shopItemsParent);
            ui.Setup(item, CurrentEra);
        }

        private void SelectPathPieceTool(string displayName, string description, int requiredLevel, int length,
            Sprite displaySprite, PathKind pathKind = PathKind.Pipe)
        {
            if (!HasPathToolAccess(displayName, description, requiredLevel)) return;

            var gm = GameMaster.Instance;
            var board = gm ? gm.pathBuildBoard : null;
            if (!board)
            {
                Debug.LogWarning("[ShopManager] No PathBuildBoard available; path tool ignored.");
                return;
            }

            gm.placementInventory?.ClearSelection();
            board.SetActivePiece(new PathPiecePlaceable(displayName, description, requiredLevel, length, displaySprite,
                pipeInfraValue, pathKind));
        }

        private static bool HasPathToolAccess(string displayName, string description, int requiredLevel)
        {
            if (CurrentLevel >= requiredLevel) return true;

            if (Debugging)
                Debug.Log($"[ShopManager] {displayName} requires level {requiredLevel}. {description}");
            return false;
        }

        private static ICard CreateCard(string cardType)
        {
            return cardType switch
            {
                "ChemicalSolvent" => new ChemicalSolvent(),
                "UpgradedMeshNet" => new UpgradedMeshNet(),
                "SuperiorMaintenance" => new SuperiorMaintenance(),
                _ => null
            };
        }

        private void ClearShop()
        {
            if (!shopItemsParent) return;
            for (var i = shopItemsParent.childCount - 1; i >= 0; i--)
                Destroy(shopItemsParent.GetChild(i).gameObject);
        }

        private static void CopyRectTransformValues(RectTransform source, RectTransform target)
        {
            if (source is null || target is null) return;

            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
            
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
        }
    }
}
