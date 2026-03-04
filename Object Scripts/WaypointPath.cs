using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    public class WaypointPath : MonoBehaviour
    {
        [SerializeField] private Transform[] waypoints;

        public int Count => waypoints.Length;

        private void OnDrawGizmos()
        {
            if (waypoints == null || waypoints.Length < 2) return;

            Gizmos.color = Color.yellow;
            for (var i = 0; i < waypoints.Length - 1; i++)
                if (waypoints[i] && waypoints[i + 1])
                    Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
        }

        public Vector3 GetPosition(int index)
        {
            return waypoints[index].position;
        }
    }
}