using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     A cosmetic resident: lake health changes its mood, never the game rules. Every quack pops
    ///     a speech bubble over its head, so the sound visibly comes from the duck.
    /// </summary>
    public sealed class LakeDuck : MonoBehaviour
    {
        [SerializeField] private LakeController lake;
        [SerializeField] private Transform body;
        [SerializeField] private Transform head;
        [SerializeField] private Transform leftWing;
        [SerializeField] private Transform rightWing;
        [SerializeField] private Transform eyebrows;
        [SerializeField] private Vector3 swimmingSpot;
        [SerializeField] private Vector3 shoreSpot;
        [SerializeField, Min(0.1f)] private float swimRadius = 3f;
        [SerializeField, Min(0.1f)] private float travelSpeed = 3f;
        [SerializeField] private AudioSource voice;

        [Header("Camera Readability")]
        [SerializeField, Min(0.1f)] private float visualScale = 1.8f;
        [SerializeField, Min(0.1f)] private float reactionDuration = 2f;

        private const float BubbleWidth = 12f;
        private const float BubbleLift = 1.7f; // Times visualScale, off the head toward the top of the screen.
        private const float BubbleDuration = 1.3f;

        private static readonly string[] HappyQuacks = { "Quack!", "Quack quack!" };
        private static readonly string[] UpsetQuacks = { "QUACK!!", "Quack?!" };
        private static readonly Color HappyInk = new(0.12f, 0.12f, 0.16f);
        private static readonly Color UpsetInk = new(0.85f, 0.16f, 0.12f);
        private static readonly Color GloomyInk = new(0.42f, 0.44f, 0.5f);

        private bool _retreating;
        private float _clock;
        private float _nextQuack;
        private float _quackUntil;
        private float _reactionStarted;
        private float _reactionUntil;
        private float _previousHealth;
        private bool _reactionUpset;
        private Vector3 _leftWingRest;
        private Vector3 _rightWingRest;
        private AudioClip _quack;
        private SpeechBubble _bubble;

        private void Start()
        {
            if (!lake) lake = FindFirstObjectByType<LakeController>();
            if (body) body.localScale = Vector3.one * visualScale;
            if (leftWing) _leftWingRest = leftWing.localPosition;
            if (rightWing) _rightWingRest = rightWing.localPosition;
            if (lake) _previousHealth = lake.health;
            _nextQuack = 8f;
            if (voice) _quack = CreateQuack();
            if (head) _bubble = SpeechBubble.Create(head, BubbleLift * visualScale, BubbleWidth);
        }

        private void Update()
        {
            if (!lake || !body || !head) return;
            _clock += Time.deltaTime;

            // A little hysteresis keeps the duck from changing its mind at the boundary.
            var retreat = _retreating ? lake.health < 80f : lake.health < 50f;
            if (retreat != _retreating)
            {
                _retreating = retreat;
                React(retreat);
            }
            else if (lake.health < _previousHealth && _clock >= _reactionUntil)
                React(true);
            _previousHealth = lake.health;

            var target = _retreating ? shoreSpot : swimmingSpot + new Vector3(
                Mathf.Sin(_clock * 0.32f) * swimRadius, 0f,
                Mathf.Sin(_clock * 0.64f) * swimRadius * 0.35f);
            var direction = target - transform.position;
            transform.position = Vector3.MoveTowards(transform.position, target, travelSpeed * Time.deltaTime);
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.04f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction), 90f * Time.deltaTime);

            var paddling = !_retreating || direction.sqrMagnitude > 0.1f;
            AnimatePose(paddling);

            if (_clock >= _nextQuack && _clock >= _reactionUntil) React(_retreating);
        }

        private void AnimatePose(bool paddling)
        {
            var elapsed = _clock - _reactionStarted;
            var duration = Mathf.Max(0.1f, reactionDuration);
            var reacting = _clock < _reactionUntil;
            var intensity = reacting
                ? Mathf.Clamp01(elapsed / 0.15f) * Mathf.Clamp01((_reactionUntil - _clock) / 0.35f)
                : 0f;
            var flap = (0.5f + 0.5f * Mathf.Sin(elapsed * 18f)) * intensity;
            var hop = !_reactionUpset && reacting
                ? Mathf.Abs(Mathf.Sin(elapsed / duration * Mathf.PI * 3f)) * intensity * 0.85f
                : 0f;
            var bob = paddling ? Mathf.Sin(_clock * 4f) * 0.16f : 0f;
            var rock = _reactionUpset ? Mathf.Sin(elapsed * 15f) * intensity * 13f : 0f;
            body.localPosition = Vector3.up * ((bob + hop) * visualScale);
            body.localRotation = Quaternion.Euler(_reactionUpset ? intensity * -10f : intensity * 10f,
                0f, rock + (paddling ? Mathf.Sin(_clock * 4f) * 5f : 0f));
            head.localRotation = Quaternion.Euler(_retreating ? -18f : Mathf.Sin(_clock * 2f) * 9f,
                _reactionUpset ? Mathf.Sin(elapsed * 16f) * intensity * 45f :
                _clock < _quackUntil ? Mathf.Sin(_clock * 28f) * 18f : 0f, 0f);
            AnimateWing(leftWing, _leftWingRest, -1f, paddling, flap, intensity);
            AnimateWing(rightWing, _rightWingRest, 1f, paddling, flap, intensity);
            if (eyebrows) eyebrows.gameObject.SetActive(_retreating || (reacting && _reactionUpset));
        }

        private void AnimateWing(Transform wing, Vector3 rest, float side, bool paddling,
            float flap, float intensity)
        {
            if (!wing) return;
            wing.localPosition = rest + new Vector3(side * intensity * 0.65f, flap * 0.7f, 0f);
            var paddle = paddling ? Mathf.Sin(_clock * 6f) * 16f : -8f;
            wing.localRotation = Quaternion.Euler(flap * 25f, 0f, side * (flap * 65f - paddle));
        }

        private void React(bool upset)
        {
            _reactionUpset = upset;
            _reactionStarted = _clock;
            _reactionUntil = _clock + Mathf.Max(0.1f, reactionDuration);
            Quack(upset);
        }

        private void Quack(bool upset)
        {
            _quackUntil = _clock + 0.65f;
            _nextQuack = _clock + (_retreating ? 9f : 16f);
            if (voice && _quack) voice.PlayOneShot(_quack);
            if (!_bubble) return;

            // Hurt by the lake getting worse: shouting. Sulking on the shore: muttering. Otherwise chipper.
            // React runs before Update records this frame's health, so a drop still shows here.
            var hurt = upset && lake && lake.health < _previousHealth;
            if (hurt)
                _bubble.Say(UpsetQuacks[Random.Range(0, UpsetQuacks.Length)], UpsetInk, BubbleDuration, true);
            else if (_retreating)
                _bubble.Say("quack...", GloomyInk, BubbleDuration);
            else
                _bubble.Say(HappyQuacks[Random.Range(0, HappyQuacks.Length)], HappyInk, BubbleDuration);
        }

        private static AudioClip CreateQuack()
        {
            const int sampleRate = 22050;
            var samples = new float[(int)(sampleRate * 0.22f)];
            var phase = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / samples.Length;
                phase += 2f * Mathf.PI * Mathf.Lerp(620f, 370f, t) / sampleRate;
                var envelope = Mathf.Sin(Mathf.PI * t) * Mathf.Min(1f, t * 25f);
                samples[i] = (Mathf.Sin(phase) + 0.35f * Mathf.Sin(phase * 2f) +
                              0.18f * Mathf.Sin(phase * 3f)) * envelope * 0.3f;
            }
            var clip = AudioClip.Create("Lake duck quack", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDestroy()
        {
            if (_quack) Destroy(_quack);
            if (_bubble) Destroy(_bubble.gameObject);
        }
    }
}
