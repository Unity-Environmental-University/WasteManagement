using System;
using System.Collections;
using UnityEngine;

namespace _project.Scripts.Card_Core
{

    public class TurnController : MonoBehaviour
    {
        private static TurnController Instance { get; set; }
        private GameMaster _gm = GameMaster.Instance;
        
        [Header("State")] public int currentTurn;
        [Header("State")] public string currentPhase;

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

        private void Update()
        {
        }

        /// <summary>
        ///     This should initialize game variables and set up the game state for a new game/run.
        /// </summary>
        private void GameStartSequence()
        {
            if (_gm.debugging) Debug.Log("Game Sequence Started!");
            
        }

        private void OnDestroy()
        {
        }

        public IEnumerator BeginWaveSequence()
        {
            return null;
        }

        /// <summary>
        ///     Called when the player ends their turn (e.g. pressing an End Turn button).
        /// </summary>
        public void EndTurn()
        {
        }

        private void PrepareNextWave(int score)
        {
        }

        private void WinGame()
        {
        }

        private void GameLost()
        {
        }
    }
}