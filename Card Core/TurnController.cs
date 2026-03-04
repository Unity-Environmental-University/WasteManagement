using System;
using System.Collections;
using UnityEngine;

namespace _project.Scripts.Card_Core
{
    public enum GameMode
    {
        Campaign,
        Endless
    }

    public class TurnController : MonoBehaviour
    {
        // private DeckManager _deckManager;
        // private ScoreManager _scoreManager;

        // private static readonly Queue<EffectRequest> EffectQueue = new();
        private static readonly object EffectQueueLock = new();

        [Header("Wave Settings")] public int turnsPerWave = 4;

        public int resourceGoal = 100;
        public GameMode currentGameMode = GameMode.Campaign;

        [Header("State")] public int currentTurn;

        public int totalTurns;
        public int currentWave;
        public bool canClickEnd;

        [Header("UI")] public GameObject lostScreen;

        public GameObject winScreen;

        [Header("Debug")] public bool debugging;

        private Coroutine _effectCoroutine;

        public Func<bool> readyToPlay;

        private static TurnController Instance { get; set; }

        private void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            canClickEnd = false;

            var master = GameMaster.Instance;
            if (!master) TryGetComponent(out master);
            if (!master) master = FindFirstObjectByType<GameMaster>(FindObjectsInactive.Include);

            // if (master)
            // {
            //     _deckManager = master.deckManager ? master.deckManager : master.GetComponent<DeckManager>();
            //     _scoreManager = master.scoreManager ? master.scoreManager : master.GetComponent<ScoreManager>();
            // }
            //
            // if (!_deckManager) TryGetComponent(out _deckManager);
            // if (!_scoreManager) TryGetComponent(out _scoreManager);
            //
            // if (!_deckManager || !_scoreManager)
            //     Debug.LogWarning("[TurnController] Missing deck or score manager references.");
        }

        private void Start()
        {
            if (readyToPlay != null)
                StartCoroutine(BeginWaveSequence());
        }

        private void Update()
        {
            var gm = GameMaster.Instance;
            if (!gm) return;

            // if (gm.turnText)
            //     gm.turnText.text = $"Turn: {currentTurn}";
            // if (gm.waveText)
            //     gm.waveText.text = $"Wave: {currentWave}";
            // if (gm.resourceText)
            //     gm.resourceText.text = $"Resources: {ScoreManager.GetResources()}";
        }

        private void OnDestroy()
        {
            Coroutine toStop;
            lock (EffectQueueLock)
            {
                toStop = _effectCoroutine;
                _effectCoroutine = null;
                // EffectQueue.Clear();
            }

            if (toStop != null) StopCoroutine(toStop);
        }

        // ReSharper disable once MemberCanBePrivate.Global
        public IEnumerator BeginWaveSequence()
        {
            yield return new WaitForEndOfFrame();

            if (readyToPlay != null)
                yield return new WaitUntil(readyToPlay);

            // if (!_deckManager || !_scoreManager)
            // {
            //     Debug.LogWarning("[TurnController] BeginWaveSequence aborted: missing references.");
            //     yield break;
            // }

            canClickEnd = false;
            currentTurn = 1;
            currentWave++;

            if (debugging) Debug.Log($"[TurnController] Wave {currentWave} starting.");

            yield return new WaitForSeconds(1f);

            // TODO: spawn wave enemies / setup board state here

            yield return new WaitForSeconds(0.5f);
            canClickEnd = true;
        }

        /// <summary>
        ///     Called when the player ends their turn (e.g. pressing an End Turn button).
        /// </summary>
        public void EndTurn()
        {
            if (!canClickEnd) return;
            canClickEnd = false;

            if (debugging) Debug.Log($"[TurnController] Ending turn {currentTurn}.");

            currentTurn++;
            totalTurns++;

            // if (currentTurn > turnsPerWave)
            // {
            //     StartCoroutine(EndWave());
            //     return;
            // }

            // Draw next hand for the following turn
            // _deckManager.DrawActionHand();
            canClickEnd = true;
        }

        // private IEnumerator EndWave(float delayTime = 1.5f)
        // {
        //     if (debugging) Debug.Log($"[TurnController] Wave {currentWave} ending.");
        //
        //     _deckManager.ClearActionHand();
        //     TryPlayQueuedEffects();
        //
        //     yield return new WaitForSeconds(delayTime);
        //
        //     var score = _scoreManager.CalculateScore();
        //
        //     if (debugging) Debug.Log($"[TurnController] Score after wave: {score}");
        //
        //     // TODO: check win/loss conditions
        //     if (ScoreManager.GetResources() >= resourceGoal)
        //     {
        //         WinGame();
        //         yield break;
        //     }
        //
        //     PrepareNextWave(score);
        // }

        private void PrepareNextWave(int score)
        {
            currentTurn = 0;

            if (debugging) Debug.Log($"[TurnController] Preparing next wave. Score: {score}");

            StartCoroutine(BeginWaveSequence());
        }

        private void WinGame()
        {
            canClickEnd = false;
            if (winScreen) winScreen.SetActive(true);
            else Debug.LogWarning("[TurnController] winScreen not set.");
        }

        private void GameLost()
        {
            canClickEnd = false;
            if (lostScreen) lostScreen.SetActive(true);
            else Debug.LogWarning("[TurnController] lostScreen not set.");
        }
    }
}