using System.Collections.Generic;
using _project.Scripts.Object_Scripts;
using UnityEngine;

namespace _project.Scripts.Core
{
    public class IssuePreview : MonoBehaviour
    {
        // Arbitrary nums TODO Balance this
        [SerializeField] private int minSpawnCount = 10;
        [SerializeField] private int maxSpawnCount = 20;

        private Queue<IssueType> _upcoming;

        public Queue<IssueType> GetUpcomingIssues()
        {
            if (_upcoming is { Count: > 0 })
                return _upcoming;

            _upcoming = GenerateUpcoming();
            return _upcoming;
        }

        public IssueType Dequeue() => _upcoming.Dequeue();

        public bool HasNext() => _upcoming is { Count: > 0 };

        private Queue<IssueType> GenerateUpcoming()
        {
            var queue = new Queue<IssueType>();
            var count = Random.Range(minSpawnCount, maxSpawnCount + 1);

            for (var i = 0; i < count; i++)
            {
                queue.Enqueue(Random.Range(0, 2) == 0 ? IssueType.Organic : IssueType.Chemical);
            }

            return queue;
        }
    }
}