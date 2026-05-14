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

        private void Awake()
        {
            spawnPoint = spawnPoint?.transform;
        }

        public bool StartSpawner()
        {
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

            _spawnCoroutine = StartCoroutine(SpawnTimer(spawnInterval));
            return true;
        }

        public void StopSpawner()
        {
            if (_spawnCoroutine != null)
                StopCoroutine(_spawnCoroutine);
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
            while (true)
            {
                yield return new WaitForSeconds(interval);
                SpawnObject(spawnerObject);
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
