using _project.Scripts.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _project.Scripts.Object_Scripts
{
    public class SpecialInteractController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerClickHandler
    {
        [Header("Slot Config")]
        [SerializeField] private PlaceableType acceptedType = PlaceableType.Any;

        [Header("Visuals")]
        [SerializeField] private Renderer slotRenderer;
        [SerializeField] private Color hoverColor = new Color(0.4f, 0.8f, 1f, 0.6f);
        [SerializeField] private Color occupiedColor = new Color(1f, 0.4f, 0.4f, 0.6f);
        private Color _defaultColor;

        [Header("Sifter Slot (optional)")]
        [SerializeField] private Slider associatedHealthBar;

        private bool _isOccupied;
        private static bool Debugging => GameMaster.Instance.debugging;

        private void Start()
        {
            if (slotRenderer) _defaultColor = slotRenderer.material.color;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            var pending = GameMaster.Instance.PendingPlacement;
            if (pending == null || !CanAccept(pending)) return;
            if (slotRenderer)
                slotRenderer.material.color = _isOccupied ? occupiedColor : hoverColor;
            if (Debugging) Debug.Log($"[SpecialInteract] Hovering — pending: {pending.PlaceableType}");
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (slotRenderer) slotRenderer.material.color = _defaultColor;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            var pending = GameMaster.Instance.PendingPlacement;
            if (pending == null) return;

            if (_isOccupied)
            {
                if (Debugging) Debug.Log("[SpecialInteract] Slot already occupied.");
                return;
            }

            if (!CanAccept(pending))
            {
                if (Debugging)
                    Debug.Log($"[SpecialInteract] Wrong type. Slot={acceptedType}, item={pending.PlaceableType}");
                return;
            }

            var placed = pending.Place(transform);

            if (pending.PlaceableType == PlaceableType.Sifter && associatedHealthBar && placed)
                GameMaster.Instance.pipCompMan.AssignHealthBar(placed, associatedHealthBar);

            _isOccupied = true;
            GameMaster.Instance.placementInventory.ConsumeSelected();

            if (slotRenderer) slotRenderer.material.color = occupiedColor;
            if (Debugging) Debug.Log($"[SpecialInteract] Placed {pending.PlaceableType} at {name}.");
        }

        private bool CanAccept(IPlaceable item)
        {
            if (item == null || item.PlaceableType == PlaceableType.Path)
                return false;

            return acceptedType == PlaceableType.Any || acceptedType == item.PlaceableType;
        }
    }
}
