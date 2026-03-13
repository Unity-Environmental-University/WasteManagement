using _project.Scripts.Core;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace _project.Scripts.UI
{
    public class CardController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private GameMaster _gm;
        private RectTransform _rectTransform;
        private Vector2 _initAnchoredPosition;
        private const float JumpHeightFraction = 0.8f;

        public ICard interFaceCard;

        private void Start()
        {
            _gm = GameMaster.Instance;
            _rectTransform = GetComponent<RectTransform>();
            _initAnchoredPosition = _rectTransform.anchoredPosition;
            
            //TODO Delete Me
            AssignCard(new TestCard());
        }

        public void AssignCard(ICard card)
        {
            interFaceCard = card;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            var offset = _rectTransform.rect.height * JumpHeightFraction;
            _rectTransform.DOLocalMoveY(_initAnchoredPosition.y + offset, 0.1f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_gm.SelectedCard == this) return;

            _rectTransform.DOLocalMoveY(_initAnchoredPosition.y, 0.1f);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_gm.SelectedCard == this)
            {
                _gm.SelectedCard = null;
                _rectTransform.DOLocalMoveY(_initAnchoredPosition.y, 0.1f);
                return;
            }

            _gm.SelectedCard = this;
        }
    }
}