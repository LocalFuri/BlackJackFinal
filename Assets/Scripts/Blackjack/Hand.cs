using System.Collections.Generic;

namespace Blackjack
{
    /// <summary>
    /// Represents a blackjack hand and calculates its best score.
    /// </summary>
    public class Hand
    {
        private readonly List<CardData> _cards = new();

        public IReadOnlyList<CardData> Cards => _cards;
        public int Count => _cards.Count;

        public void Clear() => _cards.Clear();

        public void AddCard(CardData card) => _cards.Add(card);

        /// <summary>Removes the card at the given index (used when splitting).</summary>
        public void RemoveAt(int index) => _cards.RemoveAt(index);

        /// <summary>
        /// Returns the best possible hand value (aces counted as 1 when needed to avoid bust).
        /// </summary>
        public int BestValue()
        {
            int total = 0;
            int aces  = 0;

            foreach (CardData card in _cards)
            {
                total += card.BlackjackValue;
                if (card.Rank == Rank.Ace) aces++;
            }

            while (total > 21 && aces > 0)
            {
                total -= 10;
                aces--;
            }

            return total;
        }

        public bool IsBust()       => BestValue() > 21;
        public bool IsBlackjack()  => _cards.Count == 2 && BestValue() == 21;

        /// <summary>
        /// Returns true when the hand is "soft" — at least one Ace is still counted as 11.
        /// </summary>
        public bool IsSoft()
        {
            int total = 0;
            int aces  = 0;

            foreach (CardData card in _cards)
            {
                total += card.BlackjackValue;
                if (card.Rank == Rank.Ace) aces++;
            }

            while (total > 21 && aces > 0)
            {
                total -= 10;
                aces--;
            }

            return aces > 0;
        }
    }
}
