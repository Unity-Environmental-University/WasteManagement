using System.Collections;
using _project.Scripts.UI;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    public class WasteSifter : MonoBehaviour
    {
        public HealthBar healthBar;
        public float maxHealth;
        public float health;
        [SerializeField] private int siftPower = 1;
        private bool _isBreaking;
        private SpecialInteractController _slot;

        private void Start()
        {
            if (healthBar) healthBar.gameObject.SetActive(true);
            /*
            This is being set inside the inspector- may lead to issues but maybe not?
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

        public void SetSlot(SpecialInteractController slot)
        {
            _slot = slot;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.gameObject.CompareTag("IssueObject")) return;
            var issue = other.GetComponent<IssueObject>();
            if (issue == null || !issue.TryRegisterSifter(GetEntityId())) return;

            var damage = issue.SiftCost;
            SetHealth(health - damage);
            issue.Sift(siftPower);
        }

        private IEnumerator BreakSifter()
        {
            _isBreaking = true;
            yield return new WaitForSeconds(4f);
            _slot?.ClearOccupied();
            if (healthBar) healthBar.gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
