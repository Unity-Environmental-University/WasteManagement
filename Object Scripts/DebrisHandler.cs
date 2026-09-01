using System.Collections;
using _project.Scripts.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace _project.Scripts.Object_Scripts
{
    public class DebrisHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public enum DebrisType
        {
            Incinerate,
            Landfill,
            CatchAndRelease
        }

        [SerializeField] private Image debrisImage;
        [SerializeField] private DebrisType type;

        private bool handledByBucket;
        private Coroutine strayReset;
        private CanvasGroup canvasGroup;
        private RectTransform rectTransform;
        private Vector2 originalAnchoredPos;

        private void OnEnable()
        {
            var rand = Random.Range(0, 3);
            type = (DebrisType)rand;

            GameMaster.Instance.sifterMiniController.RegisterHandler(this);

            if (!rectTransform) rectTransform = (RectTransform)transform;

            originalAnchoredPos = rectTransform.anchoredPosition;
            canvasGroup = GetComponent<CanvasGroup>();
            if (!canvasGroup) Debug.LogError("No CANVAS GROUP");
        }

        private void OnDisable()
        {
            GameMaster.Instance.sifterMiniController.UnregisterHandler(this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (strayReset is not null)
            {
                StopCoroutine(strayReset);
                strayReset = null;
            }

            handledByBucket = false;
            canvasGroup.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            transform.position = Mouse.current.position.ReadValue();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            canvasGroup.blocksRaycasts = true;

            if (!handledByBucket)
                strayReset = StartCoroutine(CollectStray());
        }

        public void HandleBucketDrop(DebrisType bucketType)
        {
            handledByBucket = true;

            if (bucketType == type)
                Destroy(gameObject);
            else
                ResetPos();
        }

        public DebrisType GetDebrisType() => type;

        public void ResetPos()
        {
            rectTransform.anchoredPosition = originalAnchoredPos;
        }

        private IEnumerator CollectStray()
        {
            yield return new WaitForSeconds(1f);

            ResetPos();
        }
    }
}
