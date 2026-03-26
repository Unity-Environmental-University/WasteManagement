using System.Collections;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    public class EntitySpawner : MonoBehaviour
    {
        [SerializeField] private GameObject spawnerObject;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private WaypointPath[] paths;
        private Coroutine _spawnCoroutine;
        
        public float spawnInterval;

        private void Awake()
        {
            spawnPoint = spawnPoint?.transform;
        }

        private void Start()
        {
        }

        public void StartSpawner()
        {
            _spawnCoroutine = StartCoroutine(SpawnTimer(spawnInterval));
        }

        public void StopSpawner()
        {
            if (_spawnCoroutine != null)
                StopCoroutine(_spawnCoroutine);
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
            if (!obj.TryGetComponent<IssueObject>(out var issue) || paths.Length <= 0) return;
            issue.AssignType();
            issue.SetPath(paths[Random.Range(0, paths.Length)]);
        }
    }
}