using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace _project.Scripts.Core
{
    public class DeckManager : MonoBehaviour
    {
        private static DeckManager Instance { get; set; }
        private static bool Debugging => GameMaster.Instance.debugging;

        [SerializeField] private int cardsDrawnPerTurn = 4;

        // Decks
        private readonly List<ICard> _deck = new();
        private readonly List<ICard> _hand = new();
        private readonly List<ICard> _discardPile = new();

        // Prototype decks
        private readonly List<ICard> _startingDeck = new()
        {
            new TestCard(),
            new TestCard(),
            new TestCard(),
            new TestCard(),
            new TestCard()
        };

        public IReadOnlyList<ICard> Hand => _hand;
        public int DeckCount => _deck.Count;
        public int DiscardCount => _discardPile.Count;

        private void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            InitializeDeck();
        }

        private void InitializeDeck()
        {
            _deck.Clear();

            foreach (var card in _startingDeck.Where(c => c != null))
                _deck.Add(card.Clone());

            Shuffle(_deck);

            if (Debugging)
                Debug.Log($"[DeckManager] Initialized {_deck.Count} cards.");
        }

        /// Discard the current hand and draw a new one. Call at the start of each turn.
        public void DrawNewHand()
        {
            DiscardHand();
            DrawCards(cardsDrawnPerTurn);
        }

        /// Draw N cards from the deck into hand, recycling the discard pile if needed.
        private void DrawCards(int count)
        {
            for (var i = 0; i < count; i++)
            {
                if (_deck.Count == 0)
                {
                    if (_discardPile.Count == 0) break;
                    RecycleDiscard();
                }

                var card = _deck[0];
                _deck.RemoveAt(0);
                _hand.Add(card);
            }

            if (Debugging)
                Debug.Log($"[DeckManager] Hand ({_hand.Count}): {string.Join(", ", _hand.ConvertAll(c => c.Name))}");
        }

        public void DiscardCard(ICard card)
        {
            if (!_hand.Remove(card))
            {
                if (Debugging) Debug.LogWarning($"[DeckManager] DiscardCard: {card.Name} not in hand.");
                return;
            }

            _discardPile.Add(card);
            if (Debugging) Debug.Log($"[DeckManager] Discarded: {card.Name}");
        }

        private void DiscardHand()
        {
            _discardPile.AddRange(_hand);
            _hand.Clear();
        }


        private void RecycleDiscard()
        {
            if (Debugging) Debug.Log($"[DeckManager] Recycling {_discardPile.Count} cards into deck.");
            _deck.AddRange(_discardPile);
            _discardPile.Clear();
            Shuffle(_deck);
        }

        /// Fisher-Yates shuffle.
        private static void Shuffle(List<ICard> deck)
        {
            var n = deck.Count;
            while (n > 1)
            {
                var k = Random.Range(0, n--);
                (deck[n], deck[k]) = (deck[k], deck[n]);
            }
        }

        public List<ICard> GetDeck() => new(_deck);
        public List<ICard> GetHand() => new(_hand);
        public List<ICard> GetDiscardPile() => new(_discardPile);
    }
}