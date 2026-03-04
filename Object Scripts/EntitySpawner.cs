using System.Collections;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    public class EntitySpawner : MonoBehaviour
    {
        [SerializeField] private GameObject spawnerObject;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private WaypointPath path;

        public float spawnInterval;
        
        private void Awake()
        {
            spawnPoint = spawnPoint?.transform;
        }

        private void Start()
        {
            StartCoroutine(SpawnTimer(spawnInterval));
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
            if (obj.TryGetComponent<IssueObject>(out var issue))
                issue.SetPath(path);
        }
    }
}