using System.Collections.Generic;
using _project.Scripts.Core;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    public class PathSplitter : MonoBehaviour
    {
        private readonly HashSet<EntityId> _splitIssueIds = new();
        private SpecialInteractController _slot;
        private int _infraValue;

        private static bool Debugging => GameMaster.Instance?.debugging ?? false;

        public void SetSlot(SpecialInteractController slot, int infraValue = 0)
        {
            _slot = slot;
            _infraValue = infraValue;
        }

        private void OnTriggerEnter(Collider other)
        {
            var issue = other.GetComponent<IssueObject>();
            if (!issue)
                issue = other.GetComponentInParent<IssueObject>();
            if (!issue)
                return;

            var issueId = issue.GetEntityId();
            if (!_splitIssueIds.Add(issueId))
                return;

            if (!issue.TrySplitToNextRoute())
                return;

            if (Debugging)
                Debug.Log($"[PathSplitter] Routed issue to branch {issue.GetRouteIndex()}.");
        }
    }
}
