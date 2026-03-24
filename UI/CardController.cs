using _project.Scripts.Core;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _project.Scripts.UI
{
    public class CardController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private GameMaster _gm;
        private RectTransform _rectTransform;
        private Vector2 _initAnchoredPosition;
        private const float JumpHeightFraction = 0.8f;

        [SerializeField] private Image artImage;
        [SerializeField] private CardSpriteLibrary spriteLibrary;

        public ICard InterFaceCard;

        private void Start()
        {
            _gm = GameMaster.Instance;
            _rectTransform = GetComponent<RectTransform>();
            _initAnchoredPosition = _rectTransform.anchoredPosition;
        }

        public void AssignCard(ICard card)
        {
            InterFaceCard = card;

            if (artImage != null && spriteLibrary != null)
                artImage.sprite = spriteLibrary.GetSprite(card.Name);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            var offset = _rectTransform.rect.height * JumpHeightFraction;
            _rectTransform.DOLocalMoveY(_initAnchoredPosition.y + offset, 0.1f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_gm.selectedCard == this) return;

            _rectTransform.DOLocalMoveY(_initAnchoredPosition.y, 0.1f);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_gm.selectedCard == this)
            {
                _gm.selectedCard = null;
                _rectTransform.DOLocalMoveY(_initAnchoredPosition.y, 0.1f);
                return;
            }

            _gm.selectedCard = this;
        }
    }
}