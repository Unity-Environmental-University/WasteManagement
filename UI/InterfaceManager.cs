using UnityEngine;
using UnityEngine.UI;

namespace _project.Scripts.UI
{
    public class InterfaceManager : MonoBehaviour
    {
        [SerializeField] private Button quitButton;
        [SerializeField] private Image mTowerUpgrades;
        [SerializeField] private Image rTowerUpgrades;
        [SerializeField] private Image lTowerUpgrades;

        private void Start()
        {
            quitButton.onClick.AddListener(Application.Quit);
        }
    }
}
