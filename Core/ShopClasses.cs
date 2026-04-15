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
        int Cost { get; }
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
        Sifter = 1,
        Any = 2,
        Path = 3
    }

    [Serializable]
    public struct CardShopEntry
    {
        public string cardType; // "ChemicalSolvent" | "UpgradedMeshNet" | "SuperiorMaintenance"
        public int cost;
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
        public int Cost => 0;
        public Sprite DisplaySprite { get; }
        public bool RemoveAfterPurchase => true;

        public void Purchase()
        {
        }
    }

    public class CardShopItem : IShopItem
    {
        private readonly ICard _card;

        public CardShopItem(ICard card, int cost, Sprite displaySprite)
        {
            _card = card;
            Cost = cost;
            DisplaySprite = displaySprite;
        }

        public string DisplayName => _card.Name;
        public string Description => _card.Description ?? string.Empty;
        public int Cost { get; }
        public Sprite DisplaySprite { get; }
        public bool RemoveAfterPurchase => true;

        public void Purchase()
        {
            ScoreManager.SpendTokens(Cost);
            GameMaster.Instance.deckManager.AddCard(_card.Clone());
        }
    }

    public class TowerShopItem : IShopItem, IPlaceable
    {
        private readonly GameObject _prefab;

        public TowerShopItem(string displayName, string description, int cost, GameObject prefab, Sprite displaySprite)
        {
            DisplayName = displayName;
            Description = description;
            Cost = cost;
            _prefab = prefab;
            DisplaySprite = displaySprite;
        }

        public string DisplayName { get; }
        public string Description { get; }
        public int Cost { get; }
        public Sprite DisplaySprite { get; }
        public bool RemoveAfterPurchase => true;
        public PlaceableType PlaceableType => PlaceableType.Tower;

        public void Purchase()
        {
            ScoreManager.SpendTokens(Cost);
            GameMaster.Instance.placementInventory.Add(this);
        }

        public GameObject Place(Transform location)
        {
            var go = Object.Instantiate(_prefab, location.position, location.rotation);
            var tc = go.GetComponent<TowerController>();
            if (tc) GameMaster.Instance.towerManager.RegisterTower(tc);
            return go;
        }
    }

    public class SifterShopItem : IShopItem, IPlaceable
    {
        private readonly GameObject _prefab;

        public SifterShopItem(string displayName, string description, int cost, GameObject prefab, Sprite displaySprite)
        {
            DisplayName = displayName;
            Description = description;
            Cost = cost;
            _prefab = prefab;
            DisplaySprite = displaySprite;
        }

        public string DisplayName { get; }
        public string Description { get; }
        public int Cost { get; }
        public Sprite DisplaySprite { get; }
        public bool RemoveAfterPurchase => true;
        public PlaceableType PlaceableType => PlaceableType.Sifter;

        public void Purchase()
        {
            ScoreManager.SpendTokens(Cost);
            GameMaster.Instance.placementInventory.Add(this);
        }

        public GameObject Place(Transform location)
        {
            return Object.Instantiate(_prefab, location.position, location.rotation);
        }
    }

    public class PathPieceShopItem : IShopItem
    {
        private readonly int _length;

        public PathPieceShopItem(string displayName, string description, int cost, int length, Sprite displaySprite)
        {
            DisplayName = displayName;
            Description = description;
            Cost = cost;
            _length = length;
            DisplaySprite = displaySprite;
        }

        public string DisplayName { get; }
        public string Description { get; }
        public int Cost { get; }
        public Sprite DisplaySprite { get; }
        public bool RemoveAfterPurchase => false;

        public void Purchase()
        {
            ScoreManager.SpendTokens(Cost);
            var placeable = new PathPiecePlaceable(DisplayName, Description, Cost, _length, DisplaySprite);
            GameMaster.Instance.placementInventory.Add(placeable);
        }
    }

    public class PathPiecePlaceable : IPathPiecePlaceable
    {
        public PathPiecePlaceable(string displayName, string description, int cost, int length, Sprite displaySprite)
        {
            DisplayName = displayName;
            Description = description;
            Cost = cost;
            Length = Mathf.Max(2, length);
            DisplaySprite = displaySprite;
        }

        public string DisplayName { get; }
        public string Description { get; }
        public int Cost { get; }
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
