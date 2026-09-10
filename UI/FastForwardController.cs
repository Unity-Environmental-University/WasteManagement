using System;
using _project.Scripts.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _project.Scripts.UI
{
    /// <summary>
    ///     Hold-to-fast-forward button. Lives on a UI prefab placed directly in the scene (not
    ///     runtime-instantiated). Only visible during the Tower phase; releasing the button (or
    ///     leaving the Tower phase, or this object being disabled) always restores normal speed.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class FastForwardController : MonoBehaviour
    {
        [SerializeField] private int fastForwardMultiplier = 2;
        [SerializeField] private Button ffButton;
        [SerializeField] private TextMeshProUGUI ffButtonText;

        private bool IsFastForwarding { get; set; }

        private void Awake()
        {
            if (!ffButton)
            {
                Debug.LogError("No FastForward Button!");
                SetVisible(false);
                return;
            }

            var trigger = ffButton.gameObject.GetComponent<EventTrigger>();
            if (!trigger) trigger = ffButton.gameObject.AddComponent<EventTrigger>();

            // Hold to fast-forward: press starts it, release stops it. PointerExit
            // covers a press that drags off the button before lifting.
            AddTriggerEntry(trigger, EventTriggerType.PointerDown, StartFastForward);
            AddTriggerEntry(trigger, EventTriggerType.PointerUp, StopFastForward);
            AddTriggerEntry(trigger, EventTriggerType.PointerExit, HandlePointerExit);

            SetVisible(false);
        }

        private void OnEnable()
        {
            TurnController.OnTowerPhaseEntered += HandleTowerPhaseEntered;
            TurnController.OnCardPhaseEntered += HandleCardPhaseEntered;
        }

        private void OnDisable()
        {
            TurnController.OnTowerPhaseEntered -= HandleTowerPhaseEntered;
            TurnController.OnCardPhaseEntered -= HandleCardPhaseEntered;
            StopFastForward();
        }

        private static void AddTriggerEntry(EventTrigger trigger, EventTriggerType type,
            Action callback)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => callback());
            trigger.triggers.Add(entry);
        }

        private void HandleTowerPhaseEntered() => SetVisible(true);

        private void HandleCardPhaseEntered()
        {
            StopFastForward();
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            ffButton.gameObject.SetActive(visible);
            if (!visible) StopFastForward();
        }

        private void HandlePointerExit()
        {
            // A press that slides off the button before lifting still releases it.
            StopFastForward();
            if (EventSystem.current && EventSystem.current.currentSelectedGameObject == ffButton.gameObject)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void StartFastForward()
        {
            if (IsFastForwarding) return;
            IsFastForwarding = true;
            GameSpeed.SetBaseScale(fastForwardMultiplier);
            ffButtonText.text = ">> x" + fastForwardMultiplier;
        }

        private void StopFastForward()
        {
            if (!IsFastForwarding) return;
            IsFastForwarding = false;
            // Through GameSpeed: if a modal pause is holding the clock at zero, this only
            // resets the base speed underneath it instead of unfreezing the game.
            GameSpeed.ResetBaseScale();
            ffButtonText.text = ">>";
        }
    }
}