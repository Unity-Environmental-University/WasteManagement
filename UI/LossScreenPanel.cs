using System;
using _project.Scripts.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _project.Scripts.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class LossScreenPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private TextMeshProUGUI statsText;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button quitButton;

        private Action _onRetry;
        private Action _onQuit;

        private void Awake()
        {
            Hide();
        }

        public void Show(int turn, int moveCount, int populationSize, int level)
        {
            Show(turn, moveCount, populationSize, level, RestartCurrentScene,
                WasteBoardReplayRecorder.RequestApplicationQuit);
        }

        private void Show(int turn, int moveCount, int populationSize, int level, Action onRetry, Action onQuit)
        {
            if (!IsConfigured())
            {
                Debug.LogError($"{nameof(LossScreenPanel)} is missing prefab references.", this);
                return;
            }

            _onRetry = onRetry;
            _onQuit = onQuit;

            titleText.text = "System Failure";
            messageText.text = "The waste network collapsed before the town could recover.";
            statsText.text =
                $"Rounds survived: {Mathf.Max(0, turn)}\n" +
                $"Moves made: {Mathf.Max(0, moveCount)}\n" +
                $"Population: {Mathf.Max(0, populationSize)}\n" +
                $"Level reached: {Mathf.Max(1, level)}";

            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(Retry);
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(Quit);
        }

        private bool IsConfigured()
        {
            return titleText && messageText && statsText && retryButton && quitButton;
        }

        private static void RestartCurrentScene()
        {
            WasteBoardReplayRecorder.RequestRestartCurrentScene();
        }

        private void Retry()
        {
            var onRetry = _onRetry;
            ClearActions();
            onRetry?.Invoke();
        }

        private void Quit()
        {
            var onQuit = _onQuit;
            ClearActions();
            onQuit?.Invoke();
        }

        private void Hide()
        {
            if (retryButton)
                retryButton.onClick.RemoveAllListeners();

            if (quitButton)
                quitButton.onClick.RemoveAllListeners();

            ClearActions();
            gameObject.SetActive(false);
        }

        private void ClearActions()
        {
            _onRetry = null;
            _onQuit = null;
        }
    }
}
