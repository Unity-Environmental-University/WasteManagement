using _project.Scripts.Core;
using _project.Scripts.UI;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    public class TreatmentTank : MonoBehaviour, IStinkSource
    {
        [Header("Throughput")]
        [SerializeField] private float sludgePerIssue = 1f;
        [SerializeField] private GameObject effluentPrefab;

        [Header("Stink")]
        [SerializeField] private float baseStink = 0.5f;
        [SerializeField] private float fullStinkBonus = 1.5f;

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

        public float CurrentStink => Mathf.Max(0f, baseStink + FullnessRatio * fullStinkBonus);

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

        public void SetSlot(SpecialInteractController slot, int infraValue = 0)
        {
            _slot = slot;
            _infraValue = infraValue;
        }

        // CARD UPGRADE HOOK: future card upgrades (e.g., capacity, efficiency)
        // would mutate maxFullness / sludgePerIssue here, parallel to TowerController.

        private void OnTriggerEnter(Collider other)
        {
            TryTreatIssue(other);
        }

        private void OnTriggerStay(Collider other)
        {
            // A pipe-blocking issue can be clicked back to a movable size while it is already
            // inside the tank trigger. OnTriggerEnter will not fire again in that case.
            TryTreatIssue(other);
        }

        private void TryTreatIssue(Collider other)
        {
            if (!other.gameObject.CompareTag("IssueObject")) return;
            if (IsFull) return; // Pass-through when full — do not consume

            var issue = other.GetComponent<IssueObject>();
            if (issue == null) return;
            if (issue.IsDirectDestination) return;
            if (issue.IsBlockingPipe) return; // Jams stay on the pipe for the player to click apart
            if (!issue.TryRegisterSifter(GetEntityId())) return;

            var spawnPos = issue.transform.position;
            var srcPath = issue.GetPath();
            var srcIndex = issue.GetWaypointIndex();
            var srcRoute = issue.GetRouteIndex();

            SetFullness(fullness + sludgePerIssue);
            SpawnEffluent(spawnPos, srcPath, srcIndex, srcRoute);
            Destroy(issue.gameObject);
        }

        private void SpawnEffluent(Vector3 pos, WaypointPath p, int waypointIndex, int routeIndex)
        {
            if (!effluentPrefab) return;
            var go = Instantiate(effluentPrefab, pos, Quaternion.identity);
            if (go.TryGetComponent<EffluentObject>(out var eff))
                eff.SetPath(p, waypointIndex, routeIndex);
        }

        private void SetFullness(float v)
        {
            fullness = Mathf.Clamp(v, 0f, maxFullness);
            UpdateFullnessBar();
            GameMaster.Instance?.interfaceManager?.RefreshStinkMeter();
        }

        private bool IsFull => maxFullness > 0f && fullness >= maxFullness;

        private float FullnessRatio => maxFullness > 0f ? Mathf.Clamp01(fullness / maxFullness) : 0f;

        private void UpdateFullnessBar()
        {
            if (fullnessBar) fullnessBar.SetValue(fullness, maxFullness);
        }
    }
}
