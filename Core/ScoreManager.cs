using TMPro;
using UnityEngine;

namespace _project.Scripts.Core
{
    public class ScoreManager : MonoBehaviour
    {
        private static ScoreManager Instance { get; set; }
        private static bool Debugging => GameMaster.Instance.debugging;

        [Header("Currency")]
        [SerializeField] private int startingTokens = 10;
        [field: SerializeField] public int TokensPerWave { get; private set; } = 5;

        [Header("UI")]
        [SerializeField] private TextMeshProUGUI tokensText;

        private static int _tokens;
        public static int Tokens => _tokens;

        private void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public void Initialize()
        {
            _tokens = startingTokens;
            UpdateDisplay();
        }

        public static void AddTokens(int amount)
        {
            _tokens += amount;
            Instance.UpdateDisplay();
            if (Debugging) Debug.Log($"[ScoreManager] +{amount} tokens → {_tokens}");
        }

        public static void SpendTokens(int amount)
        {
            _tokens -= amount;
            Instance.UpdateDisplay();
            if (Debugging) Debug.Log($"[ScoreManager] -{amount} tokens → {_tokens}");
        }

        public static bool CanAfford(int cost) => _tokens >= cost;

        private void UpdateDisplay()
        {
            if (tokensText) tokensText.text = $"Tokens: {_tokens}";
        }
    }
}
