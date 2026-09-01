using System;
using _project.Scripts.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace _project.Scripts.Object_Scripts
{
    public class DebrisMiniBucket : MonoBehaviour, IDropHandler
    {
        [SerializeField] private DebrisMiniHandler.DebrisType type;

        private void OnEnable()
        {
            GameMaster.Instance.sifterMiniHandler.RegisterBucket(this);
        }

        private void OnDisable()
        {
            GameMaster.Instance.sifterMiniHandler.UnregisterBucket(this);
        }

        public void OnDrop(PointerEventData eventData)
        {
            var handler = eventData.pointerDrag?.GetComponent<DebrisMiniHandler>();
            if (handler)
                handler.HandleBucketDrop(type);
        }
    }
}
