using UnityEngine;
using UnityEngine.UI;

namespace _project.Scripts.UI
{
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] private Slider slider;

        public bool SetHealth(float current, float max)
        {
            SetValue(current, max);

            return !(current <= 0);
        }

        public void SetValue(float current, float max)
        {
            slider.maxValue = max;
            slider.value = Mathf.Clamp(current, 0f, max);
        }
    }
}