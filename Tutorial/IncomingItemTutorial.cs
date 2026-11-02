using System.Collections.Generic;
using _project.Scripts.Core;
using _project.Scripts.Object_Scripts;
using UnityEngine;

namespace _project.Scripts.Tutorial
{
    /// <summary>
    ///     Warns the player about a new item type before it reaches the pipes: on entering the
    ///     setup phase, any configured type the spawners can now produce gets a modal popup
    ///     (game clock frozen through GameSpeed.Pause()) saying how to deal with it, so there is
    ///     still a whole setup phase left to build for it. The unlock level comes from the
    ///     spawned prefab, so the warning can't drift from when the item really appears. Each
    ///     type shows once per run — a retry re-arms it, matching the other tutorials.
    /// </summary>
    public class IncomingItemTutorial : MonoBehaviour
    {
        private static bool Debugging => GameMaster.Instance && GameMaster.Instance.debugging;

        [SerializeField] private IncomingItemPanel panelPrefab;

        [SerializeField] private IncomingItemAlert[] alerts;

        private readonly Queue<IncomingItemAlert> _pending = new();
        private readonly HashSet<IssueType> _shown = new();
        private bool _holdingPause;
        private IncomingItemPanel _panel;

        private void OnEnable()
        {
            TurnController.OnCardPhaseEntered += HandleCardPhaseEntered;
        }

        private void OnDisable()
        {
            TurnController.OnCardPhaseEntered -= HandleCardPhaseEntered;
            // Never leave the game frozen if this component dies while the popup is up.
            _pending.Clear();
            Dismiss();
        }

        private void HandleCardPhaseEntered()
        {
            var gm = GameMaster.Instance;
            if (!gm || !gm.turnController) return;

            foreach (var alert in alerts)
                if (alert && !_shown.Contains(alert.type) && CanSpawn(gm, alert.type))
                {
                    _shown.Add(alert.type);
                    _pending.Enqueue(alert);
                }

            ShowNext();
        }

        private static bool CanSpawn(GameMaster gm, IssueType type)
        {
            if (gm.entitySpawners == null) return false;

            foreach (var spawner in gm.entitySpawners)
                if (spawner && spawner.CanSpawn(type, gm.turnController.currentLevel))
                    return true;

            return false;
        }

        private void ShowNext()
        {
            if (_panel || _pending.Count == 0) return;
            var alert = _pending.Dequeue();

            if (!panelPrefab)
            {
                Debug.LogWarning($"{nameof(IncomingItemTutorial)} is missing its panel prefab — {alert.type} popup skipped.",
                    this);
                return;
            }

            var panel = Instantiate(panelPrefab);
            if (!panel.IsConfigured())
            {
                // An undismissable modal over a frozen game is worse than no warning.
                Debug.LogError($"{nameof(IncomingItemTutorial)}'s panel prefab is missing references — popup skipped.",
                    this);
                Destroy(panel.gameObject);
                return;
            }

            _panel = panel;
            if (!_holdingPause)
            {
                _holdingPause = true;
                GameSpeed.Pause();
            }

            var gm = GameMaster.Instance;
            var era = gm && gm.popManager && gm.turnController ? gm.popManager.GetEra(gm.turnController.currentLevel) : 1;
            _panel.Show(alert, era);
            _panel.Dismissed += HandleDismissed;

            if (Debugging) Debug.Log($"New item {alert.type} on the way — popup shown, game paused.", this);
        }

        private void HandleDismissed()
        {
            Dismiss();
            ShowNext();
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
