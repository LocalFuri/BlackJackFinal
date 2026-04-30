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
        /// Manipulates the top of the deck so both the player and dealer
        /// receive a natural blackjack. Places cards top-down so each
        /// subsequent insert pushes previously placed cards back into position.
        /// Deal order: [Count-1] player1, [Count-2] dealer1, [Count-3] player2, [Count-4] dealer2.
        /// </summary>
        public void ForceBothBlackjack()
        {
            PlaceAtIndex(new CardData(Suit.Spades,   Rank.Ace),   _cards.Count - 1); // player card 1
            PlaceAtIndex(new CardData(Suit.Diamonds, Rank.Ace),   _cards.Count - 2); // dealer card 1
            PlaceAtIndex(new CardData(Suit.Hearts,   Rank.King),  _cards.Count - 3); // player card 2
            PlaceAtIndex(new CardData(Suit.Clubs,    Rank.Queen), _cards.Count - 4); // dealer card 2
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

        /// <summary>
        /// Manipulates the top of the deck so the player's first two cards
        /// will have a hard total of exactly 11 (no Aces, random suit/rank pair).
        /// Valid non-Ace pairs that sum to 11: (2,9) (3,8) (4,7) (5,6).
        /// A random pair is chosen each call.
        /// </summary>
        public void ForceDoubleDownTest()
        {
            // All non-Ace rank pairs whose blackjack values sum to 11.
            var pairs = new (Rank a, Rank b)[]
            {
                (Rank.Two,   Rank.Nine),
                (Rank.Three, Rank.Eight),
                (Rank.Four,  Rank.Seven),
                (Rank.Five,  Rank.Six),
            };

            int index = Random.Range(0, pairs.Length);
            (Rank rankA, Rank rankB) = pairs[index];

            // Choose random suits for variety.
            Suit suitA = (Suit)Random.Range(0, 4);
            Suit suitB = (Suit)Random.Range(0, 4);

            PlaceAtIndex(new CardData(suitA, rankA), _cards.Count - 1); // player card 1
            PlaceAtIndex(new CardData(suitB, rankB), _cards.Count - 3); // player card 2
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
