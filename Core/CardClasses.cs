using System.Collections.Generic;
using UnityEngine;

namespace _project.Scripts.Core
{
    public interface ICard
    {
        string Name { get; }
        string Description => null;
        bool IsFoil => false;
        Sprite CardSprite => null;
        Material Material => null;

        ICard Clone();
        public void ProcessEffect(TowerController tC);
        void Selected() { }
    }

    public class FoilCard : ICard
    {
        public FoilCard(ICard inner) { Inner = inner; }
        private ICard Inner { get; }

        public bool IsFoil => true;
        public string Name => Inner.Name;
        public string Description => Inner.Description;

        public Sprite CardSprite => Inner.CardSprite;
        public Material Material => Inner.Material;

        public ICard Clone() => new FoilCard(Inner.Clone());
        
        public void ProcessEffect(TowerController tC) => Inner.ProcessEffect(tC);

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
    

    public class ChemicalSolvent : ICard
    {
        public string Name => "Chemical Solvent";
        public ICard Clone()
        {
            return new ChemicalSolvent();
        }

        public void ProcessEffect(TowerController tC)
        {
            var baseP = tC.chemicalProcessPower;
            baseP += (float)(baseP * .15);
            
            tC.chemicalProcessPower = baseP;
        }
    }

    public class UpgradedMeshNet : ICard
    {
        public string Name => "Upgraded Mesh Net";
        public ICard Clone()
        {
            return new UpgradedMeshNet();
        }

        public void ProcessEffect(TowerController tC)
        {
            var baseP = tC.organicProcessPower;
            baseP += (float)(baseP * .15);
            
            tC.organicProcessPower = baseP;
        }
    }

    public class SuperiorMaintenance : ICard
    {
        public string Name => "Superior Maintenance";
        public ICard Clone()
        {
            return new SuperiorMaintenance();
        }

        public void ProcessEffect(TowerController tC)
        {
            var baseRegen = tC.maintenanceRegen;
            var baseChemPower = tC.chemicalProcessPower;
            baseRegen += (float)(baseRegen * .35);
            baseChemPower -= (float)(baseChemPower * .25);
            
            tC.maintenanceRegen = baseRegen;
            tC.chemicalProcessPower = baseChemPower;
        }
    }
}