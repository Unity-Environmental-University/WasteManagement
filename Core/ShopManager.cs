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
        [SerializeField] private int towerCost = 15;
        [SerializeField] private GameObject towerPrefab;
        [SerializeField] private Sprite towerSprite;

        [Header("Sifter Item")]
        [SerializeField] private string sifterDisplayName = "Waste Sifter";
        [SerializeField] private string sifterDescription = "Filters the pipeline, reducing issue size.";
        [SerializeField] private int sifterCost = 8;
        [SerializeField] private GameObject sifterPrefab;
        [SerializeField] private Sprite sifterSprite;

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

        private void GenerateShopInventory()
        {
            ClearShop();

            if (includeBlankTestItem)
                SpawnShopItem(new BlankShopItem(blankTestSprite));

            if (towerPrefab)
                SpawnShopItem(new TowerShopItem(towerDisplayName, towerDescription, towerCost, towerPrefab, towerSprite));

            if (sifterPrefab)
                SpawnShopItem(new SifterShopItem(sifterDisplayName, sifterDescription, sifterCost, sifterPrefab, sifterSprite));

            foreach (var entry in cardEntries)
            {
                var card = CreateCard(entry.cardType);
                if (card != null) SpawnShopItem(new CardShopItem(card, entry.cost, entry.sprite));
            }
        }

        private void SpawnShopItem(IShopItem item)
        {
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
