using _project.Scripts.Core;
using _project.Scripts.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace _project.Scripts.Object_Scripts
{
    public class WasteSifter : MonoBehaviour, IStinkSource, IPointerClickHandler
    {
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

        public float CurrentStink => -Mathf.Max(0f, stinkReduction);

        private float DebrisRatio => maxDebrisAccumulation > 0f
            ? Mathf.Clamp01(debrisAccumulation / maxDebrisAccumulation)
            : 0f;

        private bool IsBlocked => maxDebrisAccumulation > 0f && debrisAccumulation >= maxDebrisAccumulation;

        private void OnEnable()
        {
            StinkSourceRegistry.Register(this);
            LiveComponentRegistry.Register(this);
        }

        private void OnDisable()
        {
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

            debrisAccumulation = Mathf.Clamp(
                debrisAccumulation + Mathf.Max(0f, amount),
                0f,
                maxDebrisAccumulation);
        }

        public void ClearDebris()
        {
            debrisAccumulation = 0f;
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

        public void OnPointerClick(PointerEventData eventData)
        {
            if (debrisAccumulation > 0)
                GameMaster.Instance.sifterMiniController.StartMiniGame(this);
        }
    }
}
