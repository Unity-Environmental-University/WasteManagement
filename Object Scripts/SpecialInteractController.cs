using _project.Scripts.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
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
        private Color _defaultColor;

        [Header("Utility Slot (optional)")]
        [FormerlySerializedAs("associatedHealthBar")]
        [SerializeField] private Slider associatedStatusBar;

        private bool _isHovered;
        private bool _showingAreaPreview;
        private Collider _slotCollider;
        private PlacementInventory _placementInventory;
        private MaterialPropertyBlock _slotColorPropertyBlock;
        private static bool Debugging => GameMaster.Instance.debugging;

        public PlaceableType AcceptedType => acceptedType;
        public bool IsOccupied { get; private set; }

        private void Awake()
        {
            _slotCollider = GetComponent<Collider>();
            if (!slotRenderer) slotRenderer = GetComponentInChildren<Renderer>();
            // sharedMaterial: reading .material here would clone an instance just to learn the color.
            if (slotRenderer && slotRenderer.sharedMaterial) _defaultColor = slotRenderer.sharedMaterial.color;
        }

        private void OnEnable()
        {
            LiveComponentRegistry.Register(this);
            BindInventory();
            RefreshInteractionState();
        }

        private void Start()
        {
            RefreshInteractionState();
        }

        private void OnDisable()
        {
            LiveComponentRegistry.Unregister(this);
            if (_placementInventory != null)
                _placementInventory.SelectionChanged -= HandleSelectionChanged;

            _placementInventory = null;
            _isHovered = false;
            RefreshAreaPreview(null);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            var pending = GameMaster.Instance.PendingPlacement;
            if (pending == null || !CanAccept(pending)) return;
            _isHovered = true;
            UpdateVisualState(pending);
            RefreshAreaPreview(pending);
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

            if (IsOccupied)
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
            if (placed is null) return;

            if (pending.PlaceableType == PlaceableType.Utility && associatedStatusBar && placed)
                GameMaster.Instance.pipCompMan.AssignHealthBar(placed, associatedStatusBar);

            if (pending.PlaceableType == PlaceableType.Utility && placed &&
                placed.TryGetComponent<IRemovableUtility>(out var utility))
                utility.SetSlot(this, pending.InfraValue);

            IsOccupied = true;
            _isHovered = false;
            var gm = GameMaster.Instance;
            gm.CompletePlacement(placed);
            gm.turnController.infrastructureValue += pending.InfraValue;
            gm.interfaceManager?.RefreshStinkMeter();
            RefreshInteractionState();
            if (Debugging) Debug.Log($"[SpecialInteract] Placed {pending.PlaceableType} at {name}.");
        }

        private bool CanAccept(IPlaceable item)
        {
            if (item == null || item.PlaceableType == PlaceableType.Path || item.PlaceableType == PlaceableType.Targeted)
                return false;

            return acceptedType == PlaceableType.Any || acceptedType == item.PlaceableType;
        }

        public void ClearOccupied(int infraValue = 0)
        {
            if (!IsOccupied) return;

            IsOccupied = false;
            if (infraValue > 0 && GameMaster.Instance && GameMaster.Instance.turnController)
                GameMaster.Instance.turnController.infrastructureValue -= infraValue;
            GameMaster.Instance?.interfaceManager?.RefreshStinkMeter();
            RefreshInteractionState();
        }

        /// <summary>
        ///     Reapplies this slot's occupied and phase visibility after an external system changes
        ///     its renderer, such as TurnController entering the placement phase.
        /// </summary>
        public void RefreshVisibility()
        {
            UpdateVisualState(GameMaster.Instance ? GameMaster.Instance.PendingPlacement : null);
        }

        private void BindInventory()
        {
            var inventory = GameMaster.Instance ? GameMaster.Instance.placementInventory : null;
            if (_placementInventory == inventory) return;

            if (_placementInventory is not null)
                _placementInventory.SelectionChanged -= HandleSelectionChanged;

            _placementInventory = inventory;

            if (_placementInventory is not null)
                _placementInventory.SelectionChanged += HandleSelectionChanged;
        }

        private void HandleSelectionChanged(IPlaceable pending)
        {
            RefreshInteractionState(pending);
        }

        private void Update()
        {
            if (_placementInventory is null && GameMaster.Instance is not null)
                RefreshInteractionState();
        }

        private void RefreshInteractionState(IPlaceable pending = null)
        {
            BindInventory();

            if (GameMaster.Instance is null)
                return;

            pending ??= GameMaster.Instance ? GameMaster.Instance.PendingPlacement : null;

            var canInteract = CanAccept(pending);
            if (!canInteract) _isHovered = false;

            if (_slotCollider) _slotCollider.enabled = canInteract;
            UpdateVisualState(pending);
            RefreshAreaPreview(pending);
        }

        /// <summary>
        ///     Previews the cells a lime sprinkler would cover while one is pending and this empty
        ///     slot is hovered. Only hides a preview this slot showed, so a slot exiting after its
        ///     neighbour was entered can't wipe the neighbour's preview.
        /// </summary>
        private void RefreshAreaPreview(IPlaceable pending)
        {
            if (_isHovered && !IsOccupied && pending is LimeSprinklerShopItem sprinkler)
            {
                LimeSprinkler.ShowPlacementPreview(sprinkler.SprinklerPrefab, transform.position);
                _showingAreaPreview = true;
            }
            else if (_showingAreaPreview)
            {
                LimeSprinkler.HidePlacementPreview();
                _showingAreaPreview = false;
            }
        }

        private void UpdateVisualState(IPlaceable pending)
        {
            if (!slotRenderer) return;

            var turnController = GameMaster.Instance ? GameMaster.Instance.turnController : null;
            var runInProgress = turnController && turnController.currentPhase == GamePhase.Tower;
            slotRenderer.enabled = !IsOccupied && !runInProgress;
            if (IsOccupied || runInProgress)
                return;

            // Property-block tint — `.material.color` would clone a material instance per slot.
            RendererColorUtility.SetColor(slotRenderer,
                _isHovered && CanAccept(pending) ? hoverColor : _defaultColor, ref _slotColorPropertyBlock);
        }
    }
}
