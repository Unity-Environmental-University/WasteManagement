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
        private static TurnController Instance { get; set; }
        private GameMaster _gm = GameMaster.Instance;
        [Header("State")] public int currentTurn;
        [Header("State")] public GamePhase currentPhase;

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
            EnterCardSequence();
        }

        private void EnterCardSequence()
        {
            currentPhase = GamePhase.Card;
            
            _gm.deckManager.DrawNewHand();
            _gm.interfaceManager.PopulateHand(_gm.deckManager.Hand);
            
            if (_gm.debugging) Debug.Log($"[TurnController] Card phase — turn {currentTurn}");
        }

        public void EndPhase()
        {
            switch (currentPhase)
            {
                case GamePhase.Card:
                    BeginWaveSequence();
                    break;
                case GamePhase.Tower:
                    EnterCardSequence();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(currentPhase), currentPhase, null);
            }
        }

        //TODO - Hide Cards
        private void BeginWaveSequence()
        {
            currentPhase = GamePhase.Tower;
            _gm.interfaceManager.ClearHand();
            foreach (var s in _gm.entitySpawners) s.StartSpawner();

            StartCoroutine(WaveTimer(waveDuration));
            
            Debug.Log("Beginning Wave!");
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

        private void PrepareNextWave(int score)
        {
            throw new NotImplementedException();
        }

        private void WinGame()
        {
            throw new NotImplementedException();
        }

        public void GameLost()
        {
            Debug.Log("[TurnController] Game Lost!");
        }
    }
}