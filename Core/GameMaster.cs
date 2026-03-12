using System.Collections.Generic;
using _project.Scripts.UI;
using UnityEngine;

namespace _project.Scripts.Core
{
    /// <summary>
    ///     Central coordinator for the card game system. Manages integration between
    ///     deck, score, turn, and other core systems. Singleton.
    /// </summary>
    [RequireComponent(typeof(TurnController))]
    public class GameMaster : MonoBehaviour
    {
        [Header("Major Game Components")]
        public TurnController turnController;
        public TowerManager towerManager;
        public InterfaceManager interfaceManager;
        
        [Header("Debug")] public bool debugging;

        public CardController SelectedCard;
        

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

            var missing = new List<string>();
            if (!turnController) missing.Add(nameof(turnController));
            if (missing.Count > 0)
                Debug.LogWarning($"[CardGameMaster] Missing components: {string.Join(", ", missing)}");
        }
    }
}