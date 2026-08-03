using System;
using _project.Scripts.Object_Scripts;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _project.Scripts.Core
{
    public interface IShopItem
    {
        string DisplayName { get; }
        string Description { get; }
        int RequiredLevel { get; }
        int InfraValue { get; }
        Sprite DisplaySprite { get; }
        bool RemoveAfterPurchase { get; }
        void Purchase();
    }

    public interface IPlaceable : IShopItem
    {
        PlaceableType PlaceableType { get; }
        GameObject Place(Transform location);
        
    }

    public interface IPathPiecePlaceable : IPlaceable
    {
        int Length { get; }
        PathPieceOrientation Orientation { get; }
        void ToggleOrientation();
    }

    public enum PlaceableType
    {
        Tower = 0,
        Utility = 1,
        Any = 2,
        Path = 3,
        /// <summary>Consumed by clicking a target object directly; never placed via a board cell or utility slot.</summary>
        Targeted = 4
    }

    internal static class PlacementRules
    {
        public static bool TryGetPipeBoard(Transform location, out PathBuildBoard board)
        {
            board = GameMaster.Instance ? GameMaster.Instance.pathBuildBoard : null;
            return location && board && board.TryWorldToCell(location.position, out var cell) && board.IsOccupied(cell);
        }
    }

    [Serializable]
    public struct CardShopEntry
    {
        public string cardType; // "ChemicalSolvent" | "UpgradedMeshNet" | "SuperiorMaintenance"
        public int requiredLevel;
        public int infraValue;
        public Sprite sprite;
    }

    public class BlankShopItem : IShopItem
    {
        public BlankShopItem(Sprite displaySprite)
        {
            DisplaySprite = displaySprite;
        }

        public string DisplayName => "Placeholder Item";
        public string Description => "Temporary shop entry for layout testing.";
        public int RequiredLevel => 1;
        public int InfraValue => 1;
        public Sprite DisplaySprite { get; }
        public bool RemoveAfterPurchase => true;

        public void Purchase()
        {
        }
    }

    public class CardShopItem : IShopItem
    {
        private readonly ICard _card;

        public CardShopItem(ICard card, int requiredLevel, Sprite displaySprite, int infraValue)
        {
            _card = card;
            RequiredLevel = Mathf.Max(1, requiredLevel);
            DisplaySprite = displaySprite;
            InfraValue = infraValue;
        }

        public string DisplayName => _card.Name;
        public string Description => _card.Description ?? string.Empty;
        public int RequiredLevel { get; }
        public int InfraValue { get; }
        public Sprite DisplaySprite { get; }
        public bool RemoveAfterPurchase => true;

        public void Purchase()
        {
            GameMaster.Instance.deckManager.AddCard(_card.Clone());
        }
    }

    public class CesspitCapShopItem : IPlaceable
    {
        public CesspitCapShopItem(string displayName, string description, int requiredLevel, Sprite displaySprite)
        {
            DisplayName = displayName;
            Description = description;
            RequiredLevel = Mathf.Max(1, requiredLevel);
            DisplaySprite = displaySprite;
        }

        public string DisplayName { get; }
        public string Description { get; }
        public int RequiredLevel { get; }
        public int InfraValue => 0;
        public Sprite DisplaySprite { get; }
        public bool RemoveAfterPurchase => true;
        public PlaceableType PlaceableType => PlaceableType.Targeted;

        public void Purchase()
        {
            GameMaster.Instance.placementInventory.Add(this);
        }

        // Caps are applied by clicking a Cesspit, never by a board or utility slot.
        public GameObject Place(Transform location) => null;
    }

    public class BuryCesspitShopItem : IPlaceable
    {
        public BuryCesspitShopItem(string displayName, string description, int requiredLevel, Sprite displaySprite)
        {
            DisplayName = displayName;
            Description = description;
            RequiredLevel = Mathf.Max(1, requiredLevel);
            DisplaySprite = displaySprite;
        }

        public string DisplayName { get; }
        public string Description { get; }
        public int RequiredLevel { get; }
        public int InfraValue => 0;
        public Sprite DisplaySprite { get; }
        public bool RemoveAfterPurchase => true;
        public PlaceableType PlaceableType => PlaceableType.Targeted;

        public void Purchase()
        {
            GameMaster.Instance.placementInventory.Add(this);
        }

        // Burials are applied by clicking a Cesspit, never by a board or utility slot.
        public GameObject Place(Transform location) => null;
    }

    public class TowerShopItem : IPlaceable
    {
        private readonly GameObject _prefab;

        public TowerShopItem(string displayName, string description, int requiredLevel, GameObject prefab,
            Sprite displaySprite, int infraValue)
        {
            DisplayName = displayName;
            Description = description;
            RequiredLevel = Mathf.Max(1, requiredLevel);
            _prefab = prefab;
            DisplaySprite = displaySprite;
            InfraValue = infraValue;
        }

        public string DisplayName { get; }
        public string Description { get; }
        public int RequiredLevel { get; }
        public int InfraValue { get; }
        public Sprite DisplaySprite { get; }
        public bool RemoveAfterPurchase => true;
        public PlaceableType PlaceableType => PlaceableType.Tower;

        public void Purchase()
        {
            GameMaster.Instance.placementInventory.Add(this);
        }

        public GameObject Place(Transform location)
        {
            if (!PlacementRules.TryGetPipeBoard(location, out _)) return null;

            var go = Object.Instantiate(_prefab, location.position, location.rotation);
            var tc = go.GetComponent<TowerController>();
            if (tc) GameMaster.Instance.towerManager.RegisterTower(tc);
            return go;
        }
    }

    public class SifterShopItem : IPlaceable
    {
        private readonly GameObject _prefab;

        public SifterShopItem(string displayName, string description, int requiredLevel, GameObject prefab,
            Sprite displaySprite, int infraValue)
        {
            DisplayName = displayName;
            Description = description;
            RequiredLevel = Mathf.Max(1, requiredLevel);
            _prefab = prefab;
            DisplaySprite = displaySprite;
            InfraValue = infraValue;
        }

        public string DisplayName { get; }
        public string Description { get; }
        public int RequiredLevel { get; }
        public int InfraValue { get; }
        public Sprite DisplaySprite { get; }
        public bool RemoveAfterPurchase => true;
        public PlaceableType PlaceableType => PlaceableType.Utility;

        public void Purchase()
        {
            GameMaster.Instance.placementInventory.Add(this);
        }

        public GameObject Place(Transform location)
        {
            if (!PlacementRules.TryGetPipeBoard(location, out var board)) return null;

            var rotation = location.rotation;
            if (board.TryGetPathFacingRotation(location.position, out var pathRotation))
                rotation = pathRotation;

            return Object.Instantiate(_prefab, location.position, rotation);
        }
    }

    public class PathSplitterShopItem : IPlaceable
    {
        private readonly GameObject _prefab;

        public PathSplitterShopItem(string displayName, string description, int requiredLevel, GameObject prefab,
            Sprite displaySprite, int infraValue)
        {
            DisplayName = displayName;
            Description = description;
            RequiredLevel = Mathf.Max(1, requiredLevel);
            _prefab = prefab;
            DisplaySprite = displaySprite;
            InfraValue = infraValue;
        }

        public string DisplayName { get; }
        public string Description { get; }
        public int RequiredLevel { get; }
        public int InfraValue { get; }
        public Sprite DisplaySprite { get; }
        public bool RemoveAfterPurchase => true;
        public PlaceableType PlaceableType => PlaceableType.Utility;

        public void Purchase()
        {
            GameMaster.Instance.placementInventory.Add(this);
        }

        public GameObject Place(Transform location)
        {
            if (!PlacementRules.TryGetPipeBoard(location, out var board)) return null;
            if (!board.IsPathSplitPoint(location.position)) return null;

            var rotation = location.rotation;
            if (board.TryGetPathFacingRotation(location.position, out var pathRotation))
                rotation = pathRotation;

            return Object.Instantiate(_prefab, location.position, rotation);
        }
    }

    public class CesspitShopItem : IPlaceable
    {
        private readonly GameObject _prefab;

        public CesspitShopItem(string displayName, string description, int requiredLevel, GameObject prefab, Sprite displaySprite, int infraValue)
        {
            DisplayName = displayName;
            Description = description;
            RequiredLevel = Mathf.Max(1, requiredLevel);
            _prefab = prefab;
            DisplaySprite = displaySprite;
            InfraValue = infraValue;
        }

        public string DisplayName { get; }
        public string Description { get; }
        public int RequiredLevel { get; }
        public int InfraValue { get; }
        public Sprite DisplaySprite { get; }
        public bool RemoveAfterPurchase => true;
        public PlaceableType PlaceableType => PlaceableType.Utility;

        public void Purchase()
        {
            GameMaster.Instance.placementInventory.Add(this);
        }

        public GameObject Place(Transform location)
        {
            if (!PlacementRules.TryGetPipeBoard(location, out _)) return null;

            return Object.Instantiate(_prefab, location.position, location.rotation);
        }
    }

    public class TreatmentTankShopItem : IPlaceable
    {
        private readonly GameObject _prefab;

        public TreatmentTankShopItem(string displayName, string description, int requiredLevel, GameObject prefab,
            Sprite displaySprite, int infraValue)
        {
            DisplayName = displayName;
            Description = description;
            RequiredLevel = Mathf.Max(1, requiredLevel);
            _prefab = prefab;
            DisplaySprite = displaySprite;
            InfraValue = infraValue;
        }

        public string DisplayName { get; }
        public string Description { get; }
        public int RequiredLevel { get; }
        public int InfraValue {get; }
        public Sprite DisplaySprite { get; }
        public bool RemoveAfterPurchase => true;
        public PlaceableType PlaceableType => PlaceableType.Utility;

        public void Purchase()
        {
            GameMaster.Instance.placementInventory.Add(this);
        }

        public GameObject Place(Transform location)
        {
            if (!PlacementRules.TryGetPipeBoard(location, out _)) return null;

            return Object.Instantiate(_prefab, location.position, location.rotation);
        }
    }

    public class LimeSprinklerShopItem : IPlaceable
    {
        private readonly GameObject _prefab;

        public LimeSprinklerShopItem(string displayName, string description, int requiredLevel, GameObject prefab,
            Sprite displaySprite, int infraValue)
        {
            DisplayName = displayName;
            Description = description;
            RequiredLevel = Mathf.Max(1, requiredLevel);
            _prefab = prefab;
            DisplaySprite = displaySprite;
            InfraValue = infraValue;
        }

        public string DisplayName { get; }
        public string Description { get; }
        public int RequiredLevel { get; }
        public int InfraValue { get; }
        public Sprite DisplaySprite { get; }
        public bool RemoveAfterPurchase => true;
        public PlaceableType PlaceableType => PlaceableType.Utility;

        public void Purchase()
        {
            GameMaster.Instance.placementInventory.Add(this);
        }

        public GameObject Place(Transform location)
        {
            var board = GameMaster.Instance ? GameMaster.Instance.pathBuildBoard : null;
            var rotation = location.rotation;
            if (board && board.TryGetPathFacingRotation(location.position, out var pathRotation))
                rotation = pathRotation;

            return Object.Instantiate(_prefab, location.position, rotation);
        }
    }

    public class PathPieceShopItem : IShopItem
    {
        private readonly int _length;

        public PathPieceShopItem(string displayName, string description, int requiredLevel, int length, Sprite displaySprite, int infraValue)
        {
            DisplayName = displayName;
            Description = description;
            RequiredLevel = Mathf.Max(1, requiredLevel);
            _length = length;
            DisplaySprite = displaySprite;
            InfraValue = infraValue;
        }

        public string DisplayName { get; }
        public string Description { get; }
        public int RequiredLevel { get; }
        public int InfraValue { get; }
        public Sprite DisplaySprite { get; }
        public bool RemoveAfterPurchase => false;

        public void Purchase()
        {
            var gm = GameMaster.Instance;
            var board = gm ? gm.pathBuildBoard : null;
            if (!board)
            {
                Debug.LogWarning("[PathPieceShopItem] No PathBuildBoard available; purchase ignored.");
                return;
            }

            gm.placementInventory?.ClearSelection();
            board.SetActivePiece(new PathPiecePlaceable(DisplayName, Description, RequiredLevel, _length,
                DisplaySprite, InfraValue));
        }
    }

    public class PathBreakShopItem : IShopItem
    {
        public PathBreakShopItem(string displayName, string description, int requiredLevel, Sprite displaySprite)
        {
            DisplayName = displayName;
            Description = description;
            RequiredLevel = Mathf.Max(1, requiredLevel);
            DisplaySprite = displaySprite;
        }

        public string DisplayName { get; }
        public string Description { get; }
        public int RequiredLevel { get; }
        public int InfraValue => 0;
        public Sprite DisplaySprite { get; }
        public bool RemoveAfterPurchase => false;

        public void Purchase()
        {
            var gm = GameMaster.Instance;
            var board = gm ? gm.pathBuildBoard : null;
            if (!board)
            {
                Debug.LogWarning("[PathBreakShopItem] No PathBuildBoard available; purchase ignored.");
                return;
            }

            gm.placementInventory?.ClearSelection();
            board.SetActiveBreakTool();
        }
    }

    public class PathPiecePlaceable : IPathPiecePlaceable
    {
        public PathPiecePlaceable(string displayName, string description, int requiredLevel, int length,
            Sprite displaySprite, int infraValue)
        {
            DisplayName = displayName;
            Description = description;
            RequiredLevel = Mathf.Max(1, requiredLevel);
            Length = Mathf.Max(2, length);
            DisplaySprite = displaySprite;
            InfraValue = infraValue;
        }

        public string DisplayName { get; }
        public string Description { get; }
        public int RequiredLevel { get; }
        public int InfraValue { get; }
        public Sprite DisplaySprite { get; }
        public bool RemoveAfterPurchase => true;
        public int Length { get; }
        public PathPieceOrientation Orientation { get; private set; } = PathPieceOrientation.Horizontal;
        public PlaceableType PlaceableType => PlaceableType.Path;

        public void Purchase() { }

        public void ToggleOrientation()
        {
            Orientation = Orientation == PathPieceOrientation.Horizontal
                ? PathPieceOrientation.Vertical
                : PathPieceOrientation.Horizontal;
        }

        public GameObject Place(Transform location)
        {
            if (!location) return null;
            return !location.TryGetComponent<PathBuildCell>(out var cell) ? null : cell.TryPlace(this);
        }
    }
}
