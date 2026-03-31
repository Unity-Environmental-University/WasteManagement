using UnityEngine;
using UnityEngine.UI;

namespace _project.Scripts.UI
{
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] private Slider slider;

        public void SetHealth(float current, float max)
        {
            slider.value = current / max;
        }
    }
}
