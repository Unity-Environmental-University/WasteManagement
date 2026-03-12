using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace _project.Scripts.UI
{
    public class UpgradeInterface : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private Image highlight;
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            highlight.gameObject.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            highlight.gameObject.SetActive(false);
        }
        
        public void OnPointerClick(PointerEventData eventData)
        {
            Debug.Log("UpgradeInterface clicked");
        }
    }
}