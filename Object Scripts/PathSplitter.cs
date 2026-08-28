using System;
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
        /// <summary>
        ///     Raised when a splitter enters or leaves the active scene, so live route previews can
        ///     immediately reveal or hide their alternate branch.
        /// </summary>
        public static event Action AvailabilityChanged;

        private static readonly List<PathSplitter> LiveSplitters = new();

        /// <summary>All currently enabled splitters.</summary>
        public static IReadOnlyList<PathSplitter> Live => LiveSplitters;

        private readonly HashSet<EntityId> _routedIssueIds = new();
        private int _nextRouteIndex;

        private static bool Debugging
        {
            get
            {
                var gm = GameMaster.Instance;
                return gm && gm.debugging;
            }
        }

        private void Awake()
        {
            if (!TryGetComponent<Collider>(out var trigger))
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.5f, 0f);
                box.size = new Vector3(0.9f, 1.25f, 0.9f);
                trigger = box;
            }

            trigger.isTrigger = true;
        }

        private void OnEnable()
        {
            LiveSplitters.Add(this);
            LiveComponentRegistry.Register(this);
            TurnController.OnTowerPhaseEntered += ResetSplit;
            AvailabilityChanged?.Invoke();
        }

        private void OnDisable()
        {
            LiveSplitters.Remove(this);
            LiveComponentRegistry.Unregister(this);
            TurnController.OnTowerPhaseEntered -= ResetSplit;
            AvailabilityChanged?.Invoke();
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
