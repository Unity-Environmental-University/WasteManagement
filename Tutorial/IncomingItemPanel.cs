using System;
using _project.Scripts.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _project.Scripts.Tutorial
{
    /// <summary>
    ///     Modal popup announcing a new item type as a newspaper front page. The prefab
    ///     (Assets/_project/Prefabs/UI/IncomingItemPanel.prefab) owns the hierarchy, styling, and
    ///     the sort demo's Animator, which runs on unscaled time so it plays while the game is
    ///     frozen. This script only fills in the story and picks the era's art. As with
    ///     PipeClogTutorialPanel, the dimming backdrop must stay a Button so clicks can't fall
    ///     through to the build board behind the popup.
    /// </summary>
    public class IncomingItemPanel : MonoBehaviour
    {
        [SerializeField] private Button acceptButton;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text subheadLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private TMP_Text captionLabel;
        [SerializeField] private GameObject sortDemo;

        [Tooltip("The bin's label in the sort demo, set to the incoming item's name.")]
        [SerializeField] private TMP_Text sortDemoBinLabel;

        [Header("Era Look")]
        [SerializeField] private Image paper;
        [SerializeField] private DynamicSpriteSO paperByEra;
        [SerializeField] private Image mayorPortrait;
        [SerializeField] private DynamicSpriteSO mayorByEra;
        [SerializeField] private Image splitter;
        [SerializeField] private DynamicSpriteSO splitterByEra;
        [SerializeField] private TMP_Text datelineLabel;

        [Tooltip("Printed in the dateline, one per era.")]
        [SerializeField] private string[] yearByEra;

        public event Action Dismissed;

        private void Awake()
        {
            if (acceptButton) acceptButton.onClick.AddListener(() => Dismissed?.Invoke());
        }

        /// <summary>
        ///     True when the popup can actually be dismissed; a missing button would strand the
        ///     player on a frozen game.
        /// </summary>
        public bool IsConfigured()
        {
            return acceptButton;
        }

        public void Show(IncomingItemAlert alert, int era)
        {
            if (titleLabel) titleLabel.text = alert.title;
            if (subheadLabel) subheadLabel.text = alert.subhead;
            if (bodyLabel) bodyLabel.text = alert.body;
            if (captionLabel) captionLabel.text = alert.caption;
            if (sortDemo) sortDemo.SetActive(alert.showSortDemo);
            if (sortDemoBinLabel) sortDemoBinLabel.text = $"{alert.type.ToString().ToUpperInvariant()} BIN";

            era = Mathf.Clamp(era, 1, 3);
            ApplySprite(paper, paperByEra, era);
            ApplySprite(mayorPortrait, mayorByEra, era);
            ApplySprite(splitter, splitterByEra, era);
            if (datelineLabel && era <= yearByEra.Length)
                datelineLabel.text = $"EXTRA EDITION   |   {yearByEra[era - 1]}   |   PRICE ONE PENNY";
        }

        private static void ApplySprite(Image image, DynamicSpriteSO sprites, int era)
        {
            if (!image || !sprites) return;

            var sprite = sprites.GetSpriteForEra(era);
            if (sprite) image.sprite = sprite;
        }
    }
}
