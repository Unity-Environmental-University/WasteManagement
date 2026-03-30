using UnityEngine;
using UnityEngine.UI;

namespace _project.Scripts.UI
{
    public class TowerHealthBar : MonoBehaviour
    {
        [SerializeField] private Slider slider;

        public void SetHealth(float current, float max)
        {
            slider.value = current / max;
        }
    }
}
