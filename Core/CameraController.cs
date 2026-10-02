using _project.Scripts.Object_Scripts;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

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

        [Header("Pan")]
        [Tooltip("How far the planning view may drift from center, as a fraction of the board's half-size.")]
        [SerializeField] [Range(0f, 1f)] private float panRange = 0.5f;
        [SerializeField] [Min(0f)] private float recenterDuration = 0.25f;

        private Tween _shakeTween;
        private Vector3 _shakeOrigin;

        private Tween _recenterTween;
        private Vector3 _panHome;
        private Vector3 _panGrabPoint;
        private bool _hasPanHome;
        private bool _isPanning;
        private InputAction _panPress;
        private Pointer _panPointer;

        private CameraView ActiveView =>
            secondaryCamera && secondaryCamera.gameObject.activeSelf ? CameraView.Secondary : CameraView.Main;

        public bool IsShaking => _shakeTween != null && _shakeTween.IsActive();

        /// <summary>True while the planning view sits away from its centered home position.</summary>
        public bool IsPanned => _hasPanHome && secondaryCamera &&
                                (secondaryCamera.transform.position - _panHome).sqrMagnitude > 0.0001f;

        /// <summary>
        ///     True while a press on the world drags the view: the pan tool is armed and the
        ///     planning camera is the one on screen.
        /// </summary>
        public bool IsPanArmed
        {
            get
            {
                var gm = GameMaster.Instance;
                var board = gm ? gm.pathBuildBoard : null;
                return board && board.ActiveTool == PathBuildTool.Pan && ActiveView == CameraView.Secondary;
            }
        }

        /// <summary>
        ///     Farthest the planning view may move from center on world X/Z. Scales with the
        ///     board, so a larger grid opens up more room to pan without retuning.
        /// </summary>
        private Vector2 PanLimit
        {
            get
            {
                var gm = GameMaster.Instance;
                var board = gm ? gm.pathBuildBoard : null;
                if (!board) return Vector2.zero;

                var pitch = board.CellWorldPitch;
                return new Vector2(board.Columns * pitch.x, board.Rows * pitch.y) * (0.5f * panRange);
            }
        }

        private void Awake()
        {
            _panPress = new InputAction("Pan Press", InputActionType.Button, "<Pointer>/press");
            _panPress.performed += BeginPan;
            _panPress.canceled += EndPan;
        }

        private void OnEnable() => _panPress.Enable();

        private void OnDisable()
        {
            StopPan();
            _panPress.Disable();
        }

        private void BeginPan(InputAction.CallbackContext context)
        {
            if (_isPanning || !IsPanArmed) return;
            var pointer = context.control.device as Pointer;
            if (pointer == null) return;
            if (PointerUi.IsPointerOverInteractiveUi()) return;
            if (!TryGetGroundPoint(pointer.position.ReadValue(), out _panGrabPoint)) return;

            _panPointer = pointer;
            _isPanning = true;
            InputSystem.onAfterUpdate += UpdatePan;
        }

        private void EndPan(InputAction.CallbackContext context) => StopPan();

        private void UpdatePan()
        {
            if (_panPointer == null || !_panPointer.added || !_panPointer.press.isPressed || !IsPanArmed)
            {
                StopPan();
                return;
            }

            DragPan(_panPointer.position.ReadValue());
        }

        private void StopPan()
        {
            if (!_isPanning) return;
            InputSystem.onAfterUpdate -= UpdatePan;
            _isPanning = false;
            _panPointer = null;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) StopPan();
        }

        private void OnDestroy()
        {
            StopPan();
            _panPress?.Dispose();
            StopShake();
            StopRecenter();
        }

        /// <summary>
        ///     Slides the planning view across the ground by a world-space X/Z delta, stopping at
        ///     the pan limit.
        /// </summary>
        public void PanBy(Vector2 delta)
        {
            if (!secondaryCamera) return;

            EnsurePanHome();
            StopRecenter();

            var limit = PanLimit;
            var cameraTransform = secondaryCamera.transform;
            var offset = cameraTransform.position - _panHome;
            offset.x = Mathf.Clamp(offset.x + delta.x, -limit.x, limit.x);
            offset.z = Mathf.Clamp(offset.z + delta.y, -limit.y, limit.y);
            cameraTransform.position = _panHome + offset;
        }

        /// <summary>Returns the planning view to its centered home position.</summary>
        public void Recenter()
        {
            StopPan();
            if (!IsPanned) return;

            StopRecenter();
            if (recenterDuration <= 0f)
            {
                secondaryCamera.transform.position = _panHome;
                return;
            }

            // Unscaled, so the view still glides home while the game is paused or sped up.
            _recenterTween = secondaryCamera.transform
                .DOMove(_panHome, recenterDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .OnComplete(() => _recenterTween = null);
        }

        private void DragPan(Vector2 screenPosition)
        {
            if (!TryGetGroundPoint(screenPosition, out var point)) return;

            // Move the camera by however far the grabbed ground point has slipped from the pointer.
            var slip = _panGrabPoint - point;
            PanBy(new Vector2(slip.x, slip.z));
        }

        private bool TryGetGroundPoint(Vector2 screenPosition, out Vector3 point)
        {
            point = default;
            if (!secondaryCamera) return false;

            var gm = GameMaster.Instance;
            var board = gm ? gm.pathBuildBoard : null;
            var ground = new Plane(Vector3.up, board ? board.transform.position : Vector3.zero);
            var ray = secondaryCamera.ScreenPointToRay(screenPosition);
            if (!ground.Raycast(ray, out var distance)) return false;

            point = ray.GetPoint(distance);
            return true;
        }

        // Captured on first use rather than in Awake, so it reads the camera's authored position
        // even when the camera reference is assigned after this component wakes.
        private void EnsurePanHome()
        {
            if (_hasPanHome) return;

            _panHome = secondaryCamera.transform.position;
            _hasPanHome = true;
        }

        private void StopRecenter()
        {
            if (_recenterTween == null) return;

            _recenterTween.Kill();
            _recenterTween = null;
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
            StopPan();

            mainCamera.gameObject.SetActive(view == CameraView.Main);
            secondaryCamera.gameObject.SetActive(view == CameraView.Secondary);
        }

        public Camera GetCurrentCamera()
        {
            return ActiveView == CameraView.Secondary ? secondaryCamera : mainCamera;
        }
    }
}
