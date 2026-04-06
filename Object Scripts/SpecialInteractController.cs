using UnityEngine;
using UnityEngine.EventSystems;

namespace _project.Scripts.Object_Scripts
{
    public class SpecialInteractController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
            Debug.Log("MOUSE ON");
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Debug.Log("MOUSE OFF");
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Debug.Log("MOUSE CLICK");
        }
    }
}
