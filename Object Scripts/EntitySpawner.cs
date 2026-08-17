using System.Collections;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    public class EntitySpawner : MonoBehaviour
    {
        [SerializeField] private GameObject spawnerObject;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private WaypointPath path;
        private Coroutine _spawnCoroutine;
        
        public float spawnInterval;
        public GameObject SpawnPrefab => spawnerObject;
        public WaypointPath Path => path;

        private void Awake()
        {
            if (!spawnPoint) spawnPoint = transform;
        }

        public bool StartSpawner(float spawnRateMultiplier = 1f)
        {
            // Restart semantics: never let a previous spawn loop keep running unreferenced.
            StopSpawner();

            if (!path)
            {
                Debug.LogWarning("Cannot start spawner: no waypoint path assigned.");
                return false;
            }

            if (!path.Rebuild())
            {
                Debug.LogWarning($"Cannot start spawner: {path.InvalidReason}");
                return false;
            }

            _spawnCoroutine = StartCoroutine(SpawnTimer(GetEffectiveInterval(spawnRateMultiplier)));
            return true;
        }

        private float GetEffectiveInterval(float spawnRateMultiplier)
        {
            return spawnInterval / Mathf.Max(spawnRateMultiplier, 0.01f);
        }

        public void StopSpawner()
        {
            if (_spawnCoroutine == null) return;

            StopCoroutine(_spawnCoroutine);
            _spawnCoroutine = null;
        }

        public bool ValidatePath(out string reason)
        {
            if (!path)
            {
                reason = "No waypoint path assigned.";
                return false;
            }

            if (path.Rebuild())
            {
                reason = null;
                return true;
            }

            reason = path.InvalidReason;
            return false;
        }

        private IEnumerator SpawnTimer(float interval)
        {
            yield return new WaitForSeconds(Mathf.Min(interval, Mathf.Max(0f, spawnInterval)));

            while (true)
            {
                SpawnObject(spawnerObject);
                yield return new WaitForSeconds(interval);
            }
        }

        private void SpawnObject(GameObject spawnableObject)
        {
            if (!spawnerObject) return;
            var obj = Instantiate(spawnableObject, spawnPoint.position, spawnPoint.rotation);
            if (!obj.TryGetComponent<IssueObject>(out var issue)) return;
            issue.AssignType();
            issue.SetPath(path);
        }
    }
}
