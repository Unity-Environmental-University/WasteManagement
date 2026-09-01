using System.Collections.Generic;
using _project.Scripts.Object_Scripts;
using UnityEngine;

namespace _project.Scripts.Core
{
    public class SifterMiniGameController : MonoBehaviour
    {
        [SerializeField] private GameObject minigamePanel;

        private readonly List<DebrisMiniBucket> buckets = new();
        private readonly List<DebrisHandler> debris = new();
        private WasteSifter _activeSifter;

        public void RegisterBucket(DebrisMiniBucket bucket)
        {
            buckets.Add(bucket);
        }

        public void UnregisterBucket(DebrisMiniBucket bucket)
        {
            buckets.Remove(bucket);
        }

        public void RegisterHandler(DebrisHandler handler)
        {
            debris.Add(handler);
        }

        public void UnregisterHandler(DebrisHandler handler)
        {
            debris.Remove(handler);
            if (debris.Count is 0)
                EndMiniGame();
        }

        public void StartMiniGame(WasteSifter sifter)
        {
            _activeSifter = sifter;
            minigamePanel.SetActive(true);
            //SpawnDebris();
        }

        public void EndMiniGame()
        {
            minigamePanel.SetActive(false);
            if (_activeSifter)
                _activeSifter.ClearDebris();

            _activeSifter = null;
        }
    }
}
