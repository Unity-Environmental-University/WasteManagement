using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     Placement-slot utility that sprinkles lime over the pipeline. This is a scaffold: the
    ///     prefab, model, and sprinkle animation are wired up, but the gameplay effect is not
    ///     implemented yet. The hooks below mark where future behavior (e.g., deodorizing/neutralizing
    ///     passing issues) should live, following the same shape as <see cref="WasteSifter" /> and
    ///     <see cref="TreatmentTank" />.
    /// </summary>
    public class LimeSprinkler : MonoBehaviour
    {
        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string baseAnimationState = "Take 001";
        [SerializeField] private SkinnedMeshRenderer blendShapeRenderer;
        [SerializeField] private string blendShapeName = "limeSpreader_BS.pCube10";
        [SerializeField, Min(0.1f)] private float sprinkleDuration = 1f;
        [SerializeField, Range(0f, 100f)] private float sprinkleWeight = 100f;
        [SerializeField, Min(0.1f)] private float sprinkleInterval = 5f;
        [SerializeField, Min(0f)] private float sprinkleJitter = 0.5f;

        private int _infraValue;
        private SpecialInteractController _slot;
        private int _blendShapeIndex = -1;
        private float _timeUntilSprinkle;
        private float _sprinkleElapsed = -1f;
        private int _baseAnimationStateHash;

        private void Awake()
        {
            if (!animator) animator = GetComponentInChildren<Animator>();
            _baseAnimationStateHash = Animator.StringToHash(baseAnimationState);
            FindBlendShape();
        }

        private void OnEnable()
        {
            _timeUntilSprinkle = NextSprinkleDelay();
            _sprinkleElapsed = -1f;
        }

        private void LateUpdate()
        {
            KeepBaseAnimationLooping();
            if (!blendShapeRenderer || _blendShapeIndex < 0) return;

            _timeUntilSprinkle -= Time.deltaTime;
            if (_timeUntilSprinkle <= 0f)
            {
                _sprinkleElapsed = 0f;
                _timeUntilSprinkle = NextSprinkleDelay();
            }

            var weight = 0f;
            if (_sprinkleElapsed >= 0f)
            {
                _sprinkleElapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(_sprinkleElapsed / sprinkleDuration);
                weight = Mathf.Sin(progress * Mathf.PI) * sprinkleWeight;
                if (progress >= 1f) _sprinkleElapsed = -1f;
            }

            // Override the imported clip's curve for this specific shape before rendering.
            blendShapeRenderer.SetBlendShapeWeight(_blendShapeIndex, weight);
        }

        private void KeepBaseAnimationLooping()
        {
            if (!animator || !animator.enabled || _baseAnimationStateHash == 0) return;

            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.shortNameHash == _baseAnimationStateHash && state.normalizedTime >= 1f)
                animator.Play(_baseAnimationStateHash, 0, 0f);
        }

        private void FindBlendShape()
        {
            if (blendShapeRenderer && TryFindBlendShape(blendShapeRenderer)) return;

            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (TryFindBlendShape(renderer)) return;
        }

        private bool TryFindBlendShape(SkinnedMeshRenderer renderer)
        {
            if (!renderer.sharedMesh) return false;

            for (var i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
            {
                if (renderer.sharedMesh.GetBlendShapeName(i) != blendShapeName) continue;

                blendShapeRenderer = renderer;
                _blendShapeIndex = i;
                return true;
            }

            return false;
        }

        private float NextSprinkleDelay()
        {
            return Mathf.Max(0.1f, sprinkleInterval + Random.Range(-sprinkleJitter, sprinkleJitter));
        }

        /// <summary>Called by <see cref="SpecialInteractController" /> when this utility is placed.</summary>
        public void SetSlot(SpecialInteractController slot, int infraValue = 0)
        {
            _slot = slot;
            _infraValue = infraValue;
        }

        // EFFECT HOOK: no gameplay effect yet. A future implementation would mutate the issue
        // here (e.g., reduce stink contribution, neutralize chemical process cost) and play the
        // sprinkle animation. Kept intentionally empty so the placeable can ship ahead of design.
        private void ApplyEffect(IssueObject issue)
        {
        }
    }
}
