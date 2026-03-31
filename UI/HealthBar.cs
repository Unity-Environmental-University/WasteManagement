using UnityEngine;
using UnityEngine.UI;

namespace _project.Scripts.UI
{
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] private Slider slider;

        public bool SetHealth(float current, float max)
        {
            slider.maxValue = max;
            if (current <= 0)
            {
                slider.value = float.NaN;
                return false;
            }
            
            slider.value = current;
            return true;
        }
    }
}
