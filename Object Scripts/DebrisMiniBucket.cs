using _project.Scripts.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace _project.Scripts.Object_Scripts
{
    public class DebrisMiniBucket : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private DebrisHandler.DebrisType type;
        [SerializeField] private float scaleMultiplier = 1.2f;

        // Hover sets an absolute scale off this, so a missed exit (the window closing mid-drag)
        // can't leave the bucket enlarged or compound across rounds.
        private Vector3 _restScale;

        private static SifterMiniGameController Controller
        {
            get
            {
                var master = GameMaster.Instance;
                return master ? master.sifterMiniController : null;
            }
        }

        private void Awake()
        {
            _restScale = transform.localScale;
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
            transform.localScale = _restScale;
        }

        public void OnDrop(PointerEventData eventData)
        {
            var handler = eventData.pointerDrag?.GetComponent<DebrisHandler>();
            if (handler) handler.HandleBucketDrop(type);
            transform.localScale = _restScale;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (eventData.pointerDrag)
                transform.localScale = _restScale * scaleMultiplier;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            transform.localScale = _restScale;
        }
    }
}
