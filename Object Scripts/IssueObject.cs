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

        public int Size { get; private set; }
        public float SiftCost => BaseSiftCost * Size;
        public float ProcessCost => BaseProcessCost * Size;

        private void Awake()
        {
            if (TryGetComponent<Rigidbody>(out var rb))
                rb.isKinematic = true;

            _baseScale = transform.localScale;
            Size = SetRandSize();
            transform.localScale = _baseScale * Size;
            SetMaterialColor();
        }

        private void Update()
        {
            if (!path || _waypointIndex >= path.Count)
            {
                ReachEnd();
                return;
            }

            var target = path.GetPosition(_waypointIndex);
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);

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

        private static int SetRandSize() => Random.Range(1, 3); 

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
        /// calling this — _size is mutated immediately and SiftCost reflects the post-sift value.
        /// </summary>
        public void Sift(int power)
        {
            Size = Mathf.Max(0, Size - power);
            transform.localScale = _baseScale * Size;

            if (Debugging)
                Debug.Log($"[IssueObject] Sifted — remaining size: {Size}");

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

        public void SetPath(WaypointPath p) => path = p;

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
