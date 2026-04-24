using System.Collections.Generic;
using _project.Scripts.Object_Scripts;
using _project.Scripts.UI;
using UnityEngine;

namespace _project.Scripts.Core
{
    /// <summary>
    ///     Central coordinator for the card game system. Manages integration between
    ///     deck, score, turn, and other core systems. Singleton.
    /// </summary>
    [RequireComponent(typeof(TurnController), typeof(PlacementInventory))]
    public class GameMaster : MonoBehaviour
    {
        [Header("Major Game Components")] 
        public Camera mainCamera;
        public Camera topDownCamera;
        public TurnController turnController;
        public TowerManager towerManager;
        public InterfaceManager interfaceManager;
        public DeckManager deckManager;
        public PlacementInventory placementInventory;
        public PipelineComponentManager pipCompMan;
        public ShopManager shopManager;
        public PathBuildBoard pathBuildBoard;

        [Header("Debug")] public bool debugging;

        public CardController selectedCard;
        public List<EntitySpawner> entitySpawners;
        public IPlaceable PendingPlacement => placementInventory ? placementInventory.SelectedItem : null;
        

        public static GameMaster Instance { get; private set; }

        private void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (!turnController) turnController = GetComponent<TurnController>();
            if (!towerManager) towerManager = GetComponent<TowerManager>();
            if (!interfaceManager) interfaceManager = GetComponentInChildren<InterfaceManager>();
            if (!deckManager) deckManager = GetComponentInChildren<DeckManager>();
            if (!placementInventory) placementInventory = GetComponent<PlacementInventory>();
            if (!placementInventory) placementInventory = gameObject.AddComponent<PlacementInventory>();
            if (!shopManager) shopManager = GetComponentInChildren<ShopManager>();

            var missing = new List<string>();
            if (!turnController) missing.Add(nameof(turnController));
            if (!towerManager) missing.Add(nameof(towerManager));
            if (!interfaceManager) missing.Add(nameof(interfaceManager));
            if (!deckManager) missing.Add(nameof(deckManager));
            if (!shopManager) missing.Add(nameof(shopManager));

            if (missing.Count > 0)
                Debug.LogWarning($"[CardGameMaster] Missing components: {string.Join(", ", missing)}");
        }
    }
}
