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
    ///     HUD window for tuning one <see cref="PathSplitter" />, styled as a companion to the
    ///     PIPELINE control panel: a slider sets the share of issues each lane receives, and a
    ///     row of switches per issue type either shares that type by the slider or pins it to
    ///     the main route (cyan) or the branch (amber).
    ///     Clicking a splitter's model opens the window on it; CLOSE or Escape dismisses it.
    ///     The root stays active so it can watch for those clicks — only the window toggles.
    /// </summary>
    public class PathSplitterPanel : MonoBehaviour, IPointerDownHandler
    {
        // The slider moves in whole steps of this many percent.
        private const int SharePercentStep = 5;

        [Serializable]
        private struct TypeRow
        {
            public IssueType type;
            public Button splitButton;
            public Button mainButton;
            public Button branchButton;
        }

        [SerializeField] private GameObject window;
        [SerializeField] private Button closeButton;

        [Tooltip("Whole steps of 5%; its value is the main route's share.")]
        [SerializeField] private Slider shareSlider;
        [SerializeField] private TMP_Text mainShareText;
        [SerializeField] private TMP_Text branchShareText;
        [SerializeField] private TypeRow[] typeRows = Array.Empty<TypeRow>();

        [Header("Palette")]
        [SerializeField] private Color mainColor = new(0.169f, 0.769f, 0.839f, 1f);    // lit: main route
        [SerializeField] private Color branchColor = new(0.914f, 0.635f, 0.235f, 1f);  // lit: branch
        [SerializeField] private Color neutralColor = new(0.792f, 0.831f, 0.847f, 1f); // lit: split
        [SerializeField] private Color idleFaceColor = new(0.130f, 0.187f, 0.244f, 1f);
        [SerializeField] private Color hoverFaceColor = new(0.166f, 0.231f, 0.297f, 1f);
        [SerializeField] private Color inkColor = new(0.055f, 0.078f, 0.094f, 1f);     // text on a lit switch
        [SerializeField] private Color textColor = new(0.914f, 0.945f, 0.953f, 1f);    // text on an idle switch

        [Tooltip("Tint on the splitter the open window is tuning, so it stands out from the others.")]
        [SerializeField] private Color targetTint = new(0.85f, 0.60f, 0.22f, 1f);

        public static PathSplitterPanel Instance { get; private set; }

        /// <summary>The splitter being tuned, or null while the window is closed.</summary>
        public PathSplitter Target { get; private set; }

        public bool IsOpen => Target;

        private void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (closeButton) closeButton.onClick.AddListener(Hide);
            if (shareSlider) shareSlider.onValueChanged.AddListener(HandleShareChanged);
            foreach (var row in typeRows)
            {
                var type = row.type;
                if (row.splitButton) row.splitButton.onClick.AddListener(() => SetRule(type, SplitterRule.Split));
                if (row.mainButton) row.mainButton.onClick.AddListener(() => SetRule(type, SplitterRule.MainOnly));
                if (row.branchButton)
                    row.branchButton.onClick.AddListener(() => SetRule(type, SplitterRule.BranchOnly));
            }

            if (window) window.SetActive(false);
        }

        private void Update()
        {
            // The splitter was destroyed or disabled while its window was open.
            if (window && window.activeSelf && (!Target || !Target.isActiveAndEnabled))
                Hide();

            // Tuning is a setup-phase job; close the window when the tower phase begins.
            var turnController = GameMaster.Instance ? GameMaster.Instance.turnController : null;
            if (IsOpen && turnController && turnController.currentPhase != GamePhase.Card)
                Hide();

            // The Remove tool's hover tint would fight the target tint; it can't tune anyway.
            var board = GameMaster.Instance ? GameMaster.Instance.pathBuildBoard : null;
            if (IsOpen && board && board.ActiveTool == PathBuildTool.Break)
                Hide();

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
        ///     Splitters are tuned during setup (the Card phase), never during the tower phase. A press
        ///     only selects one while the pointer isn't removing pipe or utilities or dragging the view.
        ///     An armed palette tool doesn't block it: palette tools stay armed after every placement,
        ///     and a splitter's slot is occupied, so the press can't place anything there anyway.
        /// </summary>
        private static bool CanPickSplitters()
        {
            var gm = GameMaster.Instance;
            if (!gm) return true;
            if (!gm.turnController || gm.turnController.currentPhase != GamePhase.Card) return false;
            var tool = gm.pathBuildBoard ? gm.pathBuildBoard.ActiveTool : PathBuildTool.None;
            if (tool == PathBuildTool.Break || tool == PathBuildTool.Pan) return false;
            return !gm.cameraController || !gm.cameraController.IsPanArmed;
        }

        private void HandleShareChanged(float steps)
        {
            if (!Target) return;

            Target.MainSharePercent = Mathf.RoundToInt(steps) * SharePercentStep;
            RefreshShare();
        }

        private void SetRule(IssueType type, SplitterRule rule)
        {
            if (!Target) return;

            Target.SetRule(type, rule);
            RefreshRules();
        }

        private void Refresh()
        {
            if (shareSlider)
                shareSlider.SetValueWithoutNotify(Mathf.Round((float)Target.MainSharePercent / SharePercentStep));

            RefreshShare();
            RefreshRules();
        }

        private void RefreshShare()
        {
            if (mainShareText) mainShareText.text = $"{Target.MainSharePercent}%";
            if (branchShareText) branchShareText.text = $"{100 - Target.MainSharePercent}%";
        }

        private void RefreshRules()
        {
            foreach (var row in typeRows)
            {
                var rule = Target.GetRule(row.type);
                ApplySwitch(row.splitButton, rule == SplitterRule.Split, neutralColor);
                ApplySwitch(row.mainButton, rule == SplitterRule.MainOnly, mainColor);
                ApplySwitch(row.branchButton, rule == SplitterRule.BranchOnly, branchColor);
            }
        }

        /// <summary>Drives a switch's lit/idle face and label colour, as the PIPELINE panel does.</summary>
        private void ApplySwitch(Button button, bool lit, Color accent)
        {
            if (!button) return;

            var colors = button.colors;
            colors.normalColor = lit ? accent : idleFaceColor;
            colors.highlightedColor = lit ? accent : hoverFaceColor;
            colors.selectedColor = colors.normalColor;
            button.colors = colors;

            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label) label.color = lit ? inkColor : textColor;
        }
    }
}
