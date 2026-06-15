using System;
using System.Collections;
using System.Linq;
using _project.Scripts.Object_Scripts;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace _project.Scripts.Core
{
    public enum GamePhase
    {
        Card,
        Tower
    }

    public class TurnController : MonoBehaviour
    {
        [Header("State")] public int currentTurn;
        [Header("State")] public int currentLevel;
        [Header("State")] public GamePhase currentPhase;
        [Header("State")] public int moveCount;
        [Header("State")] public int infrastructureValue;

        public float waveDuration = 30;
        private GameMaster _gm = GameMaster.Instance;
        private bool _waitingForPostRoundContinue;
        private static TurnController Instance { get; set; }

        private void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            if (!_gm) _gm = GameMaster.Instance;
            GameStartSequence();
        }

        public static event Action OnCardPhaseEntered;
        public static event Action OnTowerPhaseEntered;

        /// <summary>
        ///     This should initialize game variables and set up the game state for a new game/run.
        /// </summary>
        private void GameStartSequence()
        {
            if (_gm.debugging) Debug.Log("Game Sequence Started!");
            currentTurn = 0;
            currentLevel = _gm.popManager ? _gm.popManager.GetLevelByPopulationSize() : 1;
            moveCount = 0;
            EnterCardSequence();
        }

        /// <summary>
        ///     Records a player placement as a move. Increments the persistent run counter.
        /// </summary>
        public void RegisterMove()
        {
            moveCount++;
            if (_gm.debugging) Debug.Log($"[TurnController] Move registered (total: {moveCount}).");
            RefreshInfoBar();
        }

        /// <summary>
        ///     Pushes the current move count, population, and level to the info bar UI.
        /// </summary>
        private void RefreshInfoBar()
        {
            var populationSize = _gm.popManager ? _gm.popManager.GetPopulationSize() : 0;
            _gm.interfaceManager?.UpdateInfoBar(moveCount, populationSize, currentLevel);
        }

        private void EnterCardSequence()
        {
            currentPhase = GamePhase.Card;
            OnCardPhaseEntered?.Invoke();
            SwitchCamera();
            _gm.placementInventory?.SelectFirstAvailable();
            _gm.deckManager?.DrawNewHand();
            // TODO: re-enable hand UI when card system is back in use
            // _gm.interfaceManager.PopulateHand(_gm.deckManager.Hand);
            _gm.interfaceManager?.ShowPrepUI();
            RefreshInfoBar();

            if (_gm.shopManager) _gm.shopManager.OpenShop();

            if (_gm.debugging) Debug.Log($"[TurnController] Card phase — turn {currentTurn}, level {currentLevel}");
        }

        private void SwitchCamera()
        {
            var top = _gm.topDownCamera;
            var main = _gm.mainCamera;

            if (main.isActiveAndEnabled)
            {
                main.gameObject.SetActive(false);
                top.gameObject.SetActive(true);
            }
            else if (top.isActiveAndEnabled)
            {
                top.gameObject.SetActive(false);
                main.gameObject.SetActive(true);
            }
        }

        public void EndPhase()
        {
            switch (currentPhase)
            {
                case GamePhase.Card:
                    if (!CanBeginWave()) return;
                    BeginWaveSequence();
                    break;
                case GamePhase.Tower:
                    CompleteWaveAndShowSummary();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(currentPhase), currentPhase, null);
            }
        }

        private void CompleteWaveAndShowSummary()
        {
            if (_waitingForPostRoundContinue) return;

            var summary = BuildPostRoundSummary();
            _waitingForPostRoundContinue = true;

            if (_gm.interfaceManager)
                _gm.interfaceManager.ShowPostRoundSummary(summary, ContinueFromPostRoundSummary);
            else
                ContinueFromPostRoundSummary();
        }

        private PostRoundSummaryData BuildPostRoundSummary()
        {
            PostWaveGrowthResult growthResult;
            if (_gm.popManager)
            {
                growthResult = _gm.popManager.ApplyPostWaveGrowth(infrastructureValue);
                currentLevel = growthResult.LevelAfter;
            }
            else
            {
                growthResult = PostWaveGrowthResult.None(currentLevel);
            }

            var unlocks = _gm.shopManager
                ? _gm.shopManager.GetUnlockNamesForLevelRange(growthResult.LevelBefore, growthResult.LevelAfter)
                : Array.Empty<string>();
            var nextSpawnRateMultiplier = _gm.popManager ? _gm.popManager.GetIssueSpawnRateMultiplier() : 1f;

            return new PostRoundSummaryData(currentTurn, moveCount, growthResult, nextSpawnRateMultiplier, unlocks,
                GetCesspitSummary());
        }

        private static CesspitSummary GetCesspitSummary()
        {
            var count = 0;
            var fullCount = 0;
            var fullness = 0f;
            var capacity = 0f;

            foreach (var cesspit in FindObjectsByType<Cesspit>(FindObjectsInactive.Exclude))
            {
                count++;
                if (cesspit.IsFull)
                    fullCount++;

                fullness += cesspit.fullness;
                capacity += cesspit.maxFullness;
            }

            return new CesspitSummary(count, fullCount, fullness, capacity);
        }

        private void ContinueFromPostRoundSummary()
        {
            _waitingForPostRoundContinue = false;
            EnterCardSequence();
        }

        private void BeginWaveSequence()
        {
            currentPhase = GamePhase.Tower;
            OnTowerPhaseEntered?.Invoke();
            SwitchCamera();
            _gm.placementInventory.ClearSelection();
            if (_gm.pathBuildBoard) _gm.pathBuildBoard.ClearActivePiece();
            _gm.interfaceManager.ClearHand();
            _gm.interfaceManager.HidePrepUI();
            var spawnRateMultiplier = _gm.popManager ? _gm.popManager.GetIssueSpawnRateMultiplier() : 1f;
            var scaledWaveDuration = _gm.popManager
                ? _gm.popManager.GetScaledWaveDuration(waveDuration)
                : Mathf.Max(0f, waveDuration);
            foreach (var s in _gm.entitySpawners.Where(s => s))
                s.StartSpawner(spawnRateMultiplier);

            StartCoroutine(WaveTimer(scaledWaveDuration));

            Debug.Log($"Beginning Wave! Duration: {scaledWaveDuration:F1}s, spawn rate: x{spawnRateMultiplier:F2}");
        }

        private bool CanBeginWave()
        {
            if (_gm.entitySpawners == null) return true;

            foreach (var spawner in _gm.entitySpawners)
            {
                if (!spawner) continue;
                if (spawner.ValidatePath(out var reason)) continue;

                Debug.LogWarning($"Cannot begin wave: {reason}");
                return false;
            }

            return true;
        }

        private IEnumerator WaveTimer(float duration)
        {
            yield return new WaitForSeconds(duration);

            StopAllSpawners();

            while (IssueObject.ActiveCount > 0)
                yield return null;

            currentTurn++;
            if (_gm.debugging) Debug.Log("[TurnController] Wave ended.");

            EndPhase();
        }

        public void GameLost()
        {
            StopAllSpawners();
            Debug.Log("[TurnController] Game Lost!");
        }

        private void StopAllSpawners()
        {
            foreach (var spawner in _gm.entitySpawners.Where(spawner => spawner)) spawner.StopSpawner();

            foreach (var cesspit in FindObjectsByType<Cesspit>(FindObjectsInactive.Exclude))
                cesspit.StopRunaways();
        }
    }
}
