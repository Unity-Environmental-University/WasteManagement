using _project.Scripts.UI;
using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    public class WasteSifter : MonoBehaviour
    {
        public HealthBar healthBar;
        public float health;

        public void SetHealth(float newHealth)
        {
            var cHealth = health;
            health = newHealth;
            healthBar.SetHealth(cHealth, newHealth);
        }
    }
}