using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _project.Scripts.Tutorial
{
    /// <summary>
    ///     Code-built overlay card for the path-building tutorial, styled after the
    ///     PathToolBar industrial palette (dark steel, cyan flow accent, amber caution).
    ///     Docked on the right beneath the PathToolBar, clear of the board, stink meter,
    ///     and Next Phase button, with an optional looping pipe-placement demo diagram.
    ///     Lives on its own high-sorting-order canvas. Only the two buttons receive
    ///     pointer input; every other graphic has raycasts off so board clicks pass
    ///     through (PointerUi.IsPointerOverInteractiveUi would otherwise swallow them).
    /// </summary>
    public class TutorialPanel : MonoBehaviour
    {
        private const int DemoCellCount = 6;
        private const float DemoCellSpacing = 32f;
        private static readonly Color PanelColor = new(0.055f, 0.078f, 0.094f, 0.94f);
        private static readonly Color FlowColor = new(0.169f, 0.769f, 0.839f, 1f);
        internal static readonly Color CautionColor = new(0.914f, 0.635f, 0.235f, 1f);
        private static readonly Color InkColor = new(0.055f, 0.078f, 0.094f, 1f);
        private static readonly Color TextColor = new(0.914f, 0.945f, 0.953f, 1f);
        private TMP_Text _bodyLabel;
        private RectTransform _demoCursor;
        private RectTransform _demoFlowDot;
        private Vector2 _demoLakePos;
        private Coroutine _demoLoop;
        private Vector2 _demoOutfallPos;
        private GameObject[] _demoPipes;

        private GameObject _demoRoot;
        private TMP_Text _hintLabel;
        private Button _nextButton;
        private TMP_Text _nextButtonLabel;
        private GameObject _panelRoot;

        private TMP_Text _stepLabel;
        private TMP_Text _titleLabel;

        public event Action NextRequested;
        public event Action SkipRequested;

        /// <summary>Builds the canvas and panel hierarchy and returns the controller.</summary>
        public static TutorialPanel Create()
        {
            var canvasGo = new GameObject("TutorialCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = canvasGo.AddComponent<TutorialPanel>();
            panel.BuildHierarchy();
            return panel;
        }

        public void SetVisible(bool visible)
        {
            if (!_panelRoot || _panelRoot.activeSelf == visible) return;
            _panelRoot.SetActive(visible);
            RestartDemoLoop();
        }

        /// <summary>Resets the hint, demo, and next button; steps opt back in afterward.</summary>
        public void ShowStep(int stepNumber, int stepCount, string title, string body)
        {
            _stepLabel.text = $"STEP {stepNumber} OF {stepCount}";
            _titleLabel.text = title;
            _bodyLabel.text = body;
            SetHint(null);
            SetDemoVisible(false);
            _nextButton.gameObject.SetActive(false);
        }

        /// <summary>Looping outfall-to-lake pipe placement animation; steps opt in per ShowStep.</summary>
        public void SetDemoVisible(bool visible)
        {
            _demoRoot.SetActive(visible);
            RestartDemoLoop();
        }

        /// <summary>Amber status line under the body text; null/empty hides it.</summary>
        public void SetHint(string hint)
        {
            var show = !string.IsNullOrEmpty(hint);
            _hintLabel.gameObject.SetActive(show);
            if (show) _hintLabel.text = hint;
        }

        public void SetNextButton(string label)
        {
            _nextButtonLabel.text = label;
            _nextButton.gameObject.SetActive(true);
        }

        private void BuildHierarchy()
        {
            _panelRoot = new GameObject("TutorialPanel",
                typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            _panelRoot.transform.SetParent(transform, false);

            // Tucks into the free strip between the board's right edge and the stink
            // meter, directly under the PathToolBar it points the player at — keeps
            // the board, toolbar, meter, and Next Phase button all unobstructed.
            var rect = (RectTransform)_panelRoot.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-160f, -232f);
            rect.sizeDelta = new Vector2(300f, 0f);

            var background = _panelRoot.GetComponent<Image>();
            background.color = PanelColor;
            background.raycastTarget = false;

            var layout = _panelRoot.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = _panelRoot.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _stepLabel = CreateLabel(_panelRoot.transform, "StepLabel", 11f, FlowColor, FontStyles.Bold);
            _stepLabel.characterSpacing = 6f;
            _titleLabel = CreateLabel(_panelRoot.transform, "TitleLabel", 17f, TextColor, FontStyles.Bold);
            _bodyLabel = CreateLabel(_panelRoot.transform, "BodyLabel", 13f, TextColor, FontStyles.Normal);
            BuildDemoDiagram();
            _hintLabel = CreateLabel(_panelRoot.transform, "HintLabel", 12f, CautionColor, FontStyles.Italic);
            _hintLabel.gameObject.SetActive(false);

            var buttonRow = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            buttonRow.transform.SetParent(_panelRoot.transform, false);
            var rowLayout = buttonRow.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10f;
            rowLayout.childAlignment = TextAnchor.MiddleRight;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(buttonRow.transform, false);
            spacer.GetComponent<LayoutElement>().flexibleWidth = 1f;

            var skipButton = CreateButton(buttonRow.transform, "SkipButton", "SKIP TUTORIAL",
                new Color(1f, 1f, 1f, 0.06f), TextColor, 124f, out _);
            skipButton.onClick.AddListener(() => SkipRequested?.Invoke());

            _nextButton = CreateButton(buttonRow.transform, "NextButton", "NEXT",
                FlowColor, InkColor, 84f, out _nextButtonLabel);
            _nextButton.onClick.AddListener(() => NextRequested?.Invoke());
        }

        /// <summary>
        ///     Mini schematic of the build loop: outfall and lake nodes with a row of
        ///     empty cells between them. A hover cursor visits each cell and "places" a
        ///     pipe segment; once the row is linked, a flow dot sweeps outfall-to-lake.
        /// </summary>
        private void BuildDemoDiagram()
        {
            _demoRoot = new GameObject("DemoDiagram", typeof(RectTransform), typeof(LayoutElement));
            _demoRoot.transform.SetParent(_panelRoot.transform, false);
            _demoRoot.GetComponent<LayoutElement>().preferredHeight = 64f;

            var lakeColor = new Color(0.235f, 0.478f, 0.792f, 1f);
            var cellColor = new Color(1f, 1f, 1f, 0.06f);
            var labelColor = new Color(TextColor.r, TextColor.g, TextColor.b, 0.55f);

            var firstCellX = -(DemoCellCount - 1) * DemoCellSpacing * 0.5f;
            var outfallX = firstCellX - DemoCellSpacing;
            var lakeX = -outfallX;
            _demoOutfallPos = new Vector2(outfallX, 6f);
            _demoLakePos = new Vector2(lakeX, 6f);

            CreateDemoRect(_demoRoot.transform, "Outfall", _demoOutfallPos, new Vector2(24f, 24f), CautionColor);
            CreateDemoNodeLabel("OutfallLabel", outfallX, labelColor);
            CreateDemoRect(_demoRoot.transform, "Lake", _demoLakePos, new Vector2(24f, 24f), lakeColor);
            CreateDemoNodeLabel("LakeLabel", lakeX, labelColor);

            // Build squares (matching the PlaceSpot slot tint) behind two route cells,
            // mirroring the two utilities the tutorial has the player install.
            var slotColor = new Color(0.223f, 0.837f, 0.858f, 0.55f);
            CreateDemoRect(_demoRoot.transform, "BuildSquareA",
                new Vector2(firstCellX + 1 * DemoCellSpacing, 6f), new Vector2(30f, 30f), slotColor);
            CreateDemoRect(_demoRoot.transform, "BuildSquareB",
                new Vector2(firstCellX + 4 * DemoCellSpacing, 6f), new Vector2(30f, 30f), slotColor);

            _demoPipes = new GameObject[DemoCellCount];
            for (var i = 0; i < DemoCellCount; i++)
            {
                var x = firstCellX + i * DemoCellSpacing;
                CreateDemoRect(_demoRoot.transform, $"Cell{i}", new Vector2(x, 6f), new Vector2(28f, 18f), cellColor);
                var pipe = CreateDemoRect(_demoRoot.transform, $"Pipe{i}", new Vector2(x, 6f), new Vector2(24f, 12f),
                    FlowColor);
                pipe.gameObject.SetActive(false);
                _demoPipes[i] = pipe.gameObject;
            }

            _demoCursor = CreateDemoRect(_demoRoot.transform, "Cursor",
                new Vector2(firstCellX, 6f), new Vector2(32f, 22f), new Color(1f, 1f, 1f, 0.22f));
            _demoFlowDot = CreateDemoRect(_demoRoot.transform, "FlowDot",
                _demoOutfallPos, new Vector2(10f, 10f), Color.white);
            _demoFlowDot.gameObject.SetActive(false);

            _demoRoot.SetActive(false);
        }

        private void CreateDemoNodeLabel(string nodeLabel, float x, Color color)
        {
            var label = CreateLabel(_demoRoot.transform, nodeLabel, 9f, color, FontStyles.Bold);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            var rect = (RectTransform)label.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, -16f);
            rect.sizeDelta = new Vector2(60f, 12f);
        }

        private static RectTransform CreateDemoRect(Transform parent, string name, Vector2 position, Vector2 size,
            Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        /// <summary>Runs the demo animation only while both the panel and diagram are shown.</summary>
        private void RestartDemoLoop()
        {
            if (_demoLoop != null)
            {
                StopCoroutine(_demoLoop);
                _demoLoop = null;
            }

            if (_demoRoot && _demoRoot.activeInHierarchy && isActiveAndEnabled)
                _demoLoop = StartCoroutine(DemoLoop());
        }

        private IEnumerator DemoLoop()
        {
            var placePause = new WaitForSeconds(0.32f);
            var placedPause = new WaitForSeconds(0.16f);
            var cyclePause = new WaitForSeconds(0.9f);

            while (_demoRoot && _demoRoot.activeInHierarchy && isActiveAndEnabled)
            {
                foreach (var pipe in _demoPipes) pipe.SetActive(false);
                _demoFlowDot.gameObject.SetActive(false);
                _demoCursor.gameObject.SetActive(true);

                foreach (var t in _demoPipes)
                {
                    _demoCursor.anchoredPosition = ((RectTransform)t.transform).anchoredPosition;
                    yield return placePause;
                    t.SetActive(true);
                    yield return placedPause;
                }

                _demoCursor.gameObject.SetActive(false);
                yield return placedPause;

                _demoFlowDot.gameObject.SetActive(true);
                for (var t = 0f; t < 1f; t += Time.deltaTime / 1.1f)
                {
                    _demoFlowDot.anchoredPosition = Vector2.Lerp(_demoOutfallPos, _demoLakePos, t);
                    yield return null;
                }

                _demoFlowDot.anchoredPosition = _demoLakePos;
                yield return cyclePause;
            }

            _demoLoop = null;
        }

        private static TMP_Text CreateLabel(Transform parent, string name, float size, Color color, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label,
            Color face, Color labelColor, float width, out TMP_Text labelText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = 30f;

            var image = go.GetComponent<Image>();
            image.color = face;
            image.raycastTarget = true;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            labelText = labelGo.AddComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 12f;
            labelText.fontStyle = FontStyles.Bold;
            labelText.color = labelColor;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.raycastTarget = false;

            return button;
        }
    }
}
