using UnityEngine;

namespace _project.Scripts.Core
{
    public class EditorTestingController : MonoBehaviour
    {
        [SerializeField] private GameObject layoutVisual;

        private void Awake()
        {
            HideNonPlayItems();
        }

        private void HideNonPlayItems()
        {
            Destroy(layoutVisual);
        }
    }
}