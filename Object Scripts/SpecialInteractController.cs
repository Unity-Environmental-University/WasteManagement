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
        private bool _isHovered;
        private Collider _slotCollider;
        private PlacementInventory _placementInventory;
        private static bool Debugging => GameMaster.Instance.debugging;

        private void Awake()
        {
            _slotCollider = GetComponent<Collider>();
            if (slotRenderer) _defaultColor = slotRenderer.material.color;
        }

        private void OnEnable()
        {
            BindInventory();
            RefreshInteractionState();
        }

        private void Start()
        {
            RefreshInteractionState();
        }

        private void OnDisable()
        {
            if (_placementInventory != null)
                _placementInventory.SelectionChanged -= HandleSelectionChanged;

            _placementInventory = null;
            _isHovered = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            var pending = GameMaster.Instance.PendingPlacement;
            if (pending == null || !CanAccept(pending)) return;
            _isHovered = true;
            UpdateVisualState(pending);
            if (Debugging) Debug.Log($"[SpecialInteract] Hovering — pending: {pending.PlaceableType}");
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            RefreshInteractionState();
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

            if (pending.PlaceableType == PlaceableType.Sifter && placed && placed.TryGetComponent<WasteSifter>(out var sifter))
                sifter.SetSlot(this);

            _isOccupied = true;
            _isHovered = false;
            GameMaster.Instance.placementInventory.ConsumeSelected();

            RefreshInteractionState();
            if (Debugging) Debug.Log($"[SpecialInteract] Placed {pending.PlaceableType} at {name}.");
        }

        private bool CanAccept(IPlaceable item)
        {
            if (item == null || item.PlaceableType == PlaceableType.Path)
                return false;

            return acceptedType == PlaceableType.Any || acceptedType == item.PlaceableType;
        }

        public void ClearOccupied()
        {
            if (!_isOccupied) return;

            _isOccupied = false;
            RefreshInteractionState();
        }

        private void BindInventory()
        {
            var inventory = GameMaster.Instance ? GameMaster.Instance.placementInventory : null;
            if (_placementInventory == inventory) return;

            if (_placementInventory != null)
                _placementInventory.SelectionChanged -= HandleSelectionChanged;

            _placementInventory = inventory;

            if (_placementInventory != null)
                _placementInventory.SelectionChanged += HandleSelectionChanged;
        }

        private void HandleSelectionChanged(IPlaceable pending)
        {
            RefreshInteractionState(pending);
        }

        private void Update()
        {
            if (_placementInventory == null && GameMaster.Instance != null)
                RefreshInteractionState();
        }

        private void RefreshInteractionState(IPlaceable pending = null)
        {
            BindInventory();

            if (GameMaster.Instance == null)
                return;

            pending ??= GameMaster.Instance ? GameMaster.Instance.PendingPlacement : null;

            var canInteract = CanAccept(pending);
            if (!canInteract) _isHovered = false;

            if (_slotCollider) _slotCollider.enabled = canInteract;
            UpdateVisualState(pending);
        }

        private void UpdateVisualState(IPlaceable pending)
        {
            if (!slotRenderer) return;

            if (_isOccupied)
            {
                slotRenderer.material.color = occupiedColor;
                return;
            }

            slotRenderer.material.color = _isHovered && CanAccept(pending) ? hoverColor : _defaultColor;
        }
    }
}
