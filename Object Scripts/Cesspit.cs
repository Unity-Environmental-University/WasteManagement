using System.Collections;
using _project.Scripts.UI;
using UnityEngine;
using UnityEngine.Serialization;

namespace _project.Scripts.Object_Scripts
{
    public class Cesspit : MonoBehaviour
    { 
        [SerializeField] private int processPower = 3;
        [SerializeField] private GameObject runawayPrefab;
        [SerializeField] private Transform runawayDestination;
        [SerializeField] private float runawaySpawnInterval = 4f;
        
        [FormerlySerializedAs("healthBar")] public HealthBar fullnessBar;
        [FormerlySerializedAs("maxHealth")] public float maxFullness;
        [FormerlySerializedAs("health")] public float fullness;

        private SpecialInteractController _slot;
        private bool _spawningRunaways;
        
        private void Start()
        {
            if (fullnessBar) fullnessBar.gameObject.SetActive(true);
            UpdateFullnessBar();

            if (IsFull)
                StartRunaways();
        }

        public void SetFullness(float newFullness)
        {
            fullness = Mathf.Clamp(newFullness, 0f, maxFullness);
            UpdateFullnessBar();

            if (IsFull)
                StartRunaways();
        }

        public void SetSlot(SpecialInteractController slot) => _slot = slot;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.gameObject.CompareTag("IssueObject")) return;
            var issue = other.GetComponent<IssueObject>();
            if (issue == null || !issue.TryRegisterSifter(GetEntityId())) return;

            SetFullness(fullness + issue.SiftCost);
            issue.Sift(processPower);
        }

        private bool IsFull => maxFullness > 0f && fullness >= maxFullness;

        private void UpdateFullnessBar()
        {
            if (fullnessBar) fullnessBar.SetValue(fullness, maxFullness);
        }

        private void StartRunaways()
        {
            if (_spawningRunaways) return;

            _spawningRunaways = true;
            StartCoroutine(SpawnRunaway());
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
