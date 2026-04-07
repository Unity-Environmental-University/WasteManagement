using System;
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
        void Purchase();
    }

    public interface IPlaceable
    {
        PlaceableType PlaceableType { get; }
        GameObject Place(Transform location);
    }

    public enum PlaceableType { Tower, Sifter, Any }

    [Serializable]
    public struct CardShopEntry
    {
        public string cardType; // "ChemicalSolvent" | "UpgradedMeshNet" | "SuperiorMaintenance"
        public int cost;
        public Sprite sprite;
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
        public PlaceableType PlaceableType => PlaceableType.Tower;

        public void Purchase()
        {
            ScoreManager.SpendTokens(Cost);
            GameMaster.Instance.pendingPlacement = this;
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
        public PlaceableType PlaceableType => PlaceableType.Sifter;

        public void Purchase()
        {
            ScoreManager.SpendTokens(Cost);
            GameMaster.Instance.pendingPlacement = this;
        }

        public GameObject Place(Transform location)
        {
            return Object.Instantiate(_prefab, location.position, location.rotation);
        }
    }
}
