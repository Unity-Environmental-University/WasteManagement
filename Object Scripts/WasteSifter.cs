using System.Collections;
using _project.Scripts.Core;
using _project.Scripts.UI;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    public class WasteSifter : MonoBehaviour, IStinkSource
    {
        public HealthBar healthBar;
        public float maxHealth;
        public float health;
        
        [SerializeField] private int siftPower = 1;

        [Header("Stink")]
        [SerializeField] private float stinkReduction = 0.5f;

        private bool _isBreaking;
        private SpecialInteractController _slot;
        private int _infraValue;

        public float CurrentStink => -Mathf.Max(0f, stinkReduction);

        private void OnEnable()
        {
            StinkSourceRegistry.Register(this);
        }

        private void OnDisable()
        {
            StinkSourceRegistry.Unregister(this);
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
            var survived = healthBar ? healthBar.SetHealth(newHealth, maxHealth) : newHealth > 0;
            if (!survived && !_isBreaking) StartCoroutine(BreakSifter());
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

            var damage = issue.SiftCost;
            SetHealth(health - damage);
            issue.Process(siftPower, "Sifted");
        }

        private IEnumerator BreakSifter()
        {
            _isBreaking = true;
            yield return new WaitForSeconds(4f);
            _slot?.ClearOccupied(_infraValue);
            if (healthBar) healthBar.gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
