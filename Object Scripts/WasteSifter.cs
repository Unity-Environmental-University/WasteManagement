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
        public float defaultProcessCost = 5;

        private void Start()
        {
            healthBar.gameObject.SetActive(true);
            /*
            This is being set inside the inspector- may lead to issues but maybe not?
            health = maxHealth;
            */
            healthBar.SetHealth(health, maxHealth);
        }

        public void SetHealth(float newHealth)
        {
            health = newHealth;
            var survived = healthBar.SetHealth(newHealth, maxHealth);
            if (!survived) StartCoroutine(BreakSifter());
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.gameObject.CompareTag($"IssueObject")) return;
            var newHealth = health - other.GetComponent<IssueObject>()?.siftCost ?? defaultProcessCost;
            SetHealth(newHealth);
        }

        private IEnumerator BreakSifter()
        {
            yield return new WaitForSeconds(4f);
            healthBar.gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}