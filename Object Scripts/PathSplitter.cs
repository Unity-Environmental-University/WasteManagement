using System.Collections.Generic;
using _project.Scripts.Core;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     Sends successive issues down the normal and alternate path routes in strict rotation:
    ///     0, 1, 0, 1. This guarantees a 50/50 split for every pair and avoids random streaks.
    /// </summary>
    public class PathSplitter : MonoBehaviour
    {
        private readonly HashSet<EntityId> _routedIssueIds = new();
        private int _nextRouteIndex;

        private static bool Debugging => GameMaster.Instance?.debugging ?? false;

        private void Awake()
        {
            if (!TryGetComponent<Collider>(out var trigger))
                trigger = gameObject.AddComponent<BoxCollider>();

            trigger.isTrigger = true;
        }

        private void OnEnable()
        {
            TurnController.OnTowerPhaseEntered += ResetSplit;
        }

        private void OnDisable()
        {
            TurnController.OnTowerPhaseEntered -= ResetSplit;
        }

        private void OnTriggerEnter(Collider other)
        {
            var issue = other.GetComponentInParent<IssueObject>();
            if (issue) RouteIssue(issue);
        }

        public bool RouteIssue(IssueObject issue)
        {
            if (!issue || issue.IsDirectDestination) return false;

            var path = issue.GetPath();
            if (!path || !path.HasAlternateRoute || !path.IsSplitPoint(transform.position)) return false;
            if (!_routedIssueIds.Add(issue.GetEntityId())) return false;

            var routeIndex = _nextRouteIndex;
            if (!issue.TrySetRoute(routeIndex)) return false;

            _nextRouteIndex = 1 - _nextRouteIndex;

            if (Debugging)
                Debug.Log($"[PathSplitter] Routed issue to option {routeIndex + 1}.");

            return true;
        }

        private void ResetSplit()
        {
            _routedIssueIds.Clear();
            _nextRouteIndex = 0;
        }
    }
}
