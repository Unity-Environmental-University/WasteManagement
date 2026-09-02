using System.Collections;
using _project.Scripts.Core;
using _project.Scripts.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace _project.Scripts.Object_Scripts
{
    public class WasteSifter : MonoBehaviour, IStinkSource, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly int OpeningAnimation = Animator.StringToHash(
            "Base Layer.sifterOpening");

        public HealthBar healthBar;
        public float maxHealth;
        public float health;
        public float debrisAccumulation;
        public float maxDebrisAccumulation;
        
        [SerializeField] private int siftPower = 1;

        [Header("Stink")]
        [SerializeField] private float stinkReduction = 0.5f;

        private SpecialInteractController _slot;
        private int _infraValue;
        private bool _isHovered;
        private Animator _animator;
        private bool _isSifting;
        private Coroutine _closeAnimation;

        public float CurrentStink => -Mathf.Max(0f, stinkReduction);

        private float DebrisRatio => maxDebrisAccumulation > 0f
            ? Mathf.Clamp01(debrisAccumulation / maxDebrisAccumulation)
            : 0f;

        private bool IsBlocked => maxDebrisAccumulation > 0f && debrisAccumulation >= maxDebrisAccumulation;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>(true);
            SetSifting(!IsBlocked, true);
        }

        private void OnEnable()
        {
            StinkSourceRegistry.Register(this);
            LiveComponentRegistry.Register(this);
        }

        private void OnDisable()
        {
            UtilityHoverStatsPopup.Instance?.Hide(transform);
            StinkSourceRegistry.Unregister(this);
            LiveComponentRegistry.Unregister(this);
        }

        private void Start()
        {
            if (healthBar) healthBar.gameObject.SetActive(true);
            /*
            This is being set inside the inspector: may lead to issues but maybe not?
            health = maxHealth;
            */
            if (healthBar) healthBar.SetHealth(health, maxHealth);
        }

        public void SetHealth(float newHealth)
        {
            health = newHealth;
            if (healthBar) healthBar.SetHealth(newHealth, maxHealth);
        }

        private void AccumulateDebris(float amount)
        {
            if (maxDebrisAccumulation <= 0f) return;

            var wasBlocked = IsBlocked;
            debrisAccumulation = Mathf.Clamp(
                debrisAccumulation + Mathf.Max(0f, amount),
                0f,
                maxDebrisAccumulation);

            if (!wasBlocked && IsBlocked)
                SetSifting(false);
        }

        public void ClearDebris()
        {
            debrisAccumulation = 0f;
            SetSifting(true);
        }

        private void SetSifting(bool isSifting, bool force = false)
        {
            if (!force && _isSifting == isSifting) return;

            _isSifting = isSifting;
            if (!_animator) return;

            if (_closeAnimation != null)
            {
                StopCoroutine(_closeAnimation);
                _closeAnimation = null;
            }

            _animator.enabled = true;
            if (isSifting)
            {
                if (force)
                {
                    SetOpeningFrame(0f);
                    _animator.enabled = false;
                }
                else
                {
                    _closeAnimation = StartCoroutine(CloseSifter());
                }

                return;
            }

            _animator.speed = 1f;
            SetOpeningFrame(0f);
        }

        private IEnumerator CloseSifter()
        {
            _animator.speed = 1f;
            SetOpeningFrame(1f);
            var duration = _animator.GetCurrentAnimatorStateInfo(0).length;
            _animator.speed = 0f;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                SetOpeningFrame(1f - elapsed / duration);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            SetOpeningFrame(0f);
            _animator.speed = 1f;
            _animator.enabled = false;
            _closeAnimation = null;
        }

        private void SetOpeningFrame(float normalizedTime)
        {
            _animator.Play(OpeningAnimation, 0, Mathf.Clamp01(normalizedTime));
            _animator.Update(0f);
        }

        public void SetSlot(SpecialInteractController slot, int infraValue = 0)
        {
            _slot = slot;
            _infraValue = infraValue;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.gameObject.CompareTag("IssueObject")) return;
            var issue = other.GetComponent<IssueObject>();
            if (issue == null || !issue.TryRegisterSifter(GetEntityId())) return;

            if (issue.GetIssueType() == IssueType.NonWaste)
                AccumulateDebris(issue.SiftCost);

            var speedMultiplier = DebrisRatio switch
            {
                < 0.2f => 1f,
                < 0.6f => 0.8f,
                < 0.8f => 0.5f,
                _ => 0.2f
            };

            if (speedMultiplier < 1f)
                issue.SetTemporaryMoveSpeedMultiplier(speedMultiplier, 2);

            // A debris-blocked sifter opens its gates: issues keep moving but remain slowed.
            if (IsBlocked) return;

            issue.Process(siftPower, "Sifted");
        }

        private void Update()
        {
            if (_isHovered)
                UtilityHoverStatsPopup.Instance?.SetStats(BuildHoverStats());
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            UtilityHoverStatsPopup.Instance?.Show(transform, BuildHoverStats());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            UtilityHoverStatsPopup.Instance?.Hide(transform);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (debrisAccumulation > 0)
                GameMaster.Instance.sifterMiniController.StartMiniGame(this);
        }

        private string BuildHoverStats()
        {
            return
                $"SIFTER\nHealth: {health:F0} / {maxHealth:F0}\nDebris: {debrisAccumulation:F0} / {maxDebrisAccumulation:F0}";
        }
    }
}
