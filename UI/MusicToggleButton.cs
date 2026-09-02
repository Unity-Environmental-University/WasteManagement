using _project.Scripts.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _project.Scripts.UI
{
    /// <summary>
    ///     HUD switch that mutes the background music. The button and its label live in the
    ///     MusicToggleButton prefab; this component only binds the click and paints the
    ///     lit/dimmed state. State is read from <see cref="MusicPlayer" /> statics rather
    ///     than cached locally, so a toggle instantiated into a scene the music already
    ///     survived into (menu to gameplay) shows the correct label on its first frame.
    /// </summary>
    public class MusicToggleButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;

        [Header("Palette")]
        [SerializeField] private Color playingColor = new(0.169f, 0.769f, 0.839f, 1f); // lit: music on
        [SerializeField] private Color mutedColor = new(0.353f, 0.392f, 0.412f, 1f);   // dimmed: muted

        [Header("Labels")]
        [SerializeField] private string playingText = "MUSIC ON";
        [SerializeField] private string mutedText = "MUSIC OFF";

        private void Awake()
        {
            if (!button) button = GetComponent<Button>();
            if (!label) label = GetComponentInChildren<TMP_Text>();
        }

        private void OnEnable()
        {
            if (button) button.onClick.AddListener(Toggle);
            MusicPlayer.MuteChanged += Paint;
            Paint(MusicPlayer.IsMuted);
        }

        private void OnDisable()
        {
            if (button) button.onClick.RemoveListener(Toggle);
            MusicPlayer.MuteChanged -= Paint;
        }

        private void Toggle()
        {
            if (MusicPlayer.Instance)
                MusicPlayer.Instance.ToggleMute();
            else
                Debug.LogWarning($"{nameof(MusicToggleButton)} clicked with no {nameof(MusicPlayer)} in the scene.",
                    this);
        }

        private void Paint(bool muted)
        {
            if (label) label.text = muted ? mutedText : playingText;
            if (!button) return;

            var colors = button.colors;
            var face = muted ? mutedColor : playingColor;
            colors.normalColor = face;
            colors.selectedColor = face;
            colors.highlightedColor = Color.Lerp(face, Color.white, 0.2f);
            colors.pressedColor = Color.Lerp(face, Color.black, 0.2f);
            button.colors = colors;
        }
    }
}
