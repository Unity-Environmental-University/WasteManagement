using System.Collections.Generic;
using _project.Scripts.Core;
using _project.Scripts.Object_Scripts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _project.Scripts.UI
{
    /// <summary>
    /// Pipe-routing tool palette, styled as an industrial "PIPELINE" control panel.
    /// Each tool is a backlit switch: dark steel at rest, lit when armed
    /// (cyan = lay pipe, amber = remove, pale = cursor). Exactly one switch is lit.
    /// The panel itself lives in the PathToolBar prefab; this component binds the
    /// switches to the build tools and drives their lit/idle state.
    /// </summary>
    public class PathToolBar : MonoBehaviour
    {
        [SerializeField] private Button shortPipeButton;
        [SerializeField] private Button longPipeButton;
        [SerializeField] private Button breakPipeButton;
        [SerializeField] private Button clearToolButton;

        [Tooltip("Hint label telling the player R rotates the armed pipe. Dimmed when no pipe is armed.")]
        [SerializeField] private TMP_Text rotateHintLabel;

        [Header("Palette")]
        [SerializeField] private Color flowColor = new(0.169f, 0.769f, 0.839f, 1f);    // armed: lay pipe
        [SerializeField] private Color cautionColor = new(0.914f, 0.635f, 0.235f, 1f); // armed/idle: remove
        [SerializeField] private Color neutralColor = new(0.792f, 0.831f, 0.847f, 1f); // armed: cursor
        [SerializeField] private Color inkColor = new(0.055f, 0.078f, 0.094f, 1f);     // text on a lit switch
        [SerializeField] private Color textColor = new(0.914f, 0.945f, 0.953f, 1f);    // text on an idle switch

        private struct ButtonStyle
        {
            public Color IdleFace;
            public Color HoverFace;
            public Color AccentFace; // lit when armed
            public Color IdleText;
            public Color EngagedText;
            public TMP_Text Label;
        }

        /// <summary>The PIPE 2-CELL switch, for external callers (e.g. the tutorial highlight).</summary>
        public RectTransform ShortPipeButtonRect =>
            shortPipeButton ? (RectTransform)shortPipeButton.transform : null;

        private readonly Dictionary<Button, ButtonStyle> _styles = new();
        private bool _isBound;
        private bool _warnedMissingButtons;

        private void Awake()
        {
            EnsureBuilt();
            BindButtons();
        }

        private void OnEnable()
        {
            EnsureBuilt();
            BindButtons();
            RefreshState();
        }

        private void Update()
        {
            RefreshState();
        }

        private void OnDestroy()
        {
            UnbindButtons();
        }

        /// <summary>
        /// Registers styling metadata for the prefab-wired switches.
        /// Safe to call repeatedly; the per-button work happens once.
        /// </summary>
        public void EnsureBuilt()
        {
            if (!shortPipeButton || !longPipeButton || !breakPipeButton || !clearToolButton)
            {
                if (_warnedMissingButtons) return;
                Debug.LogWarning($"{nameof(PathToolBar)} is missing one or more button references; " +
                                 "wire them on the prefab.", this);
                _warnedMissingButtons = true;
                return;
            }

            RegisterAssignedStyle(shortPipeButton, flowColor);
            RegisterAssignedStyle(longPipeButton, flowColor);
            RegisterAssignedStyle(breakPipeButton, cautionColor);
            RegisterAssignedStyle(clearToolButton, neutralColor);
        }

        public void SetVisible(bool visible)
        {
            EnsureBuilt();
            gameObject.SetActive(visible);
        }

        private void BindButtons()
        {
            if (_isBound) return;

            if (shortPipeButton) shortPipeButton.onClick.AddListener(SelectShortPipe);
            if (longPipeButton) longPipeButton.onClick.AddListener(SelectLongPipe);
            if (breakPipeButton) breakPipeButton.onClick.AddListener(SelectBreakPipe);
            if (clearToolButton) clearToolButton.onClick.AddListener(ClearTool);
            _isBound = true;
        }

        private void UnbindButtons()
        {
            if (!_isBound) return;

            if (shortPipeButton) shortPipeButton.onClick.RemoveListener(SelectShortPipe);
            if (longPipeButton) longPipeButton.onClick.RemoveListener(SelectLongPipe);
            if (breakPipeButton) breakPipeButton.onClick.RemoveListener(SelectBreakPipe);
            if (clearToolButton) clearToolButton.onClick.RemoveListener(ClearTool);
            _isBound = false;
        }

        private void SelectShortPipe()
        {
            ShopManager.Instance?.SelectShortPipeTool();
            RefreshState();
        }

        private void SelectLongPipe()
        {
            ShopManager.Instance?.SelectLongPipeTool();
            RefreshState();
        }

        private void SelectBreakPipe()
        {
            ShopManager.Instance?.SelectBreakPipeTool();
            RefreshState();
        }

        private void ClearTool()
        {
            ShopManager.Instance?.ClearPathTool();
            RefreshState();
        }

        private void RefreshState()
        {
            var shop = ShopManager.Instance;
            var board = GameMaster.Instance ? GameMaster.Instance.pathBuildBoard : null;
            var activeTool = board ? board.ActiveTool : PathBuildTool.None;
            var activePiece = board ? board.ActivePiece : null;

            ApplyButton(shortPipeButton,
                activeTool == PathBuildTool.Place && activePiece is { Length: 2 },
                shop && shop.CanSelectShortPipeTool);
            ApplyButton(longPipeButton,
                activeTool == PathBuildTool.Place && activePiece is { Length: 3 },
                shop && shop.CanSelectLongPipeTool);
            ApplyButton(breakPipeButton,
                activeTool == PathBuildTool.Break,
                shop && shop.CanSelectBreakPipeTool);
            ApplyButton(clearToolButton,
                activeTool == PathBuildTool.None,
                true);

            // R only rotates while a pipe piece is armed, so dim the hint otherwise.
            if (rotateHintLabel)
            {
                var hintColor = rotateHintLabel.color;
                hintColor.a = activeTool == PathBuildTool.Place && activePiece != null ? 1f : 0.35f;
                rotateHintLabel.color = hintColor;
            }
        }

        /// <summary>Drives a switch's lit/idle face and its label colour + dimming.</summary>
        private void ApplyButton(Button button, bool selected, bool interactable)
        {
            if (!button) return;
            button.interactable = interactable;

            if (!_styles.TryGetValue(button, out var style)) return;

            var colors = button.colors;
            colors.normalColor = selected ? style.AccentFace : style.IdleFace;
            colors.highlightedColor = selected ? style.AccentFace : style.HoverFace;
            colors.selectedColor = colors.normalColor;
            button.colors = colors;

            if (style.Label)
            {
                var labelColor = selected ? style.EngagedText : style.IdleText;
                labelColor.a = interactable ? 1f : 0.35f;
                style.Label.color = labelColor;
            }
        }

        /// <summary>
        /// Caches the prefab switch's resting colours plus its armed accent so
        /// <see cref="ApplyButton"/> can swap between idle and lit states.
        /// </summary>
        private void RegisterAssignedStyle(Button button, Color accent)
        {
            if (!button || _styles.ContainsKey(button)) return;

            var label = button.GetComponentInChildren<TMP_Text>(true);
            var colors = button.colors;
            _styles[button] = new ButtonStyle
            {
                IdleFace = colors.normalColor,
                HoverFace = colors.highlightedColor,
                AccentFace = accent,
                IdleText = label ? label.color : textColor,
                EngagedText = inkColor,
                Label = label
            };
        }
    }
}
