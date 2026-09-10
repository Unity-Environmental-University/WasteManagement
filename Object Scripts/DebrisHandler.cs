using System.Collections;
using _project.Scripts.Core;
using UnityEngine;
using UnityEngine.EventSystems;
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

        private bool _handledByBucket;
        private Coroutine _strayReset;
        private CanvasGroup _canvasGroup;
        private RectTransform _rectTransform;
        private Vector2 _originalAnchoredPos;

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
            _rectTransform = (RectTransform)transform;
            _originalAnchoredPos = _rectTransform.anchoredPosition;

            _canvasGroup = GetComponent<CanvasGroup>();
            if (!_canvasGroup) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            type = (DebrisType)Random.Range(0, 3);

            // A sorted piece is deactivated rather than destroyed, so the next round can reuse it, so
            // put it back where it started and make it draggable again before it returns to play.
            _handledByBucket = false;
            _strayReset = null;
            _canvasGroup.blocksRaycasts = true;
            ResetPos();

            var controller = Controller;
            if (controller) controller.RegisterHandler(this);
            else Debug.LogWarning($"{name}: no SifterMiniGameController available to register with.", this);
        }

        private void OnDisable()
        {
            var controller = Controller;
            if (controller) controller.UnregisterHandler(this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_strayReset is not null)
            {
                StopCoroutine(_strayReset);
                _strayReset = null;
            }

            _handledByBucket = false;
            _canvasGroup.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            transform.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _canvasGroup.blocksRaycasts = true;

            if (!_handledByBucket)
                _strayReset = StartCoroutine(CollectStray());
        }

        public void HandleBucketDrop(DebrisType bucketType)
        {
            _handledByBucket = true;

            var controller = Controller;

            // Sorting a piece away is only meaningful inside a round: the controller ends the game when
            // its roster empties, and a roster is only built by StartMiniGame. Bounce the piece back
            // otherwise, so a panel left live outside a round cannot be emptied into a stuck state.
            if (bucketType != type || !controller || !controller.IsRunning)
            {
                ResetPos();
                return;
            }

            gameObject.SetActive(false);
        }

        public DebrisType GetDebrisType() => type;

        public void ResetPos()
        {
            _rectTransform.anchoredPosition = _originalAnchoredPos;
        }

        private IEnumerator CollectStray()
        {
            yield return new WaitForSeconds(1f);

            ResetPos();
        }
    }
}
