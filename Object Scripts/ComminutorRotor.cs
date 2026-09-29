using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    public sealed class ComminutorRotor : MonoBehaviour
    {
        [SerializeField] private Transform rotor;
        [SerializeField, Min(0f)] private float degreesPerSecond = 360f;

        private bool _running = true;

        public void SetRunning(bool running) => _running = running;

        private void Update()
        {
            if (!_running || !rotor) return;
            rotor.Rotate(Vector3.forward, degreesPerSecond * Time.deltaTime, Space.Self);
        }
    }
}
