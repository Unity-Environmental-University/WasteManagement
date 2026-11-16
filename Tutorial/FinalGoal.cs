using System;
using System.Collections;
using _project.Scripts.Core;
using _project.Scripts.Object_Scripts;
using UnityEngine;

namespace _project.Scripts.Tutorial
{
    /// <summary>
    ///     The run's final goal. Once every listed bin has unlocked, the next setup phase puts the
    ///     goal story on the newspaper popup; once every bin has collected
    ///     <see cref="targetPerMaterial" /> pieces, the win story follows and play carries on. Each
    ///     popup freezes the game clock like the incoming-item warnings, and waits for any other
    ///     popup to close first, so the goal always comes after the last item warning. The copy
    ///     lives in the two story assets and the materials are the scene's receptacles, so a new
    ///     bin (a plastic type, say) joins the goal by being added to the list. Shows once per run;
    ///     a retry re-arms it, matching the other tutorials.
    /// </summary>
    public class FinalGoal : MonoBehaviour
    {
        private enum Stage
        {
            Locked,
            Open,
            Won
        }

        private static bool Debugging => GameMaster.Instance && GameMaster.Instance.debugging;

        [SerializeField] private IncomingItemPanel panelPrefab;

        [Tooltip("Front page shown once every material's bin has unlocked.")]
        [SerializeField] private IncomingItemAlert goalStory;

        [Tooltip("Front page shown when every bin reaches the target.")]
        [SerializeField] private IncomingItemAlert winStory;

        [Tooltip("One bin per material. The goal opens once all of them have unlocked.")]
        [SerializeField] private Receptacle[] materials;

        [Tooltip("How many pieces each bin must collect to win.")]
        [SerializeField] [Min(1)] private int targetPerMaterial = 100;

        private Stage _stage;
        private Coroutine _queued;
        private bool _holdingPause;
        private IncomingItemPanel _panel;

        public bool IsGoalOpen => _stage != Stage.Locked;
        public bool IsWon => _stage == Stage.Won;

        private void OnEnable()
        {
            TurnController.OnCardPhaseEntered += HandleCardPhaseEntered;
            Receptacle.Collected += HandleCollected;
        }

        private void OnDisable()
        {
            TurnController.OnCardPhaseEntered -= HandleCardPhaseEntered;
            Receptacle.Collected -= HandleCollected;
            // Disabling a component doesn't stop its coroutines, and the game must never stay frozen.
            StopAllCoroutines();
            _queued = null;
            Dismiss();
        }

        private void HandleCardPhaseEntered()
        {
            if (_stage != Stage.Locked || _queued != null || !EveryBin(bin => bin.IsUnlocked)) return;

            _queued = StartCoroutine(ShowWhenClear(goalStory, () =>
            {
                _queued = null;
                _stage = Stage.Open;
                CheckForWin();
            }));
        }

        private void HandleCollected(Receptacle bin)
        {
            CheckForWin();
        }

        private void CheckForWin()
        {
            if (_stage != Stage.Open || !EveryBin(bin => bin.DeliveredCount >= targetPerMaterial)) return;

            _stage = Stage.Won;
            if (Debugging) Debug.Log($"Every bin reached {targetPerMaterial}. Final goal won.", this);
            StartCoroutine(ShowWhenClear(winStory, null));
        }

        private bool EveryBin(Func<Receptacle, bool> condition)
        {
            if (materials == null || materials.Length == 0) return false;

            foreach (var bin in materials)
                if (!bin || !condition(bin))
                    return false;
            return true;
        }

        /// <summary>
        ///     Waits a frame so a popup opened by the same event (the paper warning) claims the
        ///     screen first, then until nothing else holds the game frozen.
        /// </summary>
        private IEnumerator ShowWhenClear(IncomingItemAlert story, Action shown)
        {
            yield return null;
            yield return new WaitUntil(() => !_panel && !GameSpeed.IsPaused);

            Show(story);
            shown?.Invoke();
        }

        private void Show(IncomingItemAlert story)
        {
            if (!panelPrefab || !story)
            {
                Debug.LogWarning($"{nameof(FinalGoal)} is missing its panel prefab or story. Popup skipped.", this);
                return;
            }

            var panel = Instantiate(panelPrefab);
            if (!panel.IsConfigured())
            {
                // An undismissable modal over a frozen game is worse than no popup.
                Debug.LogError($"{nameof(FinalGoal)}'s panel prefab is missing references. Popup skipped.", this);
                Destroy(panel.gameObject);
                return;
            }

            _panel = panel;
            _holdingPause = true;
            GameSpeed.Pause();

            var gm = GameMaster.Instance;
            var era = gm && gm.popManager && gm.turnController ? gm.popManager.GetEra(gm.turnController.currentLevel) : 1;
            _panel.Show(story, era);
            _panel.Dismissed += Dismiss;

            if (Debugging) Debug.Log($"Final goal popup \"{story.title}\" shown, game paused.", this);
        }

        private void Dismiss()
        {
            if (_holdingPause)
            {
                _holdingPause = false;
                GameSpeed.Resume();
            }

            if (!_panel) return;
            Destroy(_panel.gameObject);
            _panel = null;
        }
    }
}
