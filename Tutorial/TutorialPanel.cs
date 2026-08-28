using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _project.Scripts.Tutorial
{
    /// <summary>
    ///     Overlay card for the path-building tutorial. The prefab
    ///     (Assets/_project/Prefabs/UI/TutorialPanel.prefab) owns the hierarchy, copy
    ///     styling, and layout — docked on the right beneath the PathToolBar on its own
    ///     high-sorting-order canvas; this script only binds references, drives the step
    ///     text, and animates the looping pipe-placement demo. Only the two buttons may
    ///     receive pointer input; every other graphic keeps raycasts off so board clicks
    ///     pass through (PointerUi.IsPointerOverInteractiveUi would otherwise swallow them).
    /// </summary>
    public class TutorialPanel : MonoBehaviour
    {
        /// <summary>Amber accent shared with TutorialManager's tool highlight strips.</summary>
        internal static readonly Color CautionColor = new(0.914f, 0.635f, 0.235f, 1f);

        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text stepLabel;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private TMP_Text hintLabel;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private TMP_Text nextButtonLabel;

        [Header("Demo Diagram")]
        [SerializeField] private GameObject demoRoot;
        [SerializeField] private RectTransform demoCursor;
        [SerializeField] private RectTransform demoFlowDot;
        [SerializeField] private RectTransform demoOutfall;
        [SerializeField] private RectTransform demoLake;

        [Tooltip("Pipe segment visuals in placement order; each hides until the demo cursor 'places' it.")]
        [SerializeField] private GameObject[] demoPipes;

        private Coroutine _demoLoop;

        public event Action NextRequested;
        public event Action SkipRequested;

        private void Awake()
        {
            if (skipButton) skipButton.onClick.AddListener(() => SkipRequested?.Invoke());
            if (nextButton) nextButton.onClick.AddListener(() => NextRequested?.Invoke());
        }

        /// <summary>
        ///     True when every reference the step flow dereferences is assigned. The old
        ///     code-built hierarchy guaranteed this by construction; a prefab edit can lose
        ///     a reference, and TutorialManager checks this up front so a broken prefab
        ///     fails loudly instead of throwing mid-step inside a phase-event dispatch.
        /// </summary>
        public bool IsConfigured()
        {
            if (!panelRoot || !stepLabel || !titleLabel || !bodyLabel || !hintLabel ||
                !skipButton || !nextButton || !nextButtonLabel ||
                !demoRoot || !demoCursor || !demoFlowDot || !demoOutfall || !demoLake)
                return false;

            if (demoPipes == null || demoPipes.Length == 0) return false;
            foreach (var pipe in demoPipes)
                if (!pipe)
                    return false;

            return true;
        }

        public void SetVisible(bool visible)
        {
            if (!panelRoot || panelRoot.activeSelf == visible) return;
            panelRoot.SetActive(visible);
            RestartDemoLoop();
        }

        /// <summary>Resets the hint, demo, and next button; steps opt back in afterward.</summary>
        public void ShowStep(int stepNumber, int stepCount, string title, string body)
        {
            stepLabel.text = $"STEP {stepNumber} OF {stepCount}";
            titleLabel.text = title;
            bodyLabel.text = body;
            SetHint(null);
            SetDemoVisible(false);
            nextButton.gameObject.SetActive(false);
        }

        /// <summary>Looping outfall-to-lake pipe placement animation; steps opt in per ShowStep.</summary>
        public void SetDemoVisible(bool visible)
        {
            demoRoot.SetActive(visible);
            RestartDemoLoop();
        }

        /// <summary>Amber status line under the body text; null/empty hides it.</summary>
        public void SetHint(string hint)
        {
            var show = !string.IsNullOrEmpty(hint);
            hintLabel.gameObject.SetActive(show);
            if (show) hintLabel.text = hint;
        }

        public void SetNextButton(string label)
        {
            nextButtonLabel.text = label;
            nextButton.gameObject.SetActive(true);
        }

        /// <summary>Runs the demo animation only while both the panel and diagram are shown.</summary>
        private void RestartDemoLoop()
        {
            if (_demoLoop != null)
            {
                StopCoroutine(_demoLoop);
                _demoLoop = null;
            }

            if (demoRoot && demoRoot.activeInHierarchy && isActiveAndEnabled)
                _demoLoop = StartCoroutine(DemoLoop());
        }

        private IEnumerator DemoLoop()
        {
            var placePause = new WaitForSeconds(0.32f);
            var placedPause = new WaitForSeconds(0.16f);
            var cyclePause = new WaitForSeconds(0.9f);
            var outfallPos = demoOutfall.anchoredPosition;
            var lakePos = demoLake.anchoredPosition;

            while (demoRoot && demoRoot.activeInHierarchy && isActiveAndEnabled)
            {
                foreach (var pipe in demoPipes) pipe.SetActive(false);
                demoFlowDot.gameObject.SetActive(false);
                demoCursor.gameObject.SetActive(true);

                foreach (var t in demoPipes)
                {
                    demoCursor.anchoredPosition = ((RectTransform)t.transform).anchoredPosition;
                    yield return placePause;
                    t.SetActive(true);
                    yield return placedPause;
                }

                demoCursor.gameObject.SetActive(false);
                yield return placedPause;

                demoFlowDot.gameObject.SetActive(true);
                for (var t = 0f; t < 1f; t += Time.deltaTime / 1.1f)
                {
                    demoFlowDot.anchoredPosition = Vector2.Lerp(outfallPos, lakePos, t);
                    yield return null;
                }

                demoFlowDot.anchoredPosition = lakePos;
                yield return cyclePause;
            }

            _demoLoop = null;
        }
    }
}
