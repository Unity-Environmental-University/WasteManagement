using System;
using System.Collections.Generic;
using _project.Scripts.Core;
using DG.Tweening;
using UnityEngine;
using Random = UnityEngine.Random;

namespace _project.Scripts.Object_Scripts
{
    public enum IssueType
    {
        Organic,
        Chemical
    }
    
    public class IssueObject : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private IssueType type;
        [SerializeField] private WaypointPath path;
        [SerializeField] private Renderer issueRenderer;
        [SerializeField, Min(3)] private int maxMergeSize = 6;

        [Tooltip("At this size and above the issue is too large for the pipe — it stops moving and blocks the path.")]
        [SerializeField]
        [Min(2)]
        private int pipeBlockSize = 4;

        [Tooltip(
            "Clicks needed to break one size off a pipe-blocking issue. Keep clicking to shrink it back to a movable size.")]
        [SerializeField]
        [Min(1)]
        private int clicksPerShrink = 6;

        private static bool Debugging => GameMaster.Instance?.debugging ?? false;
        
        private Vector3 _baseScale;
        private readonly HashSet<EntityId> _siftersProcessed = new();
        private const float BaseProcessCost = 1f;
        private const float BaseSiftCost = 5f;
        private Transform _startPoint;
        private int _waypointIndex;
        private bool _canBePoppedByClick;
        private Vector3 _directDestination;
        private Color? _visualOverrideColor;
        private int _blockedClickCount;
        private Tween _trembleTween;
        private Tween _burstPulseTween;
        private static float PathHeight => GameMaster.Instance.pathBuildBoard.entityOnBoardHeight;

        private int Size { get; set; }
        public float SiftCost => BaseSiftCost * Size;
        public float ProcessCost => BaseProcessCost * Size;
        public bool IsDirectDestination { get; private set; }

        /// <summary>
        ///     True when this issue has grown to <see cref="pipeBlockSize" /> or beyond:
        ///     it is too large for the pipe, stops moving, and blocks the path in place.
        ///     Cleared automatically if sifting shrinks it back below the threshold.
        ///     Direct-destination issues (cesspit runaways) travel off-pipe and never block.
        /// </summary>
        public bool IsBlockingPipe => !IsDirectDestination && Size >= pipeBlockSize;

        public static int ActiveCount { get; private set; }

        private void OnEnable()
        {
            ActiveCount++;
        }

        private void OnDisable()
        {
            ActiveCount--;
        }

        private void Awake()
        {
            if (!TryGetComponent<Rigidbody>(out var rb))
                rb = gameObject.AddComponent<Rigidbody>();

            rb.isKinematic = true;
            rb.useGravity = false;

            foreach (var issueCollider in GetComponentsInChildren<Collider>())
                issueCollider.isTrigger = true;

            _baseScale = transform.localScale;
            SetSize(SetRandSize());
        }

        /// <summary>
        ///     Per-frame movement along the assigned WaypointPath.
        ///     Advances waypoint-by-waypoint: moves toward the current target and increments
        ///     the index once close enough. When the index exceeds the path length, the
        ///     issue has reached the goal and triggers ReachEnd().
        /// </summary>
        private void Update()
        {
            // BLOCKED: the issue is too large for the pipe — it sits in place, plugging the
            // path, until it either shrinks (sifting) or grows past maxMergeSize and breaks the pipe.
            if (IsBlockingPipe)
                return;

            if (IsDirectDestination)
            {
                transform.position =
                    Vector3.MoveTowards(transform.position, _directDestination, moveSpeed * Time.deltaTime);

                if (Vector3.SqrMagnitude(transform.position - _directDestination) < 0.01f)
                    ReachEnd();

                return;
            }

            // GUARD: If no path is assigned OR we've consumed all waypoints, we've reached the end
            if (!path || _waypointIndex >= path.Count)
            {
                ReachEnd();
                return;
            }

            // Fetch the world-space position of the current target waypoint
            var target = path.GetPosition(_waypointIndex);

            // Lift the target up so the issue rides ON TOP of the pipe instead of inside it
            // (scaled by this issue's size so bigger issues sit higher)
            target.y += transform.localScale.y * PathHeight;

            // Move toward the target at moveSpeed units/second (frame-rate independent)
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);

            // ADVANCE: If within ~0.1 units of the waypoint (0.01 squared), snap to the next waypoint.
            // Using sqrMagnitude avoids an expensive sqrt — compare squared distances instead
            if (Vector3.SqrMagnitude(transform.position - target) < 0.01f)
                _waypointIndex++;
        }

        private void SetMaterialColor()
        {
            var renderer = GetIssueRenderer();
            if (!renderer) return;

            renderer.material.color = _visualOverrideColor ?? Size switch
            {
                1 => Color.red,
                2 => Color.deepSkyBlue,
                3 => Color.softGreen,
                _ => Color.Lerp(Color.softGreen, Color.magenta, Mathf.InverseLerp(4f, maxMergeSize, Size))
            };
        }

        private void OnMouseDown()
        {
            var turnController = GameMaster.Instance?.turnController;
            if (!turnController || turnController.currentPhase != GamePhase.Tower) return;

            // Pipe blockages are clicked apart instead of popped — see HandleBlockedClick.
            if (IsBlockingPipe)
            {
                HandleBlockedClick();
                return;
            }

            if (_canBePoppedByClick)
                Destroy(gameObject);
        }

        /// <summary>
        ///     Player clicks chip away at a pipe blockage: each click trembles the issue as
        ///     feedback, and every <see cref="clicksPerShrink" /> clicks knock one size off it.
        ///     Once it shrinks below <see cref="pipeBlockSize" /> (via SetSize) the pipe unclogs
        ///     and the issue resumes moving.
        /// </summary>
        private void HandleBlockedClick()
        {
            PlayTremble();

            _blockedClickCount++;
            if (_blockedClickCount < clicksPerShrink) return;

            _blockedClickCount = 0;
            SetSize(Size - 1);
        }

        /// <summary>Short position shake for click feedback while the issue blocks the pipe.</summary>
        private void PlayTremble()
        {
            // Complete (not just kill) any running shake so the position resets before the next one.
            _trembleTween?.Kill(true);
            _trembleTween = transform.DOShakePosition(0.3f, transform.localScale.x * 0.12f, 20, fadeOut: true);
        }

        /// <summary>
        ///     "Ready to burst" telegraph while blocking the pipe: a looping scale pulse that
        ///     grows faster and larger the closer Size gets to maxMergeSize (the burst point).
        ///     Stopped and the true size-scale restored when the issue is no longer blocking.
        /// </summary>
        private void RefreshBurstPulse()
        {
            _burstPulseTween?.Kill();
            _burstPulseTween = null;
            // Restore the honest scale so the pulse (or normal movement) starts from it.
            ApplySizeVisuals();

            if (!IsBlockingPipe) return;

            var burstProximity = Mathf.InverseLerp(pipeBlockSize, maxMergeSize, Size);
            var pulseScale = Mathf.Lerp(1.08f, 1.2f, burstProximity);
            var pulseDuration = Mathf.Lerp(0.6f, 0.25f, burstProximity);

            _burstPulseTween = transform.DOScale(_baseScale * (Size * pulseScale), pulseDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        private void OnDestroy()
        {
            // Kill all tweens targeting this transform so loops don't outlive the object.
            transform.DOKill();
        }

        private static int SetRandSize()
        {
            return Random.Range(1, 4);
        }

        public void AssignType()
        {
            var rand = Random.Range(0, 2);

            type = rand switch
            {
                0 => IssueType.Organic,
                1 => IssueType.Chemical,
                _ => IssueType.Chemical
            };
        }

        /// <summary>
        ///     Reduces size by <paramref name="power" />. Read <see cref="SiftCost" /> BEFORE
        ///     calling this — _size is mutated immediately and SiftCost reflects the post-process value.
        /// </summary>
        public void Process(int power, string processLabel)
        {
            // SetSize handles visuals and unclogs the pipe if this drops below pipeBlockSize.
            SetSize(Size - power);

            if (Debugging)
                Debug.Log($"[IssueObject] {processLabel} — remaining size: {Size}");

            if (Size <= 0)
                Destroy(gameObject);
        }

        /// <summary>
        ///     Returns true and marks this sifter as having processed this issue.
        ///     Returns false if this sifter already processed it (e.g., compound trigger colliders).
        /// </summary>
        public bool TryRegisterSifter(EntityId sifterId)
        {
            return _siftersProcessed.Add(sifterId);
        }

        public void SetSize(int s)
        {
            Size = Mathf.Max(0, s);
            SetMaterialColor();
            // UpdatePipeBlockState → RefreshBurstPulse re-applies the size scale.
            UpdatePipeBlockState();
        }

        /// <summary>
        ///     Syncs the blocked-state feedback after Size or path mode changes: the ready-to-burst
        ///     pulse runs only while blocking, and click progress/tremble are cleared when the
        ///     pipe unclogs.
        /// </summary>
        private void UpdatePipeBlockState()
        {
            if (!IsBlockingPipe)
            {
                _blockedClickCount = 0;
                // Complete the shake so the position resets — a live shake would fight Update() movement.
                _trembleTween?.Kill(true);
                _trembleTween = null;
            }

            RefreshBurstPulse();
        }

        public IssueType GetIssueType()
        {
            return type;
        }

        public void SetType(IssueType t)
        {
            type = t;
        }

        public WaypointPath GetPath()
        {
            return path;
        }

        public int GetWaypointIndex()
        {
            return _waypointIndex;
        }

        public void SetPath(WaypointPath p)
        {
            path = p;
            IsDirectDestination = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            var otherIssue = other.GetComponentInParent<IssueObject>();
            if (CanMergeWith(otherIssue))
                Absorb(otherIssue);
        }

        public void SetDirectDestination(Vector3 destination)
        {
            _directDestination = destination;
            IsDirectDestination = true;
            path = null;
            // Off-pipe travel can't block the pipe — clear any jam state.
            UpdatePipeBlockState();
        }

        public void EnableClickPop()
        {
            _canBePoppedByClick = true;
        }

        public void SetMoveSpeed(float speed)
        {
            moveSpeed = Mathf.Max(0f, speed);
        }

        public void SetVisualOverride(Color color, Material material = null)
        {
            _visualOverrideColor = color;

            // Assign the material once here rather than in SetMaterialColor — re-assigning the
            // shared asset there would clone a fresh material instance on every repaint.
            var renderer = GetIssueRenderer();
            if (material && renderer)
                renderer.material = material;

            SetMaterialColor();
        }

        private Renderer GetIssueRenderer()
        {
            if (!issueRenderer)
                issueRenderer = GetComponent<Renderer>();

            return issueRenderer;
        }

        private bool CanMergeWith(IssueObject other)
        {
            if (!other || other == this) return false;
            if (!isActiveAndEnabled || !other.isActiveAndEnabled) return false;
            if (IsDirectDestination || other.IsDirectDestination) return false;
            if (!path || path != other.path) return false;

            return true;
        }

        private void Absorb(IssueObject other)
        {
            var mergedSize = Mathf.Max(Size, other.Size) + 1;

            moveSpeed = Mathf.Max(moveSpeed, other.moveSpeed);
            _waypointIndex = Mathf.Max(_waypointIndex, other._waypointIndex);
            transform.position = Vector3.Lerp(transform.position, other.transform.position, 0.5f);

            // Deactivate before the deferred Destroy so the absorbed issue fails the
            // isActiveAndEnabled merge check for the rest of this physics pass.
            other.gameObject.SetActive(false);
            Destroy(other.gameObject);

            // Growing past maxMergeSize bursts the pipe — the merged mass is too much for it.
            // Pin Size directly (lake damage reads ProcessCost) — SetSize's visuals/pulse
            // would be wasted work on an object BreakPipe destroys this frame.
            if (mergedSize > maxMergeSize)
            {
                Size = maxMergeSize;
                BreakPipe();
                return;
            }

            // An issue that merges up to pipeBlockSize stops in place and clogs the pipe;
            // SetSize starts its blocked-state feedback (burst pulse).
            SetSize(mergedSize);

            if (Debugging)
                Debug.Log($"[IssueObject] Merged issues — new size: {Size}, blocking pipe: {IsBlockingPipe}");
        }

        private void ApplySizeVisuals()
        {
            transform.localScale = _baseScale * Size;
        }

        public static event Action<IssueObject> OnReachedEnd;

        /// <summary>
        ///     Fired when a pipe-blocking issue grows past <see cref="maxMergeSize" /> and bursts
        ///     the pipe. EMPTY HOOK for the real broken-pipe system (destroying/disabling the pipe
        ///     segment, VFX, rerouting, etc.) — nothing subscribes to it yet.
        /// </summary>
        public static event Action<IssueObject> OnPipeBroken;

        /// <summary>
        ///     The pipe couldn't contain this issue any longer.
        ///     PLACEHOLDER BEHAVIOR: until the broken-pipe system exists, the burst spills straight
        ///     into the lake — we reuse the OnReachedEnd damage path (LakeController applies this
        ///     issue's damage) and then delete the issue object. Replace this once OnPipeBroken
        ///     has a real consumer.
        /// </summary>
        private void BreakPipe()
        {
            if (Debugging)
                Debug.Log($"[IssueObject] Pipe broken by oversized issue — size: {Size}");

            OnPipeBroken?.Invoke(this);

            // Placeholder: spill damages the lake via the existing reached-end path, then the issue is deleted.
            OnReachedEnd?.Invoke(this);
            Destroy(gameObject);
        }

        private void ReachEnd()
        {
            if (Debugging)
                Debug.Log($"[IssueObject] Reached end — type: {type}");

            OnReachedEnd?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
