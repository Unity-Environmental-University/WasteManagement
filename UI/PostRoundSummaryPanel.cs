using System;
using System.Linq;
using System.Text;
using _project.Scripts.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _project.Scripts.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class PostRoundSummaryPanel : MonoBehaviour
    {
        private const string UnlocksHeader = "Level Ups\n\n";

        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI statsText;
        [SerializeField] private TextMeshProUGUI unlocksText;
        [SerializeField] private Button continueButton;

        private Action _onContinue;

        private void Awake()
        {
            Hide();
        }

        public void Show(PostRoundSummaryData summary, Action onContinue)
        {
            if (!IsConfigured())
            {
                Debug.LogError($"{nameof(PostRoundSummaryPanel)} is missing prefab references.", this);
                onContinue?.Invoke();
                return;
            }

            _onContinue = onContinue;
            ApplySummary(summary);

            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(Continue);
        }

        private bool IsConfigured()
        {
            return titleText && levelText && statsText && unlocksText && continueButton;
        }

        private void ApplySummary(PostRoundSummaryData summary)
        {
            if (summary == null) return;

            var growth = summary.GrowthResult;
            titleText.text = $"Round {summary.RoundNumber} Complete";
            levelText.text = growth.LeveledUp
                ? $"Level Up! {growth.LevelBefore} -> {growth.LevelAfter}"
                : $"Level {growth.LevelAfter}";

            statsText.text = BuildStatsText(summary);
            unlocksText.text = BuildUnlocksText(summary);
        }

        private static string BuildStatsText(PostRoundSummaryData summary)
        {
            var growth = summary.GrowthResult;
            var builder = new StringBuilder()
                .AppendLine("Post-Round Stats")
                .AppendLine()
                .AppendLine(
                    $"Population: {growth.PopulationBefore} -> {growth.PopulationAfter} (+{growth.AppliedGrowth})")
                .AppendLine($"Raw growth: {growth.RawGrowth:F1}")
                .AppendLine($"Lake pollution: {growth.Pollution:F1}")
                .AppendLine($"Town stink: {growth.Stink:F1}")
                .AppendLine($"Moves made: {summary.MoveCount}");

            var cesspits = summary.Cesspits;
            if (cesspits.Count <= 0)
                return builder
                    .AppendLine($"Next wave pressure: x{summary.NextWaveSpawnRateMultiplier:F1}")
                    .ToString();
            var capacity = cesspits.Capacity > 0f ? cesspits.Capacity.ToString("F1") : "n/a";
            builder
                .AppendLine($"Cesspit storage: {cesspits.Fullness:F1} / {capacity}")
                .AppendLine($"Full cesspits: {cesspits.FullCount} / {cesspits.Count}");

            return builder
                .AppendLine($"Next wave pressure: x{summary.NextWaveSpawnRateMultiplier:F1}")
                .ToString();
        }

        private static string BuildUnlocksText(PostRoundSummaryData summary)
        {
            var growth = summary.GrowthResult;
            if (!growth.LeveledUp)
                return $"{UnlocksHeader}No level up this round.";

            if (summary.UnlockedItems.Count == 0)
                return $"{UnlocksHeader}No new shop unlocks registered for this level.";

            var unlockList = string.Join("\n", summary.UnlockedItems.Select(item => $"- {item}"));
            return $"{UnlocksHeader}Unlocked:\n{unlockList}";
        }

        private void Continue()
        {
            Hide();
            var onContinue = _onContinue;
            _onContinue = null;
            onContinue?.Invoke();
        }

        private void Hide()
        {
            if (continueButton)
                continueButton.onClick.RemoveAllListeners();

            gameObject.SetActive(false);
        }
    }
}