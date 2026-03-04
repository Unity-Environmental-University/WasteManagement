using System.Collections.Generic;
using UnityEngine;

namespace _project.Scripts.Card_Core
{
    /// <summary>
    ///     Central coordinator for the card game system. Manages integration between
    ///     deck, score, turn, and other core systems. Singleton.
    /// </summary>
    //[RequireComponent(typeof(DeckManager))]
    //[RequireComponent(typeof(ScoreManager))]
    [RequireComponent(typeof(TurnController))]
    public class GameMaster : MonoBehaviour
    {
        [Header("Major Game Components")]
        //public DeckManager deckManager;
        // public ScoreManager scoreManager;
        public TurnController turnController;

        // [Header("UI Text")]
        // public TextMeshProUGUI resourceText;
        // public TextMeshProUGUI turnText;
        // public TextMeshProUGUI waveText;
        //

        [Header("Debug")] public bool debugging;

        public static GameMaster Instance { get; private set; }

        private void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // if (!scoreManager) scoreManager = GetComponent<ScoreManager>();
            // if (!deckManager) deckManager = GetComponent<DeckManager>();
            if (!turnController) turnController = GetComponent<TurnController>();

            var missing = new List<string>();
            // if (!scoreManager) missing.Add(nameof(scoreManager));
            // if (!deckManager) missing.Add(nameof(deckManager));
            if (!turnController) missing.Add(nameof(turnController));
            if (missing.Count > 0)
                Debug.LogWarning($"[CardGameMaster] Missing components: {string.Join(", ", missing)}");
        }
    }
}