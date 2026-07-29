using DG.Tweening;
using UnityEngine;

namespace _project.Scripts.Core
{
    public enum CameraView
    {
        Main,
        Secondary
    }

    public class CameraController : MonoBehaviour
    {
        [SerializeField] private Camera mainCamera;
        [SerializeField] private Camera secondaryCamera;

        [Header("Shake")] [SerializeField] private float shakeIntensity = 0.7f;
        [SerializeField] [Min(0.01f)] private float shakeDecayRate = 1f;

        private Tween _shakeTween;
        private Vector3 _shakeOrigin;

        private CameraView ActiveView =>
            secondaryCamera && secondaryCamera.gameObject.activeSelf ? CameraView.Secondary : CameraView.Main;

        public bool IsShaking => _shakeTween != null && _shakeTween.IsActive();

        private void OnDestroy()
        {
            StopShake();
        }

        public void Shake(float duration)
        {
            if (duration <= 0f || IsShaking) return;

            if (!mainCamera)
            {
                Debug.LogWarning("[CameraController] Missing main camera reference; cannot shake.");
                return;
            }

            _shakeOrigin = mainCamera.transform.localPosition;
            _shakeTween = mainCamera.transform
                .DOShakePosition(duration, shakeIntensity, fadeOut: false)
                .OnComplete(() => _shakeTween = null);
            _shakeTween.timeScale = shakeDecayRate;
        }

        public void StopShake()
        {
            if (_shakeTween == null) return;

            _shakeTween.Kill();
            _shakeTween = null;
            if (mainCamera)
                mainCamera.transform.localPosition = _shakeOrigin;
        }

        public void SwitchTo(CameraView view)
        {
            if (!mainCamera || !secondaryCamera)
            {
                Debug.LogWarning("[CameraController] Missing camera reference; cannot switch cameras.");
                return;
            }

            // A shake leaves the main camera offset; settle it before it is hidden or revealed.
            StopShake();

            mainCamera.gameObject.SetActive(view == CameraView.Main);
            secondaryCamera.gameObject.SetActive(view == CameraView.Secondary);
        }

        public Camera GetCurrentCamera()
        {
            return ActiveView == CameraView.Secondary ? secondaryCamera : mainCamera;
        }
    }
}
