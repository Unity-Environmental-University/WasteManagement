using System;
using System.Collections;
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
        public static event Action OnCardPhaseEntered;
        public static event Action OnTowerPhaseEntered;
        private static TurnController Instance { get; set; }
        private GameMaster _gm = GameMaster.Instance;
        [Header("State")] public int currentTurn;
        [Header("State")] public int currentLevel;
        [Header("State")] public GamePhase currentPhase;
        [Header("State")] public int moveCount;
        [Header("State")] public int infrastructureValue;

        public float waveDuration = 60;

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
        }

        private void EnterCardSequence()
        {
            currentPhase = GamePhase.Card;
            OnCardPhaseEntered?.Invoke();
            SwitchCamera();

            _gm.placementInventory.SelectFirstAvailable();
            _gm.deckManager.DrawNewHand();
            // TODO: re-enable hand UI when card system is back in use
            // _gm.interfaceManager.PopulateHand(_gm.deckManager.Hand);
            _gm.interfaceManager.ShowPrepUI();

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
                    currentLevel = _gm.popManager ? _gm.popManager.GetLevelByPopulationSize() : currentLevel;
                    EnterCardSequence();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(currentPhase), currentPhase, null);
            }
        }
        
        private void BeginWaveSequence()
        {
            currentPhase = GamePhase.Tower;
            OnTowerPhaseEntered?.Invoke();
            SwitchCamera();
            _gm.placementInventory.ClearSelection();
            _gm.interfaceManager.ClearHand();
            _gm.interfaceManager.HidePrepUI();
            foreach (var s in _gm.entitySpawners)
                if (s)
                    s.StartSpawner();

            StartCoroutine(WaveTimer(waveDuration));
            
            Debug.Log("Beginning Wave!");
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

            foreach (var spawner in _gm.entitySpawners)
                spawner.StopSpawner();

            currentTurn++;
            if (_gm.debugging) Debug.Log("[TurnController] Wave ended.");

            EndPhase();
        }

        public void GameLost()
        {
            foreach (var s in _gm.entitySpawners)
            {
                s.StopSpawner();
            }
            Debug.Log("[TurnController] Game Lost!");
        }
    }
}
