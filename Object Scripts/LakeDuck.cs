using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     A cosmetic resident: lake health changes its mood, never the game rules. This is the duck's
    ///     brain; it decides when to sulk, hop or shout and hands the work to the prefab's other parts:
    ///     <see cref="LakeDuckSwimmer" /> moves it, the Animator poses it, <see cref="LakeDuckVoice" />
    ///     quacks and pops a speech bubble so the sound visibly comes from the duck.
    /// </summary>
    public sealed class LakeDuck : MonoBehaviour
    {
        private static readonly int PaddlingParam = Animator.StringToHash("Paddling");
        private static readonly int RetreatingParam = Animator.StringToHash("Retreating");
        private static readonly int HappyParam = Animator.StringToHash("Happy");
        private static readonly int UpsetParam = Animator.StringToHash("Upset");
        private static readonly int QuackParam = Animator.StringToHash("Quack");

        [SerializeField] private LakeController lake;
        [SerializeField] private LakeDuckSwimmer swimmer;
        [SerializeField] private LakeDuckVoice voice;
        [SerializeField] private Animator animator;

        [Tooltip("Retreats to the shore when lake health drops below this.")]
        [SerializeField, Range(0f, 100f)] private float retreatBelow = 50f;

        [Tooltip("Swims back out once lake health climbs back above this.")]
        [SerializeField, Range(0f, 100f)] private float returnAbove = 80f;

        [Tooltip("How long a hop or shake lasts; matches the reaction clips.")]
        [SerializeField, Min(0.1f)] private float reactionDuration = 2f;

        [SerializeField, Min(0f)] private float firstQuackDelay = 8f;
        [SerializeField, Min(0.1f)] private float happyQuackInterval = 16f;
        [SerializeField, Min(0.1f)] private float gloomyQuackInterval = 9f;

        private bool _retreating;
        private float _clock;
        private float _nextQuack;
        private float _reactionUntil;
        private float _previousHealth;
        private bool _hurtPending;

        private void Start()
        {
            if (!lake) lake = FindAnyObjectByType<LakeController>();
            if (lake) _previousHealth = lake.health;
            _nextQuack = firstQuackDelay;
        }

        private void Update()
        {
            if (!lake) return;
            _clock += Time.deltaTime;

            // A little hysteresis keeps the duck from changing its mind at the boundary.
            var retreat = _retreating ? lake.health < returnAbove : lake.health < retreatBelow;
            var dropped = lake.health < _previousHealth;
            _previousHealth = lake.health;
            if (retreat != _retreating)
            {
                _retreating = retreat;
                _hurtPending = false;
                React(retreat, retreat && dropped);
            }
            else if (dropped || _hurtPending)
            {
                // Damage mid-reaction waits for that reaction to finish instead of going unnoticed.
                _hurtPending = _clock < _reactionUntil;
                if (!_hurtPending) React(true, true);
            }

            if (swimmer) swimmer.Retreating = _retreating;
            if (animator)
            {
                animator.SetBool(PaddlingParam, !swimmer || swimmer.IsPaddling);
                animator.SetBool(RetreatingParam, _retreating);
            }

            if (_clock >= _nextQuack && _clock >= _reactionUntil) React(_retreating, false);
        }

        private void React(bool upset, bool hurt)
        {
            _reactionUntil = _clock + reactionDuration;
            if (animator) animator.SetTrigger(upset ? UpsetParam : HappyParam);
            Quack(hurt);
        }

        private void Quack(bool hurt)
        {
            _nextQuack = _clock + (_retreating ? gloomyQuackInterval : happyQuackInterval);
            if (animator) animator.SetTrigger(QuackParam);
            if (!voice) return;

            // Hurt by the lake getting worse: shouting. Sulking on the shore: muttering. Otherwise chipper.
            voice.Quack(hurt ? LakeDuckVoice.Tone.Shouting
                : _retreating ? LakeDuckVoice.Tone.Muttering
                : LakeDuckVoice.Tone.Chipper);
        }
    }
}
