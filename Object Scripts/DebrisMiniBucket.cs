using _project.Scripts.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace _project.Scripts.Object_Scripts
{
    public class DebrisMiniBucket : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private DebrisHandler.DebrisType type;
        [SerializeField] private float scaleMultiplier = 1.2f;

        private static SifterMiniGameController Controller
        {
            get
            {
                var master = GameMaster.Instance;
                return master ? master.sifterMiniController : null;
            }
        }

        private void OnEnable()
        {
            var controller = Controller;
            if (controller) controller.RegisterBucket(this);
        }

        private void OnDisable()
        {
            var controller = Controller;
            if (controller) controller.UnregisterBucket(this);
        }

        public void OnDrop(PointerEventData eventData)
        {
            var handler = eventData.pointerDrag?.GetComponent<DebrisHandler>();
            if (handler)
            {
                handler.HandleBucketDrop(type);
                DecreaseSize();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (eventData.pointerDrag)
                IncreaseSize();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (eventData.pointerDrag)
                DecreaseSize();
        }

        private void IncreaseSize()
        {
            transform.localScale *= scaleMultiplier;
        }

        private void DecreaseSize()
        {
            transform.localScale /= scaleMultiplier;
        }
    }
}
