using System;
using System.Collections.Generic;
using _project.Scripts.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _project.Scripts.UI
{
    public class InterfaceManager : MonoBehaviour
    {
        [SerializeField] private Button quitButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button openShopButton;
        [SerializeField] private Button closeShopButton;
        [SerializeField] private Image mTowerUpgrades;
        [SerializeField] private Image rTowerUpgrades;
        [SerializeField] private Image lTowerUpgrades;
        [SerializeField] private TextMeshProUGUI infoBarText;
        [SerializeField] private Slider stinkMeter;
        [SerializeField] private PostRoundSummaryPanel postRoundSummaryPanelPrefab;
        [SerializeField] private LossScreenPanel lossScreenPanelPrefab;
        private PostRoundSummaryPanel _postRoundSummaryPanel;
        private LossScreenPanel _lossScreenPanel;

        [Header("Hand")]
        [SerializeField] private Transform handContainer;
        [SerializeField] private CardController cardPrefab;
        [SerializeField] private float cardSpacing = 200f;

        private void Start()
        {
            if (quitButton) quitButton.onClick.AddListener(Application.Quit);
        }

        public void PopulateHand(IReadOnlyList<ICard> hand)
        {
            ClearHand();
            var count = hand.Count;
            var totalWidth = (count - 1) * cardSpacing;

            for (var i = 0; i < count; i++)
            {
                var cardController = Instantiate(cardPrefab, handContainer);
                cardController.AssignCard(hand[i]);

                var rt = cardController.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(-totalWidth / 2f + i * cardSpacing, 0f);
            }
        }

        public void HidePrepUI()
        {
            lTowerUpgrades.gameObject.SetActive(false);
            mTowerUpgrades.gameObject.SetActive(false);
            rTowerUpgrades.gameObject.SetActive(false);
            nextButton.gameObject.SetActive(false);
            openShopButton.gameObject.SetActive(false);
            closeShopButton.gameObject.SetActive(false);
        }

        public void ShowPrepUI()
        {
            nextButton.gameObject.SetActive(true);
            openShopButton.gameObject.SetActive(true);
            closeShopButton.gameObject.SetActive(true);
        }

        public void UpdateInfo(int moveCount, int populationSize, int currentLevel)
        {
            UpdateInfoBar(moveCount, populationSize, currentLevel);
            RefreshStinkMeter();
        }

        public void RefreshStinkMeter()
        {
            var popManager = GameMaster.Instance ? GameMaster.Instance.popManager : null;
            var stink = popManager ? popManager.StinkValue : 0f;
            UpdateStinkMeter(stink);
        }

        private void UpdateInfoBar(int moveCount, int popVal, int currentLevel)
        {
            if (!infoBarText) return;
            infoBarText.text = $"Moves Made: {moveCount} Population: {popVal} Level: {currentLevel}";
        }

        private void UpdateStinkMeter(float stinkValue)
        {
            if (!stinkMeter) return;

            var clampedStinkValue = Mathf.Clamp(stinkValue, stinkMeter.minValue, stinkMeter.maxValue);
            stinkMeter.value = clampedStinkValue;
        }

        public void ShowPostRoundSummary(PostRoundSummaryData summary, Action onContinue)
        {
            if (!_postRoundSummaryPanel)
                _postRoundSummaryPanel = GetComponentInChildren<PostRoundSummaryPanel>(true);

            if (!_postRoundSummaryPanel && postRoundSummaryPanelPrefab)
                _postRoundSummaryPanel = Instantiate(postRoundSummaryPanelPrefab, transform);

            if (!_postRoundSummaryPanel)
            {
                Debug.LogWarning($"{nameof(InterfaceManager)} is missing a post-round summary panel prefab.", this);
                onContinue?.Invoke();
                return;
            }

            _postRoundSummaryPanel.Show(summary, onContinue);
        }

        public void ShowLossScreen(int turn, int moveCount, int populationSize, int level)
        {
            if (!_lossScreenPanel)
                _lossScreenPanel = GetComponentInChildren<LossScreenPanel>(true);

            if (!_lossScreenPanel && lossScreenPanelPrefab)
                _lossScreenPanel = Instantiate(lossScreenPanelPrefab, transform);

            if (!_lossScreenPanel)
            {
                Debug.LogWarning($"{nameof(InterfaceManager)} is missing a loss screen panel prefab.", this);
                return;
            }

            HidePrepUI();
            ClearHand();
            _lossScreenPanel.Show(turn, moveCount, populationSize, level);
        }

        public void NextButtonPressed()
        {
            if (GameMaster.Instance.turnController.currentPhase == GamePhase.Tower) return;
            GameMaster.Instance.turnController.EndPhase();
        }

        public void ClearHand()
        {
            for (var i = handContainer.childCount - 1; i >= 0; i--)
                Destroy(handContainer.GetChild(i).gameObject);
        }

        public void HideUIForShop()
        {
            quitButton.gameObject.SetActive(false);
            nextButton.gameObject.SetActive(false);
            openShopButton.gameObject.SetActive(false);
            closeShopButton.gameObject.SetActive(true);
        }

        public void RecoverUIForShop()
        {
            quitButton.gameObject.SetActive(true);
            nextButton.gameObject.SetActive(true);
            openShopButton.gameObject.SetActive(true);
            closeShopButton.gameObject.SetActive(false);
        }
    }
}
