using UnityEngine;
using UnityEngine.UI;

namespace _project.Scripts.UI
{
    public class InterfaceManager : MonoBehaviour
    {
        [SerializeField] private Button quitButton;

        private void Start()
        {
            quitButton.onClick.AddListener(Application.Quit);
        }
    }
}
