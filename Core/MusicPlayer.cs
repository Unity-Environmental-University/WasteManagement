using System;
using UnityEngine;

namespace _project.Scripts.Core
{
    /// <summary>
    ///     Looping background music. Survives non-additive scene loads so the track keeps
    ///     playing unbroken across the menu/game boundary, and a second instance loading
    ///     into a new scene destroys itself rather than stacking a second copy of the
    ///     track on top of the first. Fades on unscaled time so a modal pause (which
    ///     freezes <see cref="GameSpeed" />) can't strand a fade half-finished.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MusicPlayer : MonoBehaviour
    {
        private const string MutedKey = "Music.Muted";

        [Header("Track")] [SerializeField] private AudioClip track;

        [Header("Mix")]
        [Tooltip("Playback volume of the music bed. Kept low so it sits under gameplay audio.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float volume = 0.22f;

        [Tooltip("Seconds to ramp up from silence when the music starts.")] [SerializeField] [Range(0f, 10f)]
        private float fadeInDuration = 2f;

        private AudioSource _source;
        private float _fadeElapsed;

        public static MusicPlayer Instance { get; private set; }

        /// <summary>
        ///     Raised whenever the mute state changes, and once when the player wakes up with a
        ///     restored preference — so a toggle loading into a fresh scene can show the right
        ///     label without polling.
        /// </summary>
        public static event Action<bool> MuteChanged;

        /// <summary>Whether the music is silenced. Persists across sessions.</summary>
        public static bool IsMuted { get; private set; }

        /// <summary>The mix level the music fades toward. Set this from a volume slider.</summary>
        public float Volume
        {
            get => volume;
            set
            {
                volume = Mathf.Clamp01(value);
                // A live change past a finished fade should apply immediately, not wait for the next fade.
                if (_source && _fadeElapsed >= fadeInDuration) _source.volume = volume;
            }
        }

        private void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _source = GetComponent<AudioSource>();
            // Configured here rather than on the prefab so the music can never come back
            // one-shot, positional, or at full volume through an inspector edit.
            _source.clip = track;
            _source.loop = true;
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.volume = 0f;

            // Mute is silence, not a stop: the track keeps running underneath so unmuting
            // drops back into the music where it would have been rather than restarting it.
            IsMuted = PlayerPrefs.GetInt(MutedKey, 0) == 1;
            _source.mute = IsMuted;
        }

        private void Start()
        {
            if (!track)
            {
                Debug.LogWarning($"{nameof(MusicPlayer)} has no track assigned; nothing to play.", this);
                return;
            }

            _source.Play();
            MuteChanged?.Invoke(IsMuted);
        }

        private void Update()
        {
            if (_fadeElapsed >= fadeInDuration) return;

            _fadeElapsed += Time.unscaledDeltaTime;
            var t = fadeInDuration > 0f ? Mathf.Clamp01(_fadeElapsed / fadeInDuration) : 1f;
            _source.volume = volume * t;
        }

        /// <summary>Silences or restores the music and remembers the choice for next session.</summary>
        public void SetMuted(bool muted)
        {
            IsMuted = muted;
            if (_source) _source.mute = muted;

            PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
            PlayerPrefs.Save();

            MuteChanged?.Invoke(muted);
        }

        public void ToggleMute()
        {
            SetMuted(!IsMuted);
        }

        private void OnValidate()
        {
            // Let the inspector slider be audible while scrubbing it in play mode.
            if (Application.isPlaying && _source && _fadeElapsed >= fadeInDuration) _source.volume = volume;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
