using System.Collections;
using System.Linq;
using _project.Scripts.Core;
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
        [SerializeField] private float runawayMoveSpeed = 12f;
        
        [FormerlySerializedAs("healthBar")] public HealthBar fullnessBar;
        [FormerlySerializedAs("maxHealth")] public float maxFullness;
        [FormerlySerializedAs("health")] public float fullness;

        private bool _spawningRunaways;
        private Coroutine _runawayCoroutine;
        
        private void OnEnable()
        {
            TurnController.OnCardPhaseEntered += PauseRunaways;
            TurnController.OnTowerPhaseEntered += ResumeRunaways;
        }

        private void OnDisable()
        {
            TurnController.OnCardPhaseEntered -= PauseRunaways;
            TurnController.OnTowerPhaseEntered -= ResumeRunaways;
        }

        private void Start()
        {
            ResolveRunawayReferences();

            if (fullnessBar) fullnessBar.gameObject.SetActive(true);
            UpdateFullnessBar();

            if (IsFull)
                StartRunaways();
        }

        private void SetFullness(float newFullness)
        {
            fullness = Mathf.Clamp(newFullness, 0f, maxFullness);
            UpdateFullnessBar();

            if (IsFull)
                StartRunaways();
        }

        public void SetSlot(SpecialInteractController slot)
        {
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.gameObject.CompareTag("IssueObject")) return;
            var issue = other.GetComponent<IssueObject>();
            if (issue != null && issue.IsDirectDestination)
                return;

            if (issue == null || !issue.TryRegisterSifter(GetEntityId())) return;

            SetFullness(fullness + issue.SiftCost);
            issue.Process(processPower, "Deposited into Cesspit");
        }

        private bool IsFull => maxFullness > 0f && fullness >= maxFullness;

        private void UpdateFullnessBar()
        {
            if (fullnessBar) fullnessBar.SetValue(fullness, maxFullness);
        }

        private void StartRunaways()
        {
            if (_spawningRunaways)
                return;

            _spawningRunaways = true;
            _runawayCoroutine = StartCoroutine(SpawnRunaway());
        }

        private void PauseRunaways()
        {
            if (!_spawningRunaways) return;

            if (_runawayCoroutine == null) return;
            StopCoroutine(_runawayCoroutine);
            _runawayCoroutine = null;
        }

        private void ResumeRunaways()
        {
            if (!_spawningRunaways) return;
            if (_runawayCoroutine != null) return;

            _runawayCoroutine = StartCoroutine(SpawnRunaway());
        }

        private void ResolveRunawayReferences()
        {
            if (!runawayPrefab && GameMaster.Instance)
                foreach (var spawner in GameMaster.Instance.entitySpawners.Where(spawner =>
                             spawner && spawner.SpawnPrefab))
                {
                    runawayPrefab = spawner.SpawnPrefab;
                    break;
                }

            if (runawayDestination) return;
            var lake = FindAnyObjectByType<LakeController>();
            if (lake)
                runawayDestination = lake.transform;
        }

        private IEnumerator SpawnRunaway()
        {
            while (_spawningRunaways)
            {
                yield return new WaitForSeconds(runawaySpawnInterval);

                ResolveRunawayReferences();

                if (!runawayPrefab || !runawayDestination)
                    continue;

                var obj = Instantiate(runawayPrefab, transform.position, transform.rotation);
                if (!obj.TryGetComponent<IssueObject>(out var issue))
                    continue;

                issue.AssignType();
                issue.SetDirectDestination(runawayDestination.position);
                issue.SetMoveSpeed(runawayMoveSpeed);
                issue.EnableClickPop();
            }
        }
    }
}
