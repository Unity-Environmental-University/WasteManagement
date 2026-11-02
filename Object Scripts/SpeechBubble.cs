using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     A cartoon speech bubble that pops up over a world object, faces the camera, and pops away
    ///     again, so a sound has something on screen to come from. The art lives in the Speech Bubble
    ///     prefab; drop it into another prefab and point <see cref="anchor" /> at what is talking.
    /// </summary>
    public sealed class SpeechBubble : MonoBehaviour
    {
        private const float PopInTime = 0.18f;
        private const float PopOutTime = 0.15f;

        private static readonly int ZTestMode = Shader.PropertyToID("unity_GUIZTestMode");

        [Tooltip("What the tail points at.")]
        [SerializeField] private Transform anchor;

        [Tooltip("How far above the anchor on screen the tail tip sits, nudged toward the camera so the "
                 + "anchor's own model doesn't cover it.")]
        [SerializeField, Min(0f)] private float lift = 1f;

        [SerializeField] private Transform art;
        [SerializeField] private SpriteRenderer balloon;
        [SerializeField] private TextMeshPro label;

        private float _balloonScale;
        private Vector2 _labelOffset;
        private float _shownAt;
        private float _hideAt;
        private float _shake;

        public bool IsShowing => art && art.gameObject.activeSelf;
        public string Text => label ? label.text : null;

        /// <summary>Points the bubble at <paramref name="target" />, <paramref name="height" /> above it on screen.</summary>
        public void Follow(Transform target, float height)
        {
            anchor = target;
            lift = height;
        }

        /// <summary>Pops the bubble up with <paramref name="text" />; a shaking bubble reads as shouting.</summary>
        public void Say(string text, Color textColor, float duration, bool shake = false)
        {
            label.text = text;
            label.color = textColor;
            _shake = shake ? 1f : 0f;
            _shownAt = Time.time;
            _hideAt = Time.time + Mathf.Max(PopInTime + PopOutTime, duration);
            art.gameObject.SetActive(true);
            LateUpdate();
        }

        private void Awake()
        {
            _balloonScale = balloon.transform.localScale.x;
            _labelOffset = label.transform.localPosition;
            // Drawn over the scenery like UI, so a bubble dipping behind the shore stays readable.
            // The shaders don't expose this as a material property, so it is set on the instances.
            balloon.material.SetFloat(ZTestMode, (float)CompareFunction.Always);
            label.fontMaterial.SetFloat(ZTestMode, (float)CompareFunction.Always);
            art.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!anchor || !IsShowing) return;

            var now = Time.time;
            if (now >= _hideAt)
            {
                art.gameObject.SetActive(false);
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
            var onScreen = view ? view.WorldToViewportPoint(anchor.position) : new Vector3(0.5f, 0.5f, 1f);
            var below = onScreen.y > 0.7f;
            var left = onScreen.x > 0.7f;
            transform.position = anchor.position + ((below ? -up : up) + back) * lift;
            if (view) transform.rotation = Quaternion.LookRotation(view.transform.forward, view.transform.up);
            balloon.transform.localScale = new Vector3(left ? -_balloonScale : _balloonScale,
                below ? -_balloonScale : _balloonScale, 1f);
            label.transform.localPosition = new Vector3(left ? -_labelOffset.x : _labelOffset.x,
                below ? -_labelOffset.y : _labelOffset.y, label.transform.localPosition.z);
            art.localScale = Vector3.one * scale;
            art.localRotation = Quaternion.Euler(0f, 0f, _shake * Mathf.Sin(now * 40f) * 6f);
        }

        private static float BackOut(float t)
        {
            const float overshoot = 2.2f;
            t -= 1f;
            return 1f + t * t * ((overshoot + 1f) * t + overshoot);
        }
    }
}
