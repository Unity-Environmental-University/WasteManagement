using System.Collections;
using System.Collections.Generic;
using _project.Scripts.Core;
using _project.Scripts.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace _project.Scripts.Object_Scripts
{
    public class WasteSifter : MonoBehaviour, IStinkSource, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler,
        IRemovableUtility
    {
        private static readonly int OpeningAnimation = Animator.StringToHash(
            "Base Layer.sifterOpening");
        private static readonly int BladesSpinningAnimation = Animator.StringToHash(
            "Base Layer.sifterBladesSpinning");

        public HealthBar healthBar;
        public float maxHealth;
        public float health;
        public float debrisAccumulation;
        public float maxDebrisAccumulation;
        
        [SerializeField] private int siftPower = 1;

        [Header("Overflow Recovery")]
        [Tooltip("Debris load from junk flushed by an upstream sifter. Fresh junk still adds its full load.")]
        [SerializeField, Range(0f, 1f)] private float flushedDebrisMultiplier = 0.25f;

        [Header("Comminutor Visual")]
        [SerializeField] private ComminutorRotor comminutorRotor;

        [Header("Animation")]
        [SerializeField] private Animator gateAnimator;
        [SerializeField] private Animator bladesAnimator;

        [Header("Stink")]
        [SerializeField] private float stinkReduction = 0.5f;

        private SpecialInteractController _slot;
        private int _infraValue;
        private bool _isHovered;
        private bool _isSifting;
        private Coroutine _closeAnimation;
        private readonly HashSet<IssueObject> _heldIssues = new();

        public float CurrentStink => -Mathf.Max(0f, stinkReduction);

        private float DebrisRatio => maxDebrisAccumulation > 0f
            ? Mathf.Clamp01(debrisAccumulation / maxDebrisAccumulation)
            : 0f;

        private bool IsBlocked => maxDebrisAccumulation > 0f && debrisAccumulation >= maxDebrisAccumulation;

        private void Awake()
        {
            // Older sifters only had the gate animator; never let the fallback pick up the blades.
            if (!gateAnimator)
                foreach (var animator in GetComponentsInChildren<Animator>(true))
                    if (animator != bladesAnimator)
                    {
                        gateAnimator = animator;
                        break;
                    }

            if (bladesAnimator) bladesAnimator.Play(BladesSpinningAnimation, 0, 0f);
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

            var sifterId = GetEntityId();
            foreach (var issue in _heldIssues)
                if (issue) issue.ReleaseFromSifter(sifterId);
            _heldIssues.Clear();
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

            if (wasBlocked || !IsBlocked) return;
            SetSifting(false);
            FlushHeldIssues();
        }

        private void FlushHeldIssues()
        {
            // The clog has forced the gates open: everything the screen was holding goes on down
            // the line at once. The debris itself stays, so the sifter remains open until the
            // minigame clears it.
            foreach (var issue in _heldIssues)
                if (issue) issue.FlushFromSifter();
            _heldIssues.Clear();
        }

        /// <summary>
        ///     How many of a minigame's <paramref name="pieceCount" /> pieces the debris amounts to. Each
        ///     piece stands for an even share of a full screen, so a full screen is every piece, a
        ///     quarter-full one with four pieces is one, and any debris at all is at least one.
        /// </summary>
        public int DebrisPieces(int pieceCount)
        {
            if (pieceCount <= 0 || maxDebrisAccumulation <= 0f || debrisAccumulation <= 0f) return 0;

            // The small allowance keeps float dust from rounding an exact share up a whole piece.
            var pieces = Mathf.CeilToInt(debrisAccumulation / maxDebrisAccumulation * pieceCount - 0.01f);
            return Mathf.Clamp(pieces, 1, pieceCount);
        }

        /// <summary>
        ///     One minigame piece sorted out of the screen: it takes its share of a full screen with it,
        ///     so three of four pieces off a full screen leaves a quarter. Dropping back under the limit
        ///     closes the gates and the screen starts sifting again.
        /// </summary>
        public void SortDebrisPiece(int pieceCount)
        {
            if (pieceCount <= 0) return;

            var wasBlocked = IsBlocked;
            debrisAccumulation = Mathf.Max(0f, debrisAccumulation - maxDebrisAccumulation / pieceCount);
            if (wasBlocked && !IsBlocked) SetSifting(true);
        }

        public void ClearDebris()
        {
            debrisAccumulation = 0f;
            SetSifting(true);
            DestroyHeldIssues();
        }

        private void DestroyHeldIssues()
        {
            // The minigame is the player sorting these out of the screen by hand, so once it's done
            // they're gone — releasing them would only send the same solids on down the line.
            foreach (var issue in _heldIssues)
                if (issue) Destroy(issue.gameObject);
            _heldIssues.Clear();
        }

        private void SetSifting(bool isSifting, bool force = false)
        {
            if (!force && _isSifting == isSifting) return;

            _isSifting = isSifting;
            if (comminutorRotor) comminutorRotor.SetRunning(isSifting);
            // A clogged screen jams the blades where they stand; clearing it picks the spin back up.
            if (bladesAnimator) bladesAnimator.speed = isSifting ? 1f : 0f;
            if (!gateAnimator) return;

            if (_closeAnimation != null)
            {
                StopCoroutine(_closeAnimation);
                _closeAnimation = null;
            }

            gateAnimator.enabled = true;
            if (isSifting)
            {
                if (force)
                {
                    SetOpeningFrame(0f);
                    gateAnimator.enabled = false;
                }
                else
                {
                    _closeAnimation = StartCoroutine(CloseSifter());
                }

                return;
            }

            gateAnimator.speed = 1f;
            SetOpeningFrame(0f);
        }

        private IEnumerator CloseSifter()
        {
            gateAnimator.speed = 1f;
            SetOpeningFrame(1f);
            var duration = gateAnimator.GetCurrentAnimatorStateInfo(0).length;
            gateAnimator.speed = 0f;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                SetOpeningFrame(1f - elapsed / duration);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            SetOpeningFrame(0f);
            gateAnimator.speed = 1f;
            gateAnimator.enabled = false;
            _closeAnimation = null;
        }

        private void SetOpeningFrame(float normalizedTime)
        {
            gateAnimator.Play(OpeningAnimation, 0, Mathf.Clamp01(normalizedTime));
            gateAnimator.Update(0f);
        }

        /// <summary>A sifter that is damaged or holding debris stays put.</summary>
        public bool CanRemove => health >= maxHealth && debrisAccumulation <= 0f;

        public void Remove()
        {
            if (_slot) _slot.ClearOccupied(_infraValue);
            StinkSourceRegistry.Unregister(this);
            Destroy(gameObject);
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

            if (issue.GetIssueType() == IssueType.Metal && !IsBlocked && maxDebrisAccumulation > 0f)
            {
                // Metal doesn't clog the screen gradually — it breaks it open in one hit. The gates
                // jam open (flushing anything already held) and the metal stays wedged in the
                // screen until the player opens the sifter and clears it by hand.
                AccumulateDebris(maxDebrisAccumulation);
                issue.SetHeldBySifter(true);
                _heldIssues.Add(issue);
                return;
            }

            var isNonWaste = issue.GetIssueType() == IssueType.NonWaste;
            if (isNonWaste && !IsBlocked)
            {
                // A screen doesn't grind solids — it stops them. NonWaste sticks here, adding
                // to the clog, until the debris minigame removes it (or a comminutor breaks it down).
                // Held before accumulating: if this piece is the one that clogs the screen, it gets
                // flushed along with the rest.
                issue.SetHeldBySifter(true);
                _heldIssues.Add(issue);
                // A downstream screen can absorb an upstream flush without immediately repeating it.
                // Apply once per capture, not to issue size or its destination damage.
                var debrisLoad = issue.WasFlushedFromSifter
                    ? issue.SiftCost * Mathf.Clamp01(flushedDebrisMultiplier)
                    : issue.SiftCost;
                AccumulateDebris(debrisLoad);
                return;
            }

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
            // Junk arriving now is no longer stopped either — it rides through unscreened.
            if (IsBlocked)
            {
                if (isNonWaste) issue.FlushFromSifter();
                return;
            }

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
            // A pan-drag that starts on the sifter carries it along under the pointer, so the
            // release lands back on it and would read as a click.
            var cameraController = GameMaster.Instance.cameraController;
            if (cameraController && cameraController.IsPanArmed) return;

            if (UtilityRemoval.TryRemove(this)) return;

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
