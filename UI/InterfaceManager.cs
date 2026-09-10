using System;
using System.Collections.Generic;
using _project.Scripts.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace _project.Scripts.UI
{
    public class InterfaceManager : MonoBehaviour
    {
        [SerializeField] private Button quitButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button openShopButton;
        [SerializeField] private GameObject shopPanel;
        [SerializeField] private Button closeShopButton;
        [SerializeField] private Image mTowerUpgrades;
        [SerializeField] private Image rTowerUpgrades;
        [SerializeField] private Image lTowerUpgrades;

        [Header("Headline Banner")]
        [Tooltip("Number shown inside the left masthead box.")]
        [SerializeField] private TextMeshProUGUI turnValueText;
        [Tooltip("Number shown inside the middle masthead box.")]
        [SerializeField] private TextMeshProUGUI movesValueText;
        [Tooltip("Number shown inside the right masthead box.")]
        [SerializeField] private TextMeshProUGUI popValueText;
        [Tooltip("Addressable section line beneath the masthead. Currently shows the level and edition year; change its content in UpdateSectionLine.")]
        [FormerlySerializedAs("infoBarText")]
        [SerializeField] private TextMeshProUGUI sectionLineText;
        [Tooltip("Newspaper edition year shown on the section line.")]
        [SerializeField] private string editionYear = "1870";

        [SerializeField] private Slider stinkMeter;
        [SerializeField] private PostRoundSummaryPanel postRoundSummaryPanelPrefab;
        [SerializeField] private LossScreenPanel lossScreenPanelPrefab;
        [SerializeField] private PathToolBar pathToolBar;
        [SerializeField] private PathToolBar pathToolBarPrefab;
        private PostRoundSummaryPanel _postRoundSummaryPanel;
        private LossScreenPanel _lossScreenPanel;

        [Header("Hand")]
        [SerializeField] private Transform handContainer;
        [SerializeField] private CardController cardPrefab;
        [SerializeField] private float cardSpacing = 200f;

        private void Start()
        {
#if UNITY_WEBGL
            if (quitButton) Destroy(quitButton);
#endif

            if (quitButton) quitButton.onClick.AddListener(WasteBoardReplayRecorder.RequestApplicationQuit);
            EnsurePathToolBar();
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
            SetActive(lTowerUpgrades, false);
            SetActive(mTowerUpgrades, false);
            SetActive(rTowerUpgrades, false);
            SetActive(openShopButton, false);
            SetActive(closeShopButton, false);
            SetActive(shopPanel, false);
            SetActive(nextButton, false);
            EnsurePathToolBar();
            if (pathToolBar) pathToolBar.SetVisible(false);
        }

        public void ShowPrepUI()
        {
            SetActive(openShopButton, true);
            SetActive(closeShopButton, true);
            SetActive(nextButton, true);
            EnsurePathToolBar();
            if (pathToolBar) pathToolBar.SetVisible(true);
        }

        public void UpdateInfo(int turn, int moveCount, int populationSize, int currentLevel)
        {
            UpdateInfoBar(turn, moveCount, populationSize, currentLevel);
            RefreshStinkMeter();
        }

        public void RefreshStinkMeter()
        {
            var popManager = GameMaster.Instance ? GameMaster.Instance.popManager : null;
            var stink = popManager ? popManager.StinkValue : 0f;
            UpdateStinkMeter(stink);
        }

        private void UpdateInfoBar(int turn, int moveCount, int popVal, int currentLevel)
        {
            if (turnValueText) turnValueText.text = turn.ToString();
            if (movesValueText) movesValueText.text = moveCount.ToString();
            if (popValueText) popValueText.text = popVal.ToString();
            UpdateSectionLine(currentLevel);
        }

        // The line beneath the masthead is a deliberately swappable "section" slot.
        // Change what the banner reports here without touching the boxes above.
        private void UpdateSectionLine(int currentLevel)
        {
            if (!sectionLineText) return;
            sectionLineText.text = string.IsNullOrWhiteSpace(editionYear)
                ? $"LEVEL {currentLevel}"
                : $"LEVEL {currentLevel}   <color=#9a7d55>|</color>   EST. {editionYear}";
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
            if (quitButton) SetActive(quitButton, false);
            SetActive(openShopButton, false);
            SetActive(closeShopButton, true);
            EnsurePathToolBar();
            if (pathToolBar) pathToolBar.SetVisible(true);
        }

        public void RecoverUIForShop()
        {
            if (quitButton) SetActive(quitButton, true);
            SetActive(openShopButton, true);
            SetActive(closeShopButton, false);
            EnsurePathToolBar();
            if (pathToolBar) pathToolBar.SetVisible(true);
        }

        /// <summary>The toolbar, resolved or instantiated on demand — for external callers (e.g., the tutorial highlight).</summary>
        public PathToolBar PathToolBar
        {
            get
            {
                EnsurePathToolBar();
                return pathToolBar;
            }
        }

        private void EnsurePathToolBar()
        {
            if (!pathToolBar)
                pathToolBar = GetComponentInChildren<PathToolBar>(true);

            if (!pathToolBar && pathToolBarPrefab)
                pathToolBar = Instantiate(pathToolBarPrefab, transform);

            if (!pathToolBar)
            {
                Debug.LogWarning($"{nameof(InterfaceManager)} is missing a path tool bar prefab.", this);
                return;
            }

            pathToolBar.EnsureBuilt();
        }

        private static void SetActive(Component component, bool active)
        {
            if (component) component.gameObject.SetActive(active);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target) target.SetActive(active);
        }
    }
}
