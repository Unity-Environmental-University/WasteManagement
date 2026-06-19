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
    /// If buttons are not wired in the Inspector, the panel builds itself at runtime.
    /// </summary>
    public class PathToolBar : MonoBehaviour
    {
        [SerializeField] private Button shortPipeButton;
        [SerializeField] private Button longPipeButton;
        [SerializeField] private Button breakPipeButton;
        [SerializeField] private Button clearToolButton;
        [SerializeField] private bool buildMockupIfUnassigned = true;

        [Header("Layout")]
        [SerializeField] private Vector2 anchoredPosition = new(24f, -128f);
        [SerializeField] private Vector2 buttonSize = new(106f, 50f);

        [Header("Palette")]
        [SerializeField] private Color panelColor = new(0.086f, 0.106f, 0.125f, 1f);      // zinc housing
        [SerializeField] private Color rimColor = new(0.173f, 0.224f, 0.259f, 1f);        // hairline / edge
        [SerializeField] private Color steelColor = new(0.251f, 0.302f, 0.345f, 1f);      // switch face (idle)
        [SerializeField] private Color steelHoverColor = new(0.322f, 0.373f, 0.420f, 1f); // switch face (hover)
        [SerializeField] private Color flowColor = new(0.169f, 0.769f, 0.839f, 1f);       // armed: lay pipe
        [SerializeField] private Color cautionColor = new(0.914f, 0.635f, 0.235f, 1f);    // armed/idle: remove
        [SerializeField] private Color neutralColor = new(0.792f, 0.831f, 0.847f, 1f);    // armed: cursor
        [SerializeField] private Color inkColor = new(0.055f, 0.078f, 0.094f, 1f);        // text on a lit switch
        [SerializeField] private Color textColor = new(0.914f, 0.945f, 0.953f, 1f);       // text on an idle switch
        [SerializeField] private Color mutedColor = new(0.494f, 0.549f, 0.580f, 1f);      // eyebrow label

        private struct ButtonStyle
        {
            public Color IdleFace;
            public Color HoverFace;
            public Color AccentFace; // lit when armed
            public Color IdleText;
            public Color EngagedText;
            public TMP_Text Label;
        }

        private readonly Dictionary<Button, ButtonStyle> _styles = new();
        private bool _isBound;

        private static Sprite _panelSprite;
        private static Sprite _buttonSprite;

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

        public void EnsureBuilt()
        {
            var hasMissingButtons = !shortPipeButton || !longPipeButton || !breakPipeButton || !clearToolButton;
            if (!hasMissingButtons)
            {
                // Designer-wired buttons: register styling metadata without rebuilding.
                RegisterAssignedStyle(shortPipeButton, flowColor);
                RegisterAssignedStyle(longPipeButton, flowColor);
                RegisterAssignedStyle(breakPipeButton, cautionColor);
                RegisterAssignedStyle(clearToolButton, neutralColor);
                return;
            }

            if (!buildMockupIfUnassigned) return;

            BuildPanel();
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

        // ---- panel construction ------------------------------------------------

        private void BuildPanel()
        {
            ConfigureRoot();
            var row = CreateButtonRow();

            if (!shortPipeButton) shortPipeButton = CreateButton(row, "PIPE", "2-CELL", flowColor, textColor);
            if (!longPipeButton) longPipeButton = CreateButton(row, "PIPE", "3-CELL", flowColor, textColor);
            if (!breakPipeButton) breakPipeButton = CreateButton(row, "REMOVE", null, cautionColor, cautionColor);
            if (!clearToolButton) clearToolButton = CreateButton(row, "CURSOR", null, neutralColor, textColor);
        }

        private void ConfigureRoot()
        {
            if (transform is not RectTransform rectTransform) return;

            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = anchoredPosition;

            var image = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            image.sprite = GetRoundedSprite(ref _panelSprite, 12);
            image.type = Image.Type.Sliced;
            image.color = panelColor;
            image.raycastTarget = true;

            var layout = GetComponent<VerticalLayoutGroup>() ?? gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 12);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = GetComponent<ContentSizeFitter>() ?? gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildHeader(rectTransform);
        }

        private void BuildHeader(Transform parent)
        {
            var header = new GameObject("Header", typeof(RectTransform));
            header.transform.SetParent(parent, false);
            var headerLayout = header.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 16f;
            headerLayout.minHeight = 16f;

            var eyebrow = new GameObject("Eyebrow", typeof(RectTransform), typeof(TextMeshProUGUI));
            var eyebrowRect = (RectTransform)eyebrow.transform;
            eyebrowRect.SetParent(header.transform, false);
            eyebrowRect.anchorMin = Vector2.zero;
            eyebrowRect.anchorMax = Vector2.one;
            eyebrowRect.offsetMin = new Vector2(2f, 0f);
            eyebrowRect.offsetMax = Vector2.zero;

            var eyebrowText = eyebrow.GetComponent<TextMeshProUGUI>();
            eyebrowText.text = "PIPELINE";
            eyebrowText.fontSize = 12f;
            eyebrowText.fontStyle = FontStyles.Bold;
            eyebrowText.characterSpacing = 10f;
            eyebrowText.color = mutedColor;
            eyebrowText.alignment = TextAlignmentOptions.Left;
            eyebrowText.enableWordWrapping = false;
            eyebrowText.raycastTarget = false;

            var rule = new GameObject("Rule", typeof(RectTransform), typeof(Image));
            var ruleRect = (RectTransform)rule.transform;
            ruleRect.SetParent(header.transform, false);
            ruleRect.anchorMin = new Vector2(0f, 0f);
            ruleRect.anchorMax = new Vector2(1f, 0f);
            ruleRect.pivot = new Vector2(0.5f, 0f);
            ruleRect.sizeDelta = new Vector2(0f, 1.5f);
            ruleRect.anchoredPosition = Vector2.zero;

            var ruleImage = rule.GetComponent<Image>();
            ruleImage.color = rimColor;
            ruleImage.raycastTarget = false;
        }

        private RectTransform CreateButtonRow()
        {
            var row = new GameObject("Buttons", typeof(RectTransform));
            var rowRect = (RectTransform)row.transform;
            rowRect.SetParent(transform, false);

            var rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = buttonSize.y;
            rowLayout.minHeight = buttonSize.y;

            var horizontal = row.AddComponent<HorizontalLayoutGroup>();
            horizontal.spacing = 8f;
            horizontal.childAlignment = TextAnchor.MiddleCenter;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = false;

            return rowRect;
        }

        private Button CreateButton(Transform parent, string main, string sub, Color accent, Color idleText)
        {
            var buttonObject = new GameObject($"{main} {sub} Button".Trim(), typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredWidth = buttonSize.x;
            layout.preferredHeight = buttonSize.y;

            var image = buttonObject.GetComponent<Image>();
            image.sprite = GetRoundedSprite(ref _buttonSprite, 8);
            image.type = Image.Type.Sliced;
            image.color = steelColor;
            image.raycastTarget = true;

            var button = buttonObject.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = steelColor;
            colors.highlightedColor = steelHoverColor;
            colors.pressedColor = Color.Lerp(steelColor, Color.black, 0.2f);
            colors.selectedColor = steelColor;
            colors.disabledColor = new Color(steelColor.r, steelColor.g, steelColor.b, 0.35f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.SetParent(buttonObject.transform, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.color = idleText;
            label.characterSpacing = 2f;
            label.fontSize = 17f;
            label.raycastTarget = false;
            label.text = string.IsNullOrEmpty(sub)
                ? $"<b>{main}</b>"
                : $"<b>{main}</b>\n<size=58%><alpha=#B0>{sub}</size>";

            _styles[button] = new ButtonStyle
            {
                IdleFace = steelColor,
                HoverFace = steelHoverColor,
                AccentFace = accent,
                IdleText = idleText,
                EngagedText = inkColor,
                Label = label
            };

            return button;
        }

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

        /// <summary>
        /// Builds (and caches) a 9-sliced rounded-rectangle sprite so the panel needs
        /// no imported art. Antialiased corners via per-pixel coverage.
        /// </summary>
        private static Sprite GetRoundedSprite(ref Sprite cache, int radius)
        {
            if (cache) return cache;

            var size = radius * 2 + 2;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                var nx = Mathf.Clamp(px, radius, size - radius);
                var ny = Mathf.Clamp(py, radius, size - radius);
                var distance = Mathf.Sqrt((px - nx) * (px - nx) + (py - ny) * (py - ny));
                var alpha = Mathf.Clamp01(radius - distance + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            cache = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            return cache;
        }
    }
}
