using System.Collections;
using _project.Scripts.UI;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    public class Cesspit : MonoBehaviour
    { 
        [SerializeField] private int processPower = 3;
        [SerializeField] private GameObject runawayPrefab;
        [SerializeField] private Transform runawayDestination;
        [SerializeField] private float runawaySpawnInterval = 4f;
        
        public HealthBar healthBar;
        public float maxHealth;
        public float health;

        private SpecialInteractController _slot;
        private bool _isBreaking;
        private bool _spawningRunaways;
        
        private void Start()
        {
            if (healthBar) healthBar.gameObject.SetActive(true);
            if (healthBar) healthBar.SetHealth(health, maxHealth);

            StartCoroutine(SpawnRunaway());
        }

        public void SetHealth(float newHealth)
        {
            health = newHealth;
            var survived = healthBar ? healthBar.SetHealth(newHealth, maxHealth) : newHealth > 0;
            //if (!survived && !_isBreaking) StartCoroutine(BreakTank());
        }

        public void SetSlot(SpecialInteractController slot) => _slot = slot;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.gameObject.CompareTag("IssueObject")) return;
            var issue = other.GetComponent<IssueObject>();
            if (issue == null || !issue.TryRegisterSifter(GetEntityId())) return;

            var damage = issue.SiftCost;
            SetHealth(health - damage);
            issue.Sift(processPower);
        }

        private IEnumerator SpawnRunaway()
        {
            while (_spawningRunaways)
            {
                yield return new WaitForSeconds(runawaySpawnInterval);

                if (!runawayPrefab || !runawayDestination) continue;

                var obj = Instantiate(runawayPrefab, transform.position, transform.rotation);
                if (!obj.TryGetComponent<IssueObject>(out var issue)) continue;

                issue.AssignType();
                issue.SetDirectDestination(runawayDestination.position);
            }
        }
    }
}
