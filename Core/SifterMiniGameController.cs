using System.Collections.Generic;
using _project.Scripts.Object_Scripts;
using UnityEngine;

namespace _project.Scripts.Core
{
    public class SifterMiniGameController : MonoBehaviour
    {
        [SerializeField] private GameObject minigamePanel;

        private readonly List<DebrisMiniBucket> _buckets = new();
        private readonly List<DebrisHandler> _debris = new();
        private WasteSifter _activeSifter;
        private bool _isRunning;
        private bool _endQueued;

        /// <summary>True, only between StartMiniGame and EndMiniGame, while a roster is being sorted.</summary>
        public bool IsRunning => _isRunning;

        private void Awake()
        {
            // The panel is authored active, so its layout stays visible while editing, but leaving it
            // live at runtime hands the player a minigame with no roster and no sifter behind it:
            // every piece can be sorted before StartMiniGame runs, and nothing would end the round.
            if (minigamePanel) minigamePanel.SetActive(false);
        }

        public void RegisterBucket(DebrisMiniBucket bucket)
        {
            if (!_buckets.Contains(bucket)) _buckets.Add(bucket);
        }

        public void UnregisterBucket(DebrisMiniBucket bucket)
        {
            _buckets.Remove(bucket);
        }

        public void RegisterHandler(DebrisHandler handler)
        {
            if (_isRunning && !_debris.Contains(handler)) _debris.Add(handler);
        }

        public void UnregisterHandler(DebrisHandler handler)
        {
            if (!_debris.Remove(handler)) return;

            // The last piece unregisters from its own OnDisable, while Unity is still deactivating it, and
            // the panel above it can't be deactivated from inside that; finish the round in LateUpdate.
            if (_isRunning && _debris.Count is 0)
                _endQueued = true;
        }

        private void LateUpdate()
        {
            if (_endQueued) EndMiniGame();
        }

        public void StartMiniGame(WasteSifter sifter)
        {
            if (_isRunning || !minigamePanel) return;

            _activeSifter = sifter;
            _debris.Clear();
            minigamePanel.SetActive(true);

            // Take the roster from the hierarchy rather than from OnEnable registration: a piece that
            // was already enabled has nothing left to fire, and one that woke before GameMaster
            // existed never registered at all. Re-activating also restores pieces the last round sorted.
            foreach (var handler in minigamePanel.GetComponentsInChildren<DebrisHandler>(true))
            {
                if (!handler) continue;

                handler.gameObject.SetActive(true);
                if (!_debris.Contains(handler)) _debris.Add(handler);
            }

            if (_debris.Count is 0)
            {
                Debug.LogWarning($"{name}: no debris under {minigamePanel.name}; nothing to sort.", this);
                EndMiniGame();
                return;
            }

            _isRunning = true;
        }

        public void EndMiniGame()
        {
            // Drop the roster first: deactivating the panel disables every remaining piece, and each of
            // those OnDisable calls comes straight back through UnregisterHandler.
            _isRunning = false;
            _endQueued = false;
            _debris.Clear();

            if (minigamePanel) minigamePanel.SetActive(false);

            if (_activeSifter) _activeSifter.ClearDebris();
            _activeSifter = null;
        }
    }
}
