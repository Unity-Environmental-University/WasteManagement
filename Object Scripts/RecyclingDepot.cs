using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    public class RecyclingDepot : MonoBehaviour
    {
        [SerializeField] private WaypointPath path;
        [SerializeField] private TextMesh label;
        public int DeliveredCount { get; private set; }

        private void OnEnable() => IssueObject.OnReachedEnd += Receive;
        private void OnDisable() => IssueObject.OnReachedEnd -= Receive;

        private void Receive(IssueObject issue)
        {
            if (issue.GetPath() != path || issue.IsDirectDestination || issue.IsBlockingPipe) return;
            DeliveredCount++;
            if (label) label.text = "RECYCLING\n" + DeliveredCount + " received";
        }
    }
}
