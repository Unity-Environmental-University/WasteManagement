using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     Moves the duck: lazy figure-eights around <see cref="swimmingSpot" />, or a paddle back to
    ///     <see cref="shoreSpot" /> while <see cref="Retreating" />.
    /// </summary>
    public sealed class LakeDuckSwimmer : MonoBehaviour
    {
        [SerializeField] private Vector3 swimmingSpot;
        [SerializeField] private Vector3 shoreSpot;
        [SerializeField, Min(0.1f)] private float swimRadius = 3f;
        [SerializeField, Min(0.1f)] private float travelSpeed = 3f;
        [Tooltip("Degrees per second.")]
        [SerializeField, Min(1f)] private float turnSpeed = 90f;

        private float _clock;

        public bool Retreating { get; set; }

        /// <summary>False once the duck has reached the shore and sits still.</summary>
        public bool IsPaddling { get; private set; } = true;

        private void Update()
        {
            _clock += Time.deltaTime;

            var target = Retreating ? shoreSpot : swimmingSpot + new Vector3(
                Mathf.Sin(_clock * 0.32f) * swimRadius, 0f,
                Mathf.Sin(_clock * 0.64f) * swimRadius * 0.35f);
            var direction = target - transform.position;
            transform.position = Vector3.MoveTowards(transform.position, target, travelSpeed * Time.deltaTime);
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.04f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);

            IsPaddling = !Retreating || direction.sqrMagnitude > 0.1f;
        }
    }
}
