using System.Collections;
using _project.Scripts.UI;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    public class Cesspit : MonoBehaviour
    { 
        [SerializeField] private int processPower = 3;
        
        public HealthBar healthBar;
        public float maxHealth;
        public float health;

        private SpecialInteractController _slot;
        private bool _isBreaking;
        private bool _isOffloading;
        
        private void Start()
        {
            if (healthBar) healthBar.gameObject.SetActive(true);
            if (healthBar) healthBar.SetHealth(health, maxHealth);
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
            while (_isOffloading)
            {
                yield return new WaitForSeconds(4f);
                // Spawn a poop gremlin
                
            }
            yield return new WaitForSeconds(4f);
        }

        private IEnumerator BreakTank()
        {
            _isBreaking = true;
            yield return new WaitForSeconds(4f);
            _slot?.ClearOccupied();
            if (healthBar) healthBar.gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
