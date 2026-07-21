using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    /// Placement-slot utility that sprinkles lime over the pipeline. This is a scaffold: the
    /// prefab, model, and sprinkle animation are wired up, but the gameplay effect is not
    /// implemented yet. The hooks below mark where future behavior (e.g., deodorizing/neutralizing
    /// passing issues) should live, following the same shape as <see cref="WasteSifter"/> and
    /// <see cref="TreatmentTank"/>.
    /// </summary>
    public class LimeSprinkler : MonoBehaviour
    {
        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string sprinkleTrigger = "Sprinkle";

        private SpecialInteractController _slot;
        private int _infraValue;

        private void Awake()
        {
            if (!animator) animator = GetComponentInChildren<Animator>();
        }

        /// <summary>Called by <see cref="SpecialInteractController"/> when this utility is placed.</summary>
        public void SetSlot(SpecialInteractController slot, int infraValue = 0)
        {
            _slot = slot;
            _infraValue = infraValue;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.gameObject.CompareTag("IssueObject")) return;

            var issue = other.GetComponent<IssueObject>();
            if (issue == null) return;
            if (issue.IsDirectDestination) return; // ignore cesspit runaways
            if (!issue.TryRegisterSifter(GetEntityId())) return;

            ApplyEffect(issue);
        }

        // EFFECT HOOK: no gameplay effect yet. A future implementation would mutate the issue
        // here (e.g., reduce stink contribution, neutralize chemical process cost) and play the
        // sprinkle animation. Kept intentionally empty so the placeable can ship ahead of design.
        private void ApplyEffect(IssueObject issue)
        {
            PlaySprinkle();
        }

        // EFFECT HOOK: telegraphs an active sprinkle. The base sprinkle animation loops
        // continuously via a self-transition on the controller state, so this is only used if a
        // one-shot reaction clip is added later. The parameter is looked up defensively because
        // the shipped controller has no parameters — setting a missing trigger logs an error.
        private void PlaySprinkle()
        {
            if (!animator || string.IsNullOrEmpty(sprinkleTrigger)) return;
            if (!HasTrigger(sprinkleTrigger)) return;

            animator.SetTrigger(sprinkleTrigger);
        }

        private bool HasTrigger(string parameterName)
        {
            foreach (var parameter in animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == parameterName)
                    return true;
            }

            return false;
        }
    }
}
