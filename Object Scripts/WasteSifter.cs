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
            if (!survived) StartCoroutine(BreakSifter());
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
            yield return new WaitForSeconds(4f);
            if (healthBar) healthBar.gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
