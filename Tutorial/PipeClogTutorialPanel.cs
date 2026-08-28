using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace _project.Scripts.Tutorial
{
    /// <summary>
    ///     Modal popup for the first pipe clog. The prefab
    ///     (Assets/_project/Prefabs/UI/PipeClogTutorialPanel.prefab) owns the entire
    ///     hierarchy, copy, and styling; this script only wires the dismiss button, swaps
    ///     in an optional screenshot, and animates the looping click-the-clog demo. It is
    ///     shown while the game is frozen (Time.timeScale = 0), so the demo runs on
    ///     unscaled/realtime waits. The prefab's dimming backdrop must stay a Button (even
    ///     a no-op one): a passive graphic would not count as interactive UI for
    ///     PointerUi.IsPointerOverInteractiveUi, and clicks would fall through to the
    ///     clogged issue behind the popup.
    /// </summary>
    public class PipeClogTutorialPanel : MonoBehaviour
    {
        [SerializeField] private Button gotItButton;

        [Header("Illustration")]
        [Tooltip("Disabled in the prefab; enabled in place of the animated demo when a screenshot is provided.")]
        [SerializeField] private Image illustrationImage;

        [Header("Demo Animation")]
        [SerializeField] private GameObject demoRoot;
        [SerializeField] private RectTransform blob;
        [SerializeField] private RectTransform blobRing;
        [SerializeField] private RectTransform cursor;

        [Tooltip("Where the unclogged blob flows off to (the demo's lake node).")]
        [SerializeField] private RectTransform flowTarget;

        public event Action Dismissed;

        private void Awake()
        {
            if (gotItButton) gotItButton.onClick.AddListener(() => Dismissed?.Invoke());
        }

        /// <summary>
        ///     True when the popup can actually be dismissed. Everything else degrades
        ///     gracefully (Start guards the demo refs, SetIllustration guards its image),
        ///     but a missing button would strand the player on a frozen game.
        /// </summary>
        public bool IsConfigured()
        {
            return gotItButton;
        }

        private void Start()
        {
            if (demoRoot && demoRoot.activeInHierarchy && blob && blobRing && cursor && flowTarget)
                StartCoroutine(DemoLoop());
        }

        /// <summary>Swaps the animated demo for a screenshot; null keeps the demo.</summary>
        public void SetIllustration(Sprite sprite)
        {
            if (!sprite || !illustrationImage) return;

            illustrationImage.sprite = sprite;
            illustrationImage.gameObject.SetActive(true);
            if (demoRoot) demoRoot.SetActive(false);
        }

        /// <summary>Blob and highlight diameters for demo sizes 1–3 (biggest = clogging).</summary>
        private void SetBlobSize(int size)
        {
            var diameter = 12f + size * 9f;
            blob.sizeDelta = new Vector2(diameter, diameter);
            blobRing.sizeDelta = new Vector2(diameter + 8f, diameter + 8f);
        }

        /// <summary>
        ///     Realtime waits and unscaled deltas throughout — the game clock is frozen at
        ///     timeScale 0 while this popup is up, so scaled waits would never elapse.
        /// </summary>
        private IEnumerator DemoLoop()
        {
            var beat = new WaitForSecondsRealtime(0.55f);
            var betweenClicks = new WaitForSecondsRealtime(0.28f);
            var home = blob.anchoredPosition;
            var exit = flowTarget.anchoredPosition;

            while (true)
            {
                SetBlobSize(3);
                blob.anchoredPosition = home;
                blobRing.gameObject.SetActive(true);
                cursor.gameObject.SetActive(true);
                yield return beat;

                // Two clicks per size knocked off, matching the in-game click-to-shrink feel.
                for (var size = 3; size > 1; size--)
                {
                    yield return ClickFlash();
                    yield return betweenClicks;
                    yield return ClickFlash();
                    SetBlobSize(size - 1);
                    yield return betweenClicks;
                }

                // Small enough to move again: highlight off, blob flows to the lake.
                blobRing.gameObject.SetActive(false);
                cursor.gameObject.SetActive(false);
                yield return betweenClicks;

                for (var t = 0f; t < 1f; t += Time.unscaledDeltaTime / 1.2f)
                {
                    blob.anchoredPosition = Vector2.Lerp(home, exit, t);
                    yield return null;
                }

                // The loop exits with t < 1 — land the blob on the lake for the final beat.
                blob.anchoredPosition = exit;
                yield return beat;
            }
            // ReSharper disable once IteratorNeverReturns — runs until the popup is destroyed.
        }

        /// <summary>One demo click: the cursor presses in while the blob trembles.</summary>
        private IEnumerator ClickFlash()
        {
            cursor.localScale = Vector3.one * 0.72f;

            var home = blob.anchoredPosition;
            var jitter = new WaitForSecondsRealtime(0.05f);
            for (var i = 0; i < 4; i++)
            {
                blob.anchoredPosition = home + new Vector2(i % 2 == 0 ? 2.5f : -2.5f, 0f);
                yield return jitter;
            }

            blob.anchoredPosition = home;
            cursor.localScale = Vector3.one;
        }
    }
}
