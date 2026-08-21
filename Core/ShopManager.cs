using System.Collections.Generic;
using System.Linq;
using _project.Scripts.UI;
using UnityEngine;

namespace _project.Scripts.Core
{
    public class ShopManager : MonoBehaviour
    {
        [Header("Shop UI")] [SerializeField] private GameObject shopPanel;

        [SerializeField] private Transform shopItemsParent;
        [SerializeField] private ShopObject shopItemPrefab;

        [Header("Tower Item")] [SerializeField]
        private string towerDisplayName = "Processing Tower";

        [SerializeField] private string towerDescription = "Intercepts issue objects on the pipeline.";
        [SerializeField] private int towerRequiredLevel = 1;
        [SerializeField] private int towerInfraValue = 2;
        [SerializeField] private GameObject towerPrefab;
        [SerializeField] private Sprite towerSprite;

        [Header("Sifter Item")] [SerializeField]
        private string sifterDisplayName = "Waste Sifter";

        [SerializeField] private string sifterDescription = "Filters the pipeline, reducing issue size.";
        [SerializeField] private int sifterRequiredLevel = 1;
        [SerializeField] private int sifterInfraValue = 1;
        [SerializeField] private int sifterCount = 3;
        [SerializeField] private GameObject sifterPrefab;
        [SerializeField] private Sprite sifterSprite;

        [Header("Path Splitter Item")] [SerializeField]
        private string pathSplitterDisplayName = "Splitter Box";

        [SerializeField]
        private string pathSplitterDescription = "Splits issues evenly between two connected path options.";

        [SerializeField] private int pathSplitterRequiredLevel = 1;
        [SerializeField] private int pathSplitterInfraValue = 1;
        [SerializeField] private int pathSplitterCount = 1;
        [SerializeField] private GameObject pathSplitterPrefab;
        [SerializeField] private Sprite pathSplitterSprite;

        [Header("Cesspit Item")] [SerializeField]
        private string cesspitDisplayName = "Cesspit";

        [SerializeField]
        private string cesspitDescription = "Stores overflow and leaks runaway issues toward the destination.";

        [SerializeField] private int cesspitRequiredLevel = 1;
        [SerializeField] private int cesspitInfraValue = 2;
        [SerializeField] private GameObject cesspitPrefab;
        [SerializeField] private Sprite cesspitSprite;

        [Header("Cesspit Cap Item")] [SerializeField]
        private bool offerCesspitCap = true;

        [SerializeField] private string cesspitCapDisplayName = "Cesspit Cap";
        [SerializeField] private string cesspitCapDescription =
            "Select, then click a cesspit to seal it and permanently stop runaways.";
        [SerializeField] private int cesspitCapRequiredLevel = 1;
        [SerializeField] private Sprite cesspitCapSprite;

        [Header("Bury Cesspit Item")] [SerializeField]
        private bool offerBuryCesspit = true;

        [SerializeField] private string buryCesspitDisplayName = "Bury Cesspit";

        [SerializeField] private string buryCesspitDescription =
            "Select, then click a cesspit to demolish it, leaving a debuff tile on its cell.";

        [SerializeField] private int buryCesspitRequiredLevel = 3;
        [SerializeField] private Sprite buryCesspitSprite;

        [Header("Treatment Tank Item")] [SerializeField]
        private string treatmentTankDisplayName = "Treatment Tank";

        [SerializeField] private string treatmentTankDescription =
            "Captures issues and emits clean effluent. Locks up when full.";

        [SerializeField] private int treatmentTankRequiredLevel = 4;
        [SerializeField] private int treatmentTankInfraValue = 8;
        [SerializeField] private GameObject treatmentTankPrefab;
        [SerializeField] private Sprite treatmentTankSprite;

        [Header("Lime Sprinkler Item")] [SerializeField]
        private string limeSprinklerDisplayName = "Lime Sprinkler";

        [SerializeField] private string limeSprinklerDescription =
            "Sprinkles lime over the pipeline. (Effect coming soon.)";

        [SerializeField] private int limeSprinklerRequiredLevel = 1;
        [SerializeField] private int limeSprinklerInfraValue = 2;
        [SerializeField] private GameObject limeSprinklerPrefab;
        [SerializeField] private Sprite limeSprinklerSprite;

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

