using System;
using UnityEngine;
using UnityEngine.Events;

namespace _project.Scripts.Core
{
    public enum GamePhase
    {
        Card,
        Tower
    }

    public class TurnController : MonoBehaviour
    {
        private GameMaster _gm = GameMaster.Instance;
        
        [Header("State")] public int currentTurn;
        [Header("State")] public GamePhase currentPhase;
        
        public static TurnController Instance { get; private set; }

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
            //TODO draw cards with DeckManager 
            currentPhase = GamePhase.Card;
            Debug.Log("Entering Card Sequence!");
            throw new NotImplementedException();
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

        private void BeginWaveSequence()
        {
            currentPhase = GamePhase.Tower;
            Debug.Log("Beginning Wave!");
            //TODO - Hide Cards, start sending waves
            throw new NotImplementedException();
        }

        private void PrepareNextWave(int score)
        {
            throw new NotImplementedException();
        }

        private void WinGame()
        {
            throw new NotImplementedException();
        }

        private void GameLost()
        {
            throw new NotImplementedException();
        }
    }
}