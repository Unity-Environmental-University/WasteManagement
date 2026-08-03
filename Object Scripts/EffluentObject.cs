using _project.Scripts.Core;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    public class EffluentObject : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private WaypointPath path;
        [SerializeField] private Material cleanWaterMaterial;

        private int _waypointIndex;
        private int _routeIndex;

        private static float PathHeight => GameMaster.Instance.pathBuildBoard.entityOnBoardHeight;

        private void Awake()
        {
            if (TryGetComponent<Rigidbody>(out var rb))
                rb.isKinematic = true;

            if (cleanWaterMaterial && TryGetComponent<Renderer>(out var r))
                r.material = cleanWaterMaterial;
        }

        private void Update()
        {
            if (!path || _waypointIndex >= path.GetWaypointCount(_routeIndex))
            {
                ReachEnd();
                return;
            }

            var target = path.GetPosition(_routeIndex, _waypointIndex);
            target.y += transform.localScale.y * PathHeight;

            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);

            if (Vector3.SqrMagnitude(transform.position - target) < 0.01f)
                _waypointIndex++;
        }

        public void SetPath(WaypointPath p, int startIndex, int routeIndex = 0)
        {
            path = p;
            _waypointIndex = Mathf.Max(0, startIndex);
            _routeIndex = routeIndex == 1 && p && p.HasAlternateRoute ? 1 : 0;
        }

        public void SetMoveSpeed(float speed) => moveSpeed = Mathf.Max(0f, speed);

        private void ReachEnd()
        {
            OnEffluentReachedEnd();
            Destroy(gameObject);
        }

        // TODO: Future hook for treated-water payoff. When implemented, this is where
        // post-tank effluent delivers its result — e.g. route to a clean-water sink,
        // award score, heal the lake, or feed a downstream output network.
        private void OnEffluentReachedEnd() { }
    }
}
