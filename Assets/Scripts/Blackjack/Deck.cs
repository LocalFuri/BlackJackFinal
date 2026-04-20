using System.Collections.Generic;
using UnityEngine;

namespace Blackjack
{
    /// <summary>
    /// Single 52-card deck. Shuffles on every Build() call.
    /// </summary>
    public class Deck
    {
        private readonly List<CardData> _cards = new(52);

        public int Remaining => _cards.Count;

        /// <summary>Builds and shuffles a fresh 52-card deck.</summary>
        public void Build()
        {
            _cards.Clear();
            foreach (Suit suit in System.Enum.GetValues(typeof(Suit)))
                foreach (Rank rank in System.Enum.GetValues(typeof(Rank)))
                    _cards.Add(new CardData(suit, rank));

            Shuffle();
        }

        /// <summary>Fisher-Yates in-place shuffle.</summary>
        public void Shuffle()
        {
            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
            }
        }

        /// <summary>Draws the top card. Throws if deck is empty.</summary>
        public CardData Draw()
        {
            if (_cards.Count == 0)
                throw new System.InvalidOperationException("Deck is empty.");

            CardData card = _cards[^1];
            _cards.RemoveAt(_cards.Count - 1);
            return card;
        }

        /// <summary>
        /// Manipulates the top of the deck so the player's first two cards
        /// will be Ace of Spades and King of Hearts (natural blackjack).
        /// </summary>
        public void ForcePlayerBlackjack()
        {
            PlaceAtIndex(new CardData(Suit.Spades, Rank.Ace),  _cards.Count - 1);
            PlaceAtIndex(new CardData(Suit.Hearts, Rank.King), _cards.Count - 3);
        }

        /// <summary>
        /// Manipulates the top of the deck so the player's first two cards
        /// will be Five of Spades and Five of Hearts — ideal for testing split.
        /// </summary>
        public void ForceSplitHand()
        {
            PlaceAtIndex(new CardData(Suit.Spades, Rank.Five), _cards.Count - 1);
            PlaceAtIndex(new CardData(Suit.Hearts, Rank.Five), _cards.Count - 3);
        }

        /// <summary>Moves a specific card to the given index within the list.</summary>
        private void PlaceAtIndex(CardData target, int desiredIndex)
        {
            int current = _cards.FindIndex(c => c.Rank == target.Rank && c.Suit == target.Suit);
            if (current < 0) return;

            _cards.RemoveAt(current);
            int clamped = Mathf.Clamp(desiredIndex, 0, _cards.Count);
            _cards.Insert(clamped, target);
        }
    }
}
