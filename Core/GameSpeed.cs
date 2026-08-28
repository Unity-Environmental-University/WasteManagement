using UnityEngine;
using UnityEngine.SceneManagement;

namespace _project.Scripts.Core
{
    /// <summary>
    ///     Single owner of Time.timeScale. Gameplay speed (fast-forward) and modal pauses
    ///     (the first-clog tutorial) are separate axes: the base scale is what the game
    ///     runs at while unpaused, and a pause holds the clock at zero without disturbing
    ///     it — so stopping a fast-forward under a pause (or dismissing a pause under a
    ///     fast-forward) can never desync the two or unfreeze a modal early. Every
    ///     non-additive scene load resets to normal speed, unpaused, so a stale 4x or a
    ///     stranded freeze can never leak across a retry regardless of teardown order.
    ///     All writes to Time.timeScale must go through this class.
    /// </summary>
    public static class GameSpeed
    {
        private static int _pauseDepth;

        /// <summary>True while at least one pause is held (the clock is frozen at zero).</summary>
        public static bool IsPaused => _pauseDepth > 0;

        /// <summary>The speed the game runs at while unpaused (1 = normal).</summary>
        public static float BaseScale { get; private set; } = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void HookSceneLoads()
        {
            // Unsubscribe first so an editor domain reload can't double-subscribe.
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) Reset();
        }

        public static void SetBaseScale(float scale)
        {
            BaseScale = Mathf.Max(0.01f, scale);
            Apply();
        }

        public static void ResetBaseScale()
        {
            SetBaseScale(1f);
        }

        /// <summary>Freezes the clock. Pauses nest; each Pause needs a matching Resume.</summary>
        public static void Pause()
        {
            _pauseDepth++;
            Apply();
        }

        public static void Resume()
        {
            _pauseDepth = Mathf.Max(0, _pauseDepth - 1);
            Apply();
        }

        private static void Reset()
        {
            BaseScale = 1f;
            _pauseDepth = 0;
            Apply();
        }

        private static void Apply()
        {
            Time.timeScale = IsPaused ? 0f : BaseScale;
        }
    }
}