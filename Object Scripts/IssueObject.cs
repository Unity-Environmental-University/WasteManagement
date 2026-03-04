using System;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    public class IssueObject : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 2f;
        private WaypointPath _path;

        private Transform _startPoint;
        private int _waypointIndex;

        private void Awake()
        {
            if (TryGetComponent<Rigidbody>(out var rb))
                rb.isKinematic = true;
        }

        private void Update()
        {
            if (!_path || _waypointIndex >= _path.Count)
            {
                ReachEnd();
                return;
            }

            var target = _path.GetPosition(_waypointIndex);
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);

            if (Vector3.SqrMagnitude(transform.position - target) < 0.01f)
                _waypointIndex++;
        }

        public void SetPath(WaypointPath path)
        {
            _path = path;
        }

        public static event Action OnReachedEnd;

        private void ReachEnd()
        {
            OnReachedEnd?.Invoke();
            Destroy(gameObject);
        }
    }
}