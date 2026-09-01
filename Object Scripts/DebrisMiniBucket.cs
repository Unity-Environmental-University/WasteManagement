using _project.Scripts.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace _project.Scripts.Object_Scripts
{
    public class DebrisMiniBucket : MonoBehaviour, IDropHandler
    {
        [SerializeField] private DebrisHandler.DebrisType type;

        private void OnEnable()
        {
            GameMaster.Instance.sifterMiniController.RegisterBucket(this);
        }

        private void OnDisable()
        {
            GameMaster.Instance.sifterMiniController.UnregisterBucket(this);
        }

        public void OnDrop(PointerEventData eventData)
        {
            var handler = eventData.pointerDrag?.GetComponent<DebrisHandler>();
            if (handler)
                handler.HandleBucketDrop(type);
        }
    }
}