        private static int CurrentLevel =>
            GameMaster.Instance && GameMaster.Instance.turnController ? 
                GameMaster.Instance.turnController.currentLevel : 1;

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
            GameMaster.Instance?.interfaceManager?.RecoverUIForShop();
            if (Debugging) Debug.Log("[ShopManager] Shop closed.");
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

        public static bool HasAccess(IShopItem item)
        {
            return item != null && CurrentLevel >= item.RequiredLevel;
        }

        public IReadOnlyList<string> GetUnlockNamesForLevelRange(int previousLevel, int currentLevel)
        {
            var unlocked = new List<string>();
            if (currentLevel <= previousLevel) return unlocked;

            var seenNames = new HashSet<string>();
            foreach (var item in CreateShopItems())
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
            var queued = CollectQueuedPlaceables();

            EnsureStockCreated();
            foreach (var item in _stockItems)
            {
                // Purchased placeable stays visible only while it is waiting to be placed,
                // allowing the player to reselect it after closing the shop. Once consumed,
                // it is no longer part of the shop's stock.
                if (_purchasedItems.Contains(item) && (item is not IPlaceable placeable || !queued.Contains(placeable)))
                    continue;

                SpawnShopItem(item);
            }
        }

        private void EnsureStockCreated()
        {
            if (_stockCreated) return;

            if (includeBlankTestItem && blankTestSprite)
                _stockItems.Add(new BlankShopItem(blankTestSprite));

            _stockItems.AddRange(CreateShopItems());
            _stockCreated = true;
        }

        private static List<IPlaceable> CollectQueuedPlaceables()
        {
            var queued = new List<IPlaceable>();
            var inventory = GameMaster.Instance ? GameMaster.Instance.placementInventory : null;
            if (!inventory) return queued;

            queued.AddRange(inventory.Items.Where(item => item != null));

            return queued;
        }

        private IEnumerable<IShopItem> CreateShopItems()
        {
            if (towerPrefab)
                yield return new TowerShopItem(towerDisplayName, towerDescription, towerRequiredLevel, towerPrefab,
                    towerSprite, towerInfraValue);

            if (sifterPrefab)
                for (var i = 0; i < sifterCount; i++)
                    yield return new SifterShopItem(sifterDisplayName, sifterDescription, sifterRequiredLevel,
                        sifterPrefab, sifterSprite, sifterInfraValue);

            if (pathSplitterPrefab)
                for (var i = 0; i < pathSplitterCount; i++)
                    yield return new PathSplitterShopItem(pathSplitterDisplayName, pathSplitterDescription,
                        pathSplitterRequiredLevel, pathSplitterPrefab, pathSplitterSprite, pathSplitterInfraValue);

            if (cesspitPrefab)
                yield return new CesspitShopItem(cesspitDisplayName, cesspitDescription, cesspitRequiredLevel,
                    cesspitPrefab, cesspitSprite, cesspitInfraValue);

            if (offerCesspitCap)
                yield return new CesspitCapShopItem(cesspitCapDisplayName, cesspitCapDescription,
                    cesspitCapRequiredLevel, cesspitCapSprite);

            if (offerBuryCesspit)
                yield return new BuryCesspitShopItem(buryCesspitDisplayName, buryCesspitDescription,
                    buryCesspitRequiredLevel, buryCesspitSprite);

            if (treatmentTankPrefab)
                yield return new TreatmentTankShopItem(treatmentTankDisplayName, treatmentTankDescription,
                    treatmentTankRequiredLevel, treatmentTankPrefab, treatmentTankSprite, treatmentTankInfraValue);

            if (limeSprinklerPrefab)
                yield return new LimeSprinklerShopItem(limeSprinklerDisplayName, limeSprinklerDescription,
                    limeSprinklerRequiredLevel, limeSprinklerPrefab, limeSprinklerSprite, limeSprinklerInfraValue);

            if (cardEntries == null) yield break;

            foreach (var entry in cardEntries)
            {
                var card = CreateCard(entry.cardType);
                if (card != null)
                    yield return new CardShopItem(card, entry.requiredLevel, entry.sprite, entry.infraValue);
            }
        }

        private void SpawnShopItem(IShopItem item)
        {
            if (!HasAccess(item)) return;
            if (!shopItemPrefab || !shopItemsParent) return;
            var ui = Instantiate(shopItemPrefab, shopItemsParent);
            ui.Setup(item);
        }

        private void SelectPathPieceTool(string displayName, string description, int requiredLevel, int length,
            Sprite displaySprite)
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
                pipeInfraValue));
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
    }
}
