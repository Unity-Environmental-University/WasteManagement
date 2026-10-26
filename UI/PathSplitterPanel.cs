using System;
using _project.Scripts.Core;
using _project.Scripts.Object_Scripts;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace _project.Scripts.UI
{
    /// <summary>
    ///     Setup panel with numbered per-type routing switches and a ratio picker: a slider while the
    ///     splitter has one branch, and a triangle once it has two.
    /// </summary>
    public class PathSplitterPanel : MonoBehaviour, IPointerDownHandler
    {
        // The slider moves in whole steps of this many percent.
        private const int SharePercentStep = 5;

        // Opacity of switches for an output that has no pipe.
        private const float DimmedAlpha = 0.35f;

        [Serializable]
        private struct TypeRow
        {
            public IssueType type;
            public Button splitButton;
            public Button mainButton;
            public Button branchButton;
            public Button thirdButton;
        }

        [SerializeField] private GameObject window;
        [SerializeField] private Button closeButton;

        [Tooltip("Whole steps of 5%; its value is the main route's share.")]
        [SerializeField] private Slider shareSlider;
        [SerializeField] private SplitRatioTriangle ratioTriangle;
        [SerializeField] private TMP_Text thirdShareText;
        [SerializeField] private Button sortPresetButton;
        [SerializeField] private TMP_Text mainShareText;
        [SerializeField] private TMP_Text branchShareText;
        [SerializeField] private TMP_Text mainLabel;
        [SerializeField] private TMP_Text branchLabel;
        [SerializeField] private TMP_Text thirdLabel;
        [SerializeField] private TMP_Text caption;
        [SerializeField] private TypeRow[] typeRows = Array.Empty<TypeRow>();

        [Header("Palette")]
        [SerializeField] private Color neutralColor = new(0.792f, 0.831f, 0.847f, 1f); // lit: split
        [SerializeField] private Color idleFaceColor = new(0.130f, 0.187f, 0.244f, 1f);
        [SerializeField] private Color hoverFaceColor = new(0.166f, 0.231f, 0.297f, 1f);
        [SerializeField] private Color inkColor = new(0.055f, 0.078f, 0.094f, 1f);     // text on a lit switch
        [SerializeField] private Color textColor = new(0.914f, 0.945f, 0.953f, 1f);    // text on an idle switch

        [Tooltip("Tint on the splitter the open window is tuning, so it stands out from the others. " +
                 "Kept neutral so it can't be mistaken for an outlet color.")]
        [SerializeField, ColorUsage(false, true)] private Color targetTint = new(1.35f, 1.35f, 1.35f, 1f);

        public static PathSplitterPanel Instance { get; private set; }

        /// <summary>The splitter being tuned, or null while the window is closed.</summary>
        public PathSplitter Target { get; private set; }

        public bool IsOpen => Target;

        /// <summary>
        ///     The branch output (1 or 2) the slider trades against the main route, or -1 while both
        ///     branches exist and the triangle sets all three shares.
        /// </summary>
        public int SliderOutput { get; private set; } = 1;

        private void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (closeButton) closeButton.onClick.AddListener(Hide);
            if (mainShareText) mainShareText.color = PathSplitter.OutputColor(0);
            if (branchShareText) branchShareText.color = PathSplitter.OutputColor(1);
            if (thirdShareText) thirdShareText.color = PathSplitter.OutputColor(2);
            if (shareSlider) shareSlider.onValueChanged.AddListener(HandleShareChanged);
            if (ratioTriangle) ratioTriangle.Changed += HandleTriangleChanged;
            if (sortPresetButton) sortPresetButton.onClick.AddListener(() =>
            {
                if (!Target) return;
                Target.SetRule(IssueType.Organic, SplitterRule.MainOnly);
                Target.SetRule(IssueType.Chemical, SplitterRule.MainOnly);
                Target.SetRule(IssueType.NonWaste, SplitterRule.BranchOnly);
                Target.SetRule(IssueType.Metal, SplitterRule.ThirdOnly);
                RefreshRules();
            });
            foreach (var row in typeRows)
            {
                var type = row.type;
                if (row.splitButton) row.splitButton.onClick.AddListener(() => SetRule(type, SplitterRule.Split));
                if (row.mainButton) row.mainButton.onClick.AddListener(() => SetRule(type, SplitterRule.MainOnly));
                if (row.branchButton)
                    row.branchButton.onClick.AddListener(() => SetRule(type, SplitterRule.BranchOnly));
                if (row.thirdButton)
                    row.thirdButton.onClick.AddListener(() => SetRule(type, SplitterRule.ThirdOnly));
            }

            if (window) window.SetActive(false);
        }

        private void Update()
        {
            // The splitter was destroyed or disabled while its window was open.
            if (window && window.activeSelf && (!Target || !Target.isActiveAndEnabled))
                Hide();

            var gm = GameMaster.Instance;
            if (IsOpen && gm)
            {
                // The Remove tool's hover tint would fight the target tint; it can't tune anyway.
                var removing = gm.pathBuildBoard && gm.pathBuildBoard.ActiveTool == PathBuildTool.Break;
                if (removing) Hide();
            }

            // Pipe laid or removed while the window is open can add or drop an outlet.
            if (IsOpen && GetSliderOutput() != SliderOutput) Refresh();

            if (IsOpen && Keyboard.current != null && Keyboard.current[Key.Escape].wasPressedThisFrame)
                Hide();

            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;
            if (!CanPickSplitters() || PointerUi.IsPointerOverInteractiveUi()) return;

            var viewCamera = Camera.main;
            if (!viewCamera) return;

            var ray = viewCamera.ScreenPointToRay(pointer.position.ReadValue());
            PathSplitter picked = null;
            var pickedDistance = float.PositiveInfinity;
            foreach (var splitter in PathSplitter.Live)
            {
                if (!splitter.TryGetPointerDistance(ray, out var distance) || distance >= pickedDistance) continue;

                picked = splitter;
                pickedDistance = distance;
            }

            if (picked) Show(picked);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Deliberately empty: handling pointer-down marks the whole window as interactive UI, so
        // presses on its backdrop never fall through to the board beneath (see PointerUi).
        public void OnPointerDown(PointerEventData eventData)
        {
        }

        public void Show(PathSplitter splitter)
        {
            if (!splitter) return;

            if (Target && Target != splitter) Target.SetHighlight(null);
            Target = splitter;
            Target.SetHighlight(targetTint);
            if (window) window.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            if (Target) Target.SetHighlight(null);
            Target = null;
            if (window) window.SetActive(false);
        }

        /// <summary>
        ///     Splitters can be tuned in either phase, including mid-run: a new rule applies to the next
        ///     issue the splitter routes. A press only selects one while the pointer isn't removing pipe
        ///     or utilities or dragging the view.
        ///     An armed palette tool doesn't block it: palette tools stay armed after every placement,
        ///     and a splitter's slot is occupied, so the press can't place anything there anyway.
        /// </summary>
        private static bool CanPickSplitters()
        {
            var gm = GameMaster.Instance;
            if (!gm) return true;
            var tool = gm.pathBuildBoard ? gm.pathBuildBoard.ActiveTool : PathBuildTool.None;
            if (tool == PathBuildTool.Break || tool == PathBuildTool.Pan) return false;
            return !gm.cameraController || !gm.cameraController.IsPanArmed;
        }

        private void HandleTriangleChanged(Vector3 shares)
        {
            if (!Target) return;
            Target.SetShares(Mathf.RoundToInt(shares.x * 100), Mathf.RoundToInt(shares.z * 100));
            RefreshShare();
        }

        private void HandleShareChanged(float steps)
        {
            if (!Target || SliderOutput < 0) return;

            // The output without pipe gives up its share, so the slider's split is the real one.
            var main = Mathf.RoundToInt(steps) * SharePercentStep;
            Target.SetShares(main, SliderOutput == 2 ? 100 - main : 0);
            RefreshShare();
        }

        private int GetSliderOutput()
        {
            var branch = Target.HasOutlet(1);
            var third = Target.HasOutlet(2);
            if (branch && third) return -1;
            return third ? 2 : 1;
        }

        // Main's share of what the slider splits; the other output's share is unused while it
        // has no pipe, just as ChooseOutput ignores it.
        private int GetSliderMainPercent()
        {
            var main = Target.MainSharePercent;
            var other = Target.SharePercent(SliderOutput);
            return main + other == 0 ? 100 : Mathf.RoundToInt(main * 100f / (main + other));
        }

        private void SetRule(IssueType type, SplitterRule rule)
        {
            if (!Target) return;

            Target.SetRule(type, rule);
            RefreshRules();
        }

        private void Refresh()
        {
            SliderOutput = GetSliderOutput();
            var sliding = SliderOutput >= 0;
            if (ratioTriangle) ratioTriangle.gameObject.SetActive(!sliding);
            if (shareSlider)
            {
                shareSlider.gameObject.SetActive(sliding);
                if (sliding)
                {
                    if (shareSlider.fillRect && shareSlider.fillRect.TryGetComponent<Graphic>(out var fill))
                        fill.color = PathSplitter.OutputColor(0);
                    var background = shareSlider.transform.Find("Background");
                    if (background && background.TryGetComponent<Graphic>(out var track))
                        track.color = PathSplitter.OutputColor(SliderOutput);
                    shareSlider.SetValueWithoutNotify(Mathf.Round((float)GetSliderMainPercent() / SharePercentStep));
                }
            }

            if (caption)
                caption.text = (sliding
                                   ? $"Slide to split 1 and {SliderOutput + 1}. SHARE uses this split."
                                   : "Drag the dot: 3 at top, 1 left, 2 right. SHARE uses these ratios.") +
                               "\nMissing pinned output? Uses 1.";

            // Two readouts sit at the quarter marks; three split the width evenly.
            PlaceColumn(mainLabel, mainShareText, true, sliding ? 0.25f : 1f / 6f);
            PlaceColumn(branchLabel, branchShareText, SliderOutput != 2, sliding ? 0.75f : 0.5f);
            PlaceColumn(thirdLabel, thirdShareText, SliderOutput != 1, sliding ? 0.75f : 5f / 6f);

            RefreshShare();
            RefreshRules();
        }

        private static void PlaceColumn(TMP_Text label, TMP_Text share, bool shown, float x)
        {
            foreach (var text in new[] { label, share })
            {
                if (!text) continue;
                text.gameObject.SetActive(shown);
                var rect = text.rectTransform;
                rect.anchorMin = new Vector2(x, rect.anchorMin.y);
                rect.anchorMax = new Vector2(x, rect.anchorMax.y);
            }
        }

        private void RefreshShare()
        {
            if (SliderOutput >= 0)
            {
                var main = GetSliderMainPercent();
                if (mainShareText) mainShareText.text = $"{main}%";
                var other = SliderOutput == 2 ? thirdShareText : branchShareText;
                if (other) other.text = $"{100 - main}%";
                return;
            }

            if (mainShareText) mainShareText.text = $"{Target.MainSharePercent}%";
            if (branchShareText) branchShareText.text = $"{Target.BranchSharePercent}%";
            if (thirdShareText) thirdShareText.text = $"{Target.ThirdSharePercent}%";
            if (ratioTriangle) ratioTriangle.SetShares(Target.MainSharePercent, Target.BranchSharePercent, Target.ThirdSharePercent);
        }

        private void RefreshRules()
        {
            foreach (var row in typeRows)
            {
                var rule = Target.GetRule(row.type);
                ApplySwitch(row.splitButton, rule == SplitterRule.Split, neutralColor, true);
                ApplySwitch(row.mainButton, rule == SplitterRule.MainOnly, PathSplitter.OutputColor(0), true);
                ApplySwitch(row.branchButton, rule == SplitterRule.BranchOnly, PathSplitter.OutputColor(1),
                    SliderOutput != 2);
                ApplySwitch(row.thirdButton, rule == SplitterRule.ThirdOnly, PathSplitter.OutputColor(2),
                    SliderOutput != 1);
            }
        }

        /// <summary>
        ///     Drives a switch's lit/idle face and label colour, as the PIPELINE panel does. A switch
        ///     for an output with no pipe is dimmed but still shows whether it's lit.
        /// </summary>
        private void ApplySwitch(Button button, bool lit, Color accent, bool available)
        {
            if (!button) return;

            var colors = button.colors;
            colors.normalColor = lit ? accent : idleFaceColor;
            colors.highlightedColor = lit ? accent : hoverFaceColor;
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = colors.normalColor * new Color(1f, 1f, 1f, DimmedAlpha);
            button.colors = colors;
            button.interactable = available;

            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (!label) return;
            var labelColor = lit ? inkColor : textColor;
            if (!available) labelColor.a *= DimmedAlpha;
            label.color = labelColor;
        }
    }
}
