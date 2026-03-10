using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace _project.Scripts.Card_Core
{
    public interface ICard
    {
        string Name { get; }
        string Description => null;
        bool IsFoil => false;
        Image CardImage => null;
        Material Material => null;
        
        ICard Clone();
        void Selected() { }
    }

    public class FoilCard : ICard
    {
        public FoilCard(ICard inner) { Inner = inner; }
        private ICard Inner { get; }

        public bool IsFoil => true;
        public string Name => Inner.Name;
        public string Description => Inner.Description;

        public Image CardImage => Inner.CardImage;
        public Material Material => Inner.Material;

        public ICard Clone() => new FoilCard(Inner.Clone());
        
        public void Selected() => Inner.Selected();
    }

    #region Decks

    public class CardHand : List<ICard>
    {
        public CardHand(string name, List<ICard> deck, List<ICard> prototypeDeck)
        {
            Name = name;
            Deck = deck;
            PrototypeDeck = prototypeDeck;
        }

        public string Name { get; }
        private List<ICard> Deck { get; }
        private List<ICard> PrototypeDeck { get; }

        public void DrawCards(int number)
        {
            if (Deck.Count == 0) return;
            number = Mathf.Min(number, Deck.Count);
            for (var i = 0; i < number; i++)
            {
                var drawnCard = Deck[0];
                Add(drawnCard);
                Deck.RemoveAt(0);
            }
        }
    }

    #endregion
}