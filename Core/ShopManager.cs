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

        [Header("Cesspit Item")] [SerializeField]
        private string cesspitDisplayName = "Cesspit";

        [SerializeField]
        private string cesspitDescription = "Stores overflow and leaks runaway issues toward the destination.";

        [SerializeField] private int cesspitRequiredLevel = 1;
        [SerializeField] private int cesspitInfraValue = 2;
        [SerializeField] private GameObject cesspitPrefab;
        [SerializeField] private Sprite cesspitSprite;

        [Header("Treatment Tank Item")] [SerializeField]
        private string treatmentTankDisplayName = "Treatment Tank";

        [SerializeField] private string treatmentTankDescription =
            "Captures issues and emits clean effluent. Locks up when full.";

        [SerializeField] private int treatmentTankRequiredLevel = 4;
        [SerializeField] private int treatmentTankInfraValue = 8;
        [SerializeField] private GameObject treatmentTankPrefab;
        [SerializeField] private Sprite treatmentTankSprite;
        [SerializeField] private int pipeInfraValue;

        [Header("Path Items")] [SerializeField]
        private string shortPipeDisplayName = "Short Pipe";

        [SerializeField] private string shortPipeDescription = "Straight pipe segment covering 2 cells.";
        [SerializeField] private int shortPipeRequiredLevel = 1;
        [SerializeField] private Sprite shortPipeSprite;
        [SerializeField] private string longPipeDisplayName = "Long Pipe";
        [SerializeField] private string longPipeDescription = "Straight pipe segment covering 3 cells.";
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
        private static bool Debugging => GameMaster.Instance.debugging;

        private static int CurrentLevel => GameMaster.Instance.turnController.currentLevel;

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
            GenerateShopInventory();
            GameMaster.Instance.interfaceManager.HideUIForShop();
            if (shopPanel) shopPanel.SetActive(true);
            if (Debugging) Debug.Log("[ShopManager] Shop opened.");
        }

        public void CloseShop()
        {
            if (shopPanel) shopPanel.SetActive(false);
            GameMaster.Instance.interfaceManager.RecoverUIForShop();
            if (Debugging) Debug.Log("[ShopManager] Shop closed.");
        }

        public void RemoveShopItem(GameObject shopItemGo)
        {
            if (shopItemGo) Destroy(shopItemGo);
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

            if (includeBlankTestItem && blankTestSprite)
                SpawnShopItem(new BlankShopItem(blankTestSprite));

            foreach (var item in CreateShopItems())
                SpawnShopItem(item);
        }

        private IEnumerable<IShopItem> CreateShopItems()
        {
            var fallbackPathSprite = shortPipeSprite ?? (longPipeSprite
                ? longPipeSprite
                : blankTestSprite);
            yield return new PathPieceShopItem(shortPipeDisplayName, shortPipeDescription, shortPipeRequiredLevel, 2,
                shortPipeSprite ?? fallbackPathSprite, pipeInfraValue);
            yield return new PathPieceShopItem(longPipeDisplayName, longPipeDescription, 1, 3,
                longPipeSprite ?? fallbackPathSprite, pipeInfraValue);
            yield return new PathBreakShopItem(breakPipeDisplayName, breakPipeDescription, breakPipeRequiredLevel,
                breakPipeSprite ?? fallbackPathSprite);

            if (towerPrefab)
                yield return new TowerShopItem(towerDisplayName, towerDescription, towerRequiredLevel, towerPrefab,
                    towerSprite, towerInfraValue);

            if (sifterPrefab)
                for (var i = 0; i < sifterCount; i++)
                    yield return new SifterShopItem(sifterDisplayName, sifterDescription, sifterRequiredLevel,
                        sifterPrefab, sifterSprite, sifterInfraValue);

            if (cesspitPrefab)
                yield return new CesspitShopItem(cesspitDisplayName, cesspitDescription, cesspitRequiredLevel,
                    cesspitPrefab, cesspitSprite, cesspitInfraValue);

            if (treatmentTankPrefab)
                yield return new TreatmentTankShopItem(treatmentTankDisplayName, treatmentTankDescription,
                    treatmentTankRequiredLevel, treatmentTankPrefab, treatmentTankSprite, treatmentTankInfraValue);

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
