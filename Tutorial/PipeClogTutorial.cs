using _project.Scripts.Core;
using _project.Scripts.Object_Scripts;
using UnityEngine;

namespace _project.Scripts.Tutorial
{
    /// <summary>
    ///     One-shot tutorial for the pipe-clog mechanic: the first time an issue grows too
    ///     big for its pipe and jams it (IssueObject.OnPipeBlockStarted), the game clock is
    ///     frozen through GameSpeed.Pause() and a modal popup demonstrates clicking the
    ///     clog apart. Freezing the clock also holds the clog's burst countdown, so the
    ///     player never loses ground while reading. GameSpeed owns Time.timeScale, so
    ///     dismissing resumes at whatever base speed is then current (a fast-forward keeps
    ///     its own speed), and a scene reload can never inherit a stranded freeze. Shows
    ///     once per run — a retry re-arms it, matching TutorialManager's per-run behavior.
    /// </summary>
    public class PipeClogTutorial : MonoBehaviour
    {
        private static bool Debugging => GameMaster.Instance.debugging;
        [SerializeField] private PipeClogTutorialPanel panelPrefab;

        [Tooltip("Optional screenshot shown in the popup instead of the built-in animated click-the-clog demo.")]
        [SerializeField] private Sprite illustration;

        private bool _holdingPause;
        private PipeClogTutorialPanel _panel;
        private bool _shown;

        private void OnEnable()
        {
            IssueObject.OnPipeBlockStarted += HandlePipeBlockStarted;
        }

        private void OnDisable()
        {
            IssueObject.OnPipeBlockStarted -= HandlePipeBlockStarted;
            // Never leave the game frozen if this component dies while the popup is up.
            // Dismiss releases the pause off _holdingPause, not the panel reference, so
            // scene-teardown order (which may destroy the panel first) can't strand it.
            Dismiss();
        }

        private void HandlePipeBlockStarted(IssueObject issue)
        {
            if (_shown) return;
            _shown = true;

            if (!panelPrefab)
            {
                Debug.LogWarning($"{nameof(PipeClogTutorial)} is missing its panel prefab — first-clog popup skipped.",
                    this);
                return;
            }

            _panel = Instantiate(panelPrefab);
            if (!_panel.IsConfigured())
            {
                // An undismissable modal over a frozen game is worse than no tutorial —
                // validate before pausing so a broken prefab can't soft-lock the run.
                Debug.LogError($"{nameof(PipeClogTutorial)}'s panel prefab is missing references — popup skipped.",
                    this);
                Destroy(_panel.gameObject);
                _panel = null;
                return;
            }

            _holdingPause = true;
            GameSpeed.Pause();
            // Settle any in-flight camera rumble: the shake tween runs on scaled time, so
            // a freeze would otherwise park the camera at a mid-shake offset all popup long.
            GameMaster.Instance?.cameraController?.StopShake();

            _panel.SetIllustration(illustration);
            _panel.Dismissed += Dismiss;

            if (Debugging) Debug.Log("First pipe clog — tutorial popup shown, game paused.", this);
        }

        private void Dismiss()
        {
            if (_holdingPause)
            {
                _holdingPause = false;
                GameSpeed.Resume();
            }

            if (!_panel) return;
            Destroy(_panel.gameObject);
            _panel = null;

            if (Debugging) Debug.Log("Pipe-clog tutorial dismissed — game resumed.", this);
        }
    }
}
