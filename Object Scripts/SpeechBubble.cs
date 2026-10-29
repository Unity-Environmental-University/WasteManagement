using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     A cartoon speech bubble that pops up over a world object, faces the camera, and pops away
    ///     again, so a sound has something on screen to come from. Builds its own art: a white
    ///     balloon with an ink outline and a tail, drawn into a texture once and shared.
    /// </summary>
    public sealed class SpeechBubble : MonoBehaviour
    {
        private const int TextureWidth = 256;
        private const int TextureHeight = 192;
        private const float PopInTime = 0.18f;
        private const float PopOutTime = 0.15f;

        private static readonly int ZTestMode = Shader.PropertyToID("unity_GUIZTestMode");
        private static Sprite _sprite;
        private static Material _overlay;

        private Transform _anchor;
        private float _lift;
        private Transform _art;
        private SpriteRenderer _balloon;
        private float _balloonScale;
        private Vector2 _labelOffset;
        private TextMeshPro _label;
        private float _shownAt;
        private float _hideAt;
        private float _shake;

        public bool IsShowing => _art && _art.gameObject.activeSelf;
        public string Text => _label ? _label.text : null;

        /// <summary>
        ///     Creates a hidden bubble whose tail points at <paramref name="anchor" /> from
        ///     <paramref name="lift" /> above it on screen, nudged toward the camera so the anchor's
        ///     own model doesn't cover it.
        /// </summary>
        public static SpeechBubble Create(Transform anchor, float lift, float width)
        {
            var bubble = new GameObject("Speech Bubble") { layer = anchor.gameObject.layer }
                .AddComponent<SpeechBubble>();
            bubble._anchor = anchor;
            bubble._lift = lift;
            bubble.Build(width);
            return bubble;
        }

        /// <summary>Pops the bubble up with <paramref name="text" />; a shaking bubble reads as shouting.</summary>
        public void Say(string text, Color textColor, float duration, bool shake = false)
        {
            _label.text = text;
            _label.color = textColor;
            _shake = shake ? 1f : 0f;
            _shownAt = Time.time;
            _hideAt = Time.time + Mathf.Max(PopInTime + PopOutTime, duration);
            _art.gameObject.SetActive(true);
            LateUpdate();
        }

        private void Build(float width)
        {
            _art = new GameObject("Art") { layer = gameObject.layer }.transform;
            _art.SetParent(transform, false);

            _balloon = new GameObject("Balloon") { layer = gameObject.layer }.AddComponent<SpriteRenderer>();
            _balloon.transform.SetParent(_art, false);
            _balloon.sprite = GetSprite();
            _balloon.sharedMaterial = GetOverlay();
            _balloon.sortingOrder = 50;
            _balloonScale = width / (TextureWidth / 100f);
            _balloon.transform.localScale = Vector3.one * _balloonScale;

            // The text fills the round part of the balloon (the pivot is the tail tip), a hair toward the camera.
            _label = new GameObject("Label") { layer = gameObject.layer }.AddComponent<TextMeshPro>();
            _label.transform.SetParent(_art, false);
            _labelOffset = new Vector2(width * 0.22f, width * 0.42f);
            _label.transform.localPosition = new Vector3(_labelOffset.x, _labelOffset.y, -0.02f);
            _label.rectTransform.sizeDelta = new Vector2(width * 0.72f, width * 0.34f);
            _label.alignment = TextAlignmentOptions.Center;
            _label.fontStyle = FontStyles.Bold;
            _label.enableAutoSizing = true;
            _label.fontSizeMin = 1f;
            _label.fontSizeMax = 40f;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.sortingOrder = 51;
            // Drawn over the scenery like UI, so a bubble dipping behind the shore stays readable.
            _label.fontMaterial.SetFloat(ZTestMode, (float)CompareFunction.Always);

            _art.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!_anchor)
            {
                Destroy(gameObject);
                return;
            }

            if (!IsShowing) return;

            var now = Time.time;
            if (now >= _hideAt)
            {
                _art.gameObject.SetActive(false);
                return;
            }

            // Overshoot on the way in, shrink on the way out.
            var popIn = Mathf.Clamp01((now - _shownAt) / PopInTime);
            var popOut = Mathf.Clamp01((_hideAt - now) / PopOutTime);
            var scale = Mathf.Min(BackOut(popIn), popOut);

            // Near the top or right of the screen the bubble flips below or to the left, so it stays
            // in view; the tail keeps pointing back at the anchor.
            var view = Camera.main;
            var up = view ? view.transform.up : Vector3.up;
            var back = view ? -view.transform.forward : Vector3.zero;
            var onScreen = view ? view.WorldToViewportPoint(_anchor.position) : new Vector3(0.5f, 0.5f, 1f);
            var below = onScreen.y > 0.7f;
            var left = onScreen.x > 0.7f;
            transform.position = _anchor.position + ((below ? -up : up) + back) * _lift;
            if (view) transform.rotation = Quaternion.LookRotation(view.transform.forward, view.transform.up);
            _balloon.transform.localScale = new Vector3(left ? -_balloonScale : _balloonScale,
                below ? -_balloonScale : _balloonScale, 1f);
            _label.transform.localPosition = new Vector3(left ? -_labelOffset.x : _labelOffset.x,
                below ? -_labelOffset.y : _labelOffset.y, -0.02f);
            _art.localScale = Vector3.one * scale;
            _art.localRotation = Quaternion.Euler(0f, 0f, _shake * Mathf.Sin(now * 40f) * 6f);
        }

        private static float BackOut(float t)
        {
            const float overshoot = 2.2f;
            t -= 1f;
            return 1f + t * t * ((overshoot + 1f) * t + overshoot);
        }

        private static Material GetOverlay()
        {
            if (_overlay) return _overlay;

            _overlay = new Material(Shader.Find("UI/Default")) { name = "Speech Bubble Overlay" };
            _overlay.SetFloat(ZTestMode, (float)CompareFunction.Always);
            return _overlay;
        }

        private static Sprite GetSprite()
        {
            if (_sprite) return _sprite;

            const float outline = 9f;
            var ink = new Color(0.1f, 0.1f, 0.13f, 1f);
            var paper = Color.white;
            var pixels = new Color[TextureWidth * TextureHeight];

            // Balloon: an ellipse in the upper part, with a tail down to the lower left.
            var center = new Vector2(TextureWidth * 0.5f, TextureHeight * 0.6f);
            var radii = new Vector2(TextureWidth * 0.46f, TextureHeight * 0.36f);
            var tailA = new Vector2(TextureWidth * 0.36f, TextureHeight * 0.36f);
            var tailB = new Vector2(TextureWidth * 0.54f, TextureHeight * 0.32f);
            var tailTip = new Vector2(TextureWidth * 0.28f, TextureHeight * 0.04f);

            for (var y = 0; y < TextureHeight; y++)
            for (var x = 0; x < TextureWidth; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                var q = new Vector2((p.x - center.x) / radii.x, (p.y - center.y) / radii.y);
                var ellipse = (q.magnitude - 1f) * Mathf.Min(radii.x, radii.y);
                var distance = Mathf.Min(ellipse, Triangle(p, tailA, tailB, tailTip));

                var alpha = Mathf.Clamp01(0.5f - distance);
                var color = Color.Lerp(paper, ink, Mathf.Clamp01(distance + outline + 0.5f));
                color.a = alpha;
                pixels[y * TextureWidth + x] = color;
            }

            var texture = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false)
            {
                name = "Speech Bubble", wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels(pixels);
            texture.Apply();
            _sprite = Sprite.Create(texture, new Rect(0, 0, TextureWidth, TextureHeight),
                new Vector2(0.28f, 0.04f), 100f);
            return _sprite;
        }

        /// <summary>Signed distance from <paramref name="p" /> to a triangle; negative inside.</summary>
        private static float Triangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 e0 = b - a, e1 = c - b, e2 = a - c;
            Vector2 v0 = p - a, v1 = p - b, v2 = p - c;
            var pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
            var pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
            var pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
            var s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
            var d0 = new Vector2(pq0.sqrMagnitude, s * (v0.x * e0.y - v0.y * e0.x));
            var d1 = new Vector2(pq1.sqrMagnitude, s * (v1.x * e1.y - v1.y * e1.x));
            var d2 = new Vector2(pq2.sqrMagnitude, s * (v2.x * e2.y - v2.y * e2.x));
            var d = Vector2.Min(Vector2.Min(d0, d1), d2);
            return -Mathf.Sqrt(d.x) * Mathf.Sign(d.y);
        }
    }
}
