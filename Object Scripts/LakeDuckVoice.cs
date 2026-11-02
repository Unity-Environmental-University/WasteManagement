using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>Plays the duck's quack and says it in a speech bubble, in one of three tones.</summary>
    public sealed class LakeDuckVoice : MonoBehaviour
    {
        public enum Tone { Chipper, Muttering, Shouting }

        [Serializable]
        private struct Lines
        {
            public string[] text;
            public Color ink;
            [Tooltip("A shaking bubble reads as shouting.")]
            public bool shake;
        }

        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip quack;
        [SerializeField] private SpeechBubble bubble;
        [SerializeField, Min(0f)] private float bubbleDuration = 1.3f;

        [SerializeField] private Lines chipper = new()
        {
            text = new[] { "Quack!", "Quack quack!" }, ink = new Color(0.12f, 0.12f, 0.16f)
        };

        [SerializeField] private Lines muttering = new()
        {
            text = new[] { "quack..." }, ink = new Color(0.42f, 0.44f, 0.5f)
        };

        [SerializeField] private Lines shouting = new()
        {
            text = new[] { "QUACK!!", "Quack?!" }, ink = new Color(0.85f, 0.16f, 0.12f), shake = true
        };

        public void Quack(Tone tone)
        {
            if (source && quack) source.PlayOneShot(quack);
            if (!bubble) return;

            var lines = tone switch
            {
                Tone.Shouting => shouting,
                Tone.Muttering => muttering,
                _ => chipper
            };
            if (lines.text == null || lines.text.Length == 0) return;
            bubble.Say(lines.text[Random.Range(0, lines.text.Length)], lines.ink, bubbleDuration, lines.shake);
        }
    }
}
