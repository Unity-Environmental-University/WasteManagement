using System;
using System.Text;
using System.Text.RegularExpressions;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _project.Scripts.UI
{
    /// <summary>
    ///     Scene-resident world-space label showing the current state of the hovered utility.
    ///     One instance lives in the scene hierarchy and is shared by every utility; they reach it
    ///     through <see cref="Instance" /> and only provide text and an anchor. The root stays active
    ///     so the component is always visible in the hierarchy — only the visual canvas toggles.
    ///     Stats strings are "TITLE (STATUS)\nLabel: value\n..." — the title feeds the header, the
    ///     optional parenthesized status becomes a color-coded tag, and each "Label: value" line is
    ///     split across the label/value columns.
    /// </summary>
    public class UtilityHoverStatsPopup : MonoBehaviour
    {
        private static readonly Regex TitleWithStatus = new(@"^(.*?)\s*\((.+)\)\s*$", RegexOptions.Compiled);

        [Header("Visuals")] [SerializeField] private Canvas visualCanvas;

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private GameObject statusTag;
        [SerializeField] private Image statusTagBackground;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text labelsText;
        [SerializeField] private TMP_Text valuesText;

        [Header("Status colours")] [SerializeField]
        private Color activeStatusColor = new(0.169f, 0.769f, 0.839f);

        [SerializeField] private Color fullStatusColor = new(0.914f, 0.635f, 0.235f);
        [SerializeField] private Color sealedStatusColor = new(0.494f, 0.549f, 0.58f);
        [SerializeField] private Color defaultStatusColor = new(0.914f, 0.945f, 0.953f);

        [Header("Placement")] [SerializeField] private Vector3 worldOffset = new(0f, 1.15f, 0f);

        [SerializeField] private float fadeDuration = 0.12f;

        [Header("Screen-size scaling")]
        [Tooltip(
            "Height of the popup, as a fraction of the screen height, when its content is referenceHeight tall. Held constant for any camera distance, FOV, or projection.")]
        [SerializeField]
        private float screenHeightFraction = 0.10f;

        [Tooltip(
            "Unscaled world height the fraction refers to. Fixed rather than measured so text stays the same size as the panel grows with content.")]
        [SerializeField]
        private float referenceHeight = 0.576f;

        [SerializeField] private float minScale = 0.5f;
        [SerializeField] private float maxScale = 40f;
        private readonly StringBuilder _labels = new();
        private readonly StringBuilder _values = new();

        private Tween _fade;
        private bool _visible;
        public static UtilityHoverStatsPopup Instance { get; private set; }

        /// <summary>The utility currently being displayed, or null while hidden.</summary>
        private Transform CurrentAnchor { get; set; }

        private void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (!visualCanvas) visualCanvas = GetComponentInChildren<Canvas>(true);
            if (!canvasGroup && visualCanvas) canvasGroup = visualCanvas.GetComponent<CanvasGroup>();

            if (visualCanvas) visualCanvas.gameObject.SetActive(false);
            if (canvasGroup) canvasGroup.alpha = 0f;
        }

        private void LateUpdate()
        {
            if (!CurrentAnchor)
            {
                // The anchored utility was destroyed or disabled without a pointer exit.
                if (_visible) Hide();
                return;
            }

            RefreshTransform();
        }

        private void OnDestroy()
        {
            _fade?.Kill();
            if (Instance == this) Instance = null;
        }

        public void Show(Transform anchor, string stats)
        {
            CurrentAnchor = anchor;
            SetStats(stats);
            RefreshTransform();

            if (_visible) return;
            _visible = true;

            if (visualCanvas) visualCanvas.gameObject.SetActive(true);
            _fade?.Kill();
            if (canvasGroup) _fade = FadeTo(1f);
        }

        public void SetStats(string stats)
        {
            var lines = string.IsNullOrEmpty(stats) ? Array.Empty<string>() : stats.Split('\n');

            ApplyTitle(lines.Length > 0 ? lines[0] : string.Empty);

            _labels.Clear();
            _values.Clear();
            for (var i = 1; i < lines.Length; i++)
            {
                if (i > 1)
                {
                    _labels.Append('\n');
                    _values.Append('\n');
                }

                var line = lines[i];
                var colon = line.IndexOf(':');
                if (colon >= 0)
                {
                    _labels.Append(line, 0, colon);
                    _values.Append(line, colon + 1, line.Length - colon - 1);
                }
                else
                {
                    _labels.Append(line);
                }
            }

            if (labelsText) labelsText.text = _labels.ToString();
            if (valuesText) valuesText.text = _values.ToString().Trim();
        }

        private void ApplyTitle(string rawTitle)
        {
            var match = TitleWithStatus.Match(rawTitle);
            var title = match.Success ? match.Groups[1].Value : rawTitle;
            var status = match.Success ? match.Groups[2].Value.Trim() : string.Empty;

            if (titleText) titleText.text = title.Trim();

            var hasStatus = status.Length > 0;
            if (statusTag) statusTag.SetActive(hasStatus);
            if (!hasStatus) return;

            var color = status.ToUpperInvariant() switch
            {
                "ACTIVE" => activeStatusColor,
                "FULL" => fullStatusColor,
                "SEALED" => sealedStatusColor,
                _ => defaultStatusColor
            };

            if (statusText)
            {
                statusText.text = status;
                statusText.color = color;
            }

            if (statusTagBackground)
                statusTagBackground.color = new Color(color.r, color.g, color.b, 0.18f);
        }

        /// <summary>Hides the popup, but only if it is currently showing <paramref name="anchor" />.</summary>
        public void Hide(Transform anchor)
        {
            if (CurrentAnchor == anchor) Hide();
        }

        private void Hide()
        {
            CurrentAnchor = null;
            if (!_visible) return;
            _visible = false;

            _fade?.Kill();
            if (canvasGroup)
                _fade = FadeTo(0f).OnComplete(() =>
                {
                    if (visualCanvas) visualCanvas.gameObject.SetActive(false);
                });
            else if (visualCanvas)
                visualCanvas.gameObject.SetActive(false);
        }

        // Core DOTween tween; CanvasGroup.DOFade needs the UI module, which this project doesn't enable.
        private Tween FadeTo(float alpha)
        {
            return DOTween.To(() => canvasGroup.alpha, a => canvasGroup.alpha = a, alpha, fadeDuration)
                .SetUpdate(true);
        }

        private void RefreshTransform()
        {
            transform.position = CurrentAnchor.position + worldOffset;

            var cam = Camera.main;
            if (!cam) return;
            var cameraTransform = cam.transform;

            // Screen-aligned billboard: matches the camera plane exactly, so the popup
            // never skews under the wide-FOV top-down card-phase camera.
            transform.rotation = Quaternion.LookRotation(cameraTransform.forward, cameraTransform.up);

            // Visible world height at the popup's depth, for either projection.
            var distance = Vector3.Distance(cameraTransform.position, transform.position);
            var frustumHeight = cam.orthographic
                ? cam.orthographicSize * 2f
                : 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);

            var scale = frustumHeight * screenHeightFraction / Mathf.Max(0.01f, referenceHeight);
            transform.localScale = Vector3.one * Mathf.Clamp(scale, minScale, maxScale);
        }
    }
}