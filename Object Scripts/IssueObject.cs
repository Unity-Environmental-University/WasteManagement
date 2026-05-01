using System;
using System.Collections.Generic;
using _project.Scripts.Core;
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

        private static bool Debugging => GameMaster.Instance?.debugging ?? false;
        
        private Vector3 _baseScale;
        private readonly HashSet<EntityId> _siftersProcessed = new();
        private const float BaseProcessCost = 1f;
        private const float BaseSiftCost = 5f;
        private Transform _startPoint;
        private int _waypointIndex;
        private bool _useDirectDestination;
        private Vector3 _directDestination;
        private static float PathHeight => GameMaster.Instance.pathBuildBoard.entityOnBoardHeight;

        private int Size { get; set; }
        public float SiftCost => BaseSiftCost * Size;
        public float ProcessCost => BaseProcessCost * Size;
        public bool IsDirectDestination => _useDirectDestination;

        private void Awake()
        {
            if (TryGetComponent<Rigidbody>(out var rb))
                rb.isKinematic = true;

            _baseScale = transform.localScale;
            Size = SetRandSize();
            transform.localScale = _baseScale * Size;
            SetMaterialColor();
        }

        /// <summary>
        /// Per-frame movement along the assigned WaypointPath.
        /// Advances waypoint-by-waypoint: moves toward the current target and increments
        /// the index once close enough. When the index exceeds the path length, the
        /// issue has reached the goal and triggers ReachEnd().
        /// </summary>
        private void Update()
        {
            if (_useDirectDestination)
            {
                transform.position = Vector3.MoveTowards(transform.position, _directDestination, moveSpeed * Time.deltaTime);

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

            // ADVANCE: If within ~0.1 units of the waypoint (0.01 squared), snap to next waypoint
            // Using sqrMagnitude avoids an expensive sqrt — compare squared distances instead
            if (Vector3.SqrMagnitude(transform.position - target) < 0.01f)
                _waypointIndex++;
        }

        private void SetMaterialColor()
        {
            var mat = Size switch
            {
                1 => Color.red,
                2 => Color.deepSkyBlue,
                3 => Color.softGreen,
                _ => throw new ArgumentOutOfRangeException()
            };
            gameObject.GetComponent<Renderer>().material.color = mat;
        }

        private static int SetRandSize() => Random.Range(1, 4);

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
        /// Reduces size by <paramref name="power"/>. Read <see cref="SiftCost"/> BEFORE
        /// calling this — _size is mutated immediately and SiftCost reflects the post-process value.
        /// </summary>
        public void Process(int power, string processLabel)
        {
            Size = Mathf.Max(0, Size - power);
            transform.localScale = _baseScale * Size;

            if (Debugging)
                Debug.Log($"[IssueObject] {processLabel} — remaining size: {Size}");

            if (Size <= 0)
            {
                Destroy(gameObject);
                return;
            }
            SetMaterialColor();
        }

        /// <summary>
        /// Returns true and marks this sifter as having processed this issue.
        /// Returns false if this sifter already processed it (e.g., compound trigger colliders).
        /// </summary>
        public bool TryRegisterSifter(EntityId sifterId) => _siftersProcessed.Add(sifterId);

        public void SetSize(int s)
        {
            Size = Mathf.Max(0, s);
            transform.localScale = _baseScale * Size;
        }

        public IssueType GetIssueType() => type;
        public void SetType(IssueType t) => type = t;
        public WaypointPath GetPath() => path;

        public void SetPath(WaypointPath p)
        {
            path = p;
            _useDirectDestination = false;
        }

        public void SetDirectDestination(Vector3 destination)
        {
            _directDestination = destination;
            _useDirectDestination = true;
            path = null;
        }

        public void SetMoveSpeed(float speed)
        {
            moveSpeed = Mathf.Max(0f, speed);
        }

        public static event Action<IssueObject> OnReachedEnd;

        private void ReachEnd()
        {
            if (Debugging)
                Debug.Log($"[IssueObject] Reached end — type: {type}");

            OnReachedEnd?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
