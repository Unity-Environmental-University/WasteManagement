using _project.Scripts.UI;
using UnityEngine;

namespace _project.Scripts.Core
{
    public class ShopManager : MonoBehaviour
    {
        public static ShopManager Instance { get; private set; }
        private static bool Debugging => GameMaster.Instance.debugging;

        [Header("Shop UI")]
        [SerializeField] private GameObject shopPanel;
        [SerializeField] private Transform shopItemsParent;
        [SerializeField] private ShopObject shopItemPrefab;

        [Header("Tower Item")]
        [SerializeField] private string towerDisplayName = "Processing Tower";
        [SerializeField] private string towerDescription = "Intercepts issue objects on the pipeline.";
        [SerializeField] private int towerRequiredLevel = 1;
        [SerializeField] private GameObject towerPrefab;
        [SerializeField] private Sprite towerSprite;

        [Header("Sifter Item")]
        [SerializeField] private string sifterDisplayName = "Waste Sifter";
        [SerializeField] private string sifterDescription = "Filters the pipeline, reducing issue size.";
        [SerializeField] private int sifterRequiredLevel = 1;
        [SerializeField] private int sifterCount = 3;
        [SerializeField] private GameObject sifterPrefab;
        [SerializeField] private Sprite sifterSprite;

        [Header("Path Items")]
        [SerializeField] private string shortPipeDisplayName = "Short Pipe";
        [SerializeField] private string shortPipeDescription = "Straight pipe segment covering 2 cells.";
        [SerializeField] private int shortPipeRequiredLevel = 1;
        [SerializeField] private Sprite shortPipeSprite;
        [SerializeField] private string longPipeDisplayName = "Long Pipe";
        [SerializeField] private string longPipeDescription = "Straight pipe segment covering 3 cells.";
        [SerializeField] private Sprite longPipeSprite;

        [Header("Card Items")]
        [SerializeField] private CardShopEntry[] cardEntries;

        [Header("Testing")]
        [SerializeField] private bool includeBlankTestItem = true;
        [SerializeField] private Sprite blankTestSprite;

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

        private static int CurrentLevel => GameMaster.Instance.turnController.currentTurn + 1;

        public static bool HasAccess(IShopItem item)
        {
            return item != null && CurrentLevel >= item.RequiredLevel;
        }

        private void GenerateShopInventory()
        {
            ClearShop();

            if (includeBlankTestItem)
            {
                if (blankTestSprite)
                    SpawnShopItem(new BlankShopItem(blankTestSprite));
            }

            var fallbackPathSprite = shortPipeSprite != null
                ? shortPipeSprite
                : longPipeSprite != null
                    ? longPipeSprite
                    : blankTestSprite;
            SpawnShopItem(new PathPieceShopItem(shortPipeDisplayName, shortPipeDescription, shortPipeRequiredLevel, 2,
                shortPipeSprite != null ? shortPipeSprite : fallbackPathSprite));
            SpawnShopItem(new PathPieceShopItem(longPipeDisplayName, longPipeDescription, 1, 3,
                longPipeSprite != null ? longPipeSprite : fallbackPathSprite));

            if (towerPrefab)
                SpawnShopItem(new TowerShopItem(towerDisplayName, towerDescription, towerRequiredLevel, towerPrefab, towerSprite));

            if (sifterPrefab)
            {
                for (var i = 0; i < sifterCount; i++)
                    SpawnShopItem(new SifterShopItem(sifterDisplayName, sifterDescription, sifterRequiredLevel, sifterPrefab, sifterSprite));
            }

            foreach (var entry in cardEntries)
            {
                var card = CreateCard(entry.cardType);
                if (card != null) SpawnShopItem(new CardShopItem(card, entry.requiredLevel, entry.sprite));
            }
        }

        private void SpawnShopItem(IShopItem item)
        {
            if (!HasAccess(item)) return;
            if (!shopItemPrefab || !shopItemsParent) return;
            var ui = Instantiate(shopItemPrefab, shopItemsParent);
            ui.Setup(item);
        }

        private static ICard CreateCard(string cardType) => cardType switch
        {
            "ChemicalSolvent" => new ChemicalSolvent(),
            "UpgradedMeshNet" => new UpgradedMeshNet(),
            "SuperiorMaintenance" => new SuperiorMaintenance(),
            _ => null
        };

        private void ClearShop()
        {
            if (!shopItemsParent) return;
            for (var i = shopItemsParent.childCount - 1; i >= 0; i--)
                Destroy(shopItemsParent.GetChild(i).gameObject);
        }
    }
}
