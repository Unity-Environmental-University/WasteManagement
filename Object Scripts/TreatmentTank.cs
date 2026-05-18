using _project.Scripts.UI;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    public class TreatmentTank : MonoBehaviour
    {
        [Header("Throughput")]
        [SerializeField] private float sludgePerIssue = 1f;
        [SerializeField] private GameObject effluentPrefab;

        [Header("Fullness")]
        public HealthBar fullnessBar;
        public float maxFullness = 10f;
        public float fullness;

        private void Start()
        {
            if (fullnessBar) fullnessBar.gameObject.SetActive(true);
            UpdateFullnessBar();
        }

        private SpecialInteractController _slot;
        private int _infraValue;

        public void SetSlot(SpecialInteractController slot, int infraValue = 0)
        {
            _slot = slot;
            _infraValue = infraValue;
        }

        // CARD UPGRADE HOOK: future card upgrades (e.g., capacity, efficiency)
        // would mutate maxFullness / sludgePerIssue here, parallel to TowerController.

        private void OnTriggerEnter(Collider other)
        {
            if (!other.gameObject.CompareTag("IssueObject")) return;
            if (IsFull) return; // Pass-through when full — do not consume

            var issue = other.GetComponent<IssueObject>();
            if (issue == null) return;
            if (issue.IsDirectDestination) return;
            if (!issue.TryRegisterSifter(GetEntityId())) return;

            var spawnPos = issue.transform.position;
            var srcPath = issue.GetPath();
            var srcIndex = issue.GetWaypointIndex();

            SetFullness(fullness + sludgePerIssue);
            SpawnEffluent(spawnPos, srcPath, srcIndex);
            Destroy(issue.gameObject);
        }

        private void SpawnEffluent(Vector3 pos, WaypointPath p, int waypointIndex)
        {
            if (!effluentPrefab) return;
            var go = Instantiate(effluentPrefab, pos, Quaternion.identity);
            if (go.TryGetComponent<EffluentObject>(out var eff))
                eff.SetPath(p, waypointIndex);
        }

        private void SetFullness(float v)
        {
            fullness = Mathf.Clamp(v, 0f, maxFullness);
            UpdateFullnessBar();
        }

        private bool IsFull => maxFullness > 0f && fullness >= maxFullness;

        private void UpdateFullnessBar()
        {
            if (fullnessBar) fullnessBar.SetValue(fullness, maxFullness);
        }
    }
}
