using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace _project.Scripts.UI
{
    public class CardController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private RectTransform _rectTransform;
        private Vector2 _initAnchoredPosition;
        private const float JumpHeightFraction = 0.8f;

        private void Start()
        {
            _rectTransform = GetComponent<RectTransform>();
            _initAnchoredPosition = _rectTransform.anchoredPosition;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            var offset = _rectTransform.rect.height * JumpHeightFraction;
            _rectTransform.DOLocalMoveY(_initAnchoredPosition.y + offset, 0.1f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _rectTransform.DOLocalMoveY(_initAnchoredPosition.y, 0.1f);
        }
    }
}