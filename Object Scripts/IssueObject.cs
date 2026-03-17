using System;
using UnityEngine;

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
        
        private const float BaseProcessCost = 1f;
        private Transform _startPoint;
        private int _waypointIndex;

        public float processCost = BaseProcessCost;

        private void Awake()
        {
            if (TryGetComponent<Rigidbody>(out var rb))
                rb.isKinematic = true;
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

        public void AssignType()
        {
            var rand = UnityEngine.Random.Range(0, 2);

            type = rand switch
            {
                0 => IssueType.Organic,
                1 => IssueType.Chemical,
                _ => IssueType.Chemical
            };
        }
        
        public IssueType GetIssueType() => type;

        public void SetPath(WaypointPath p) => path = p;

        public static event Action OnReachedEnd;

        private void ReachEnd()
        {
            OnReachedEnd?.Invoke();
            Destroy(gameObject);
        }
    }
}