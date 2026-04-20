using UnityEngine;

namespace Blackjack
{
    /// <summary>
    /// Immutable value type representing a single playing card.
    /// </summary>
    public readonly struct CardData
    {
        public readonly Suit Suit;
        public readonly Rank Rank;

        public CardData(Suit suit, Rank rank)
        {
            Suit = suit;
            Rank = rank;
        }

        /// <summary>Returns the blackjack point value of this card (Ace = 11, face cards = 10).</summary>
        public int BlackjackValue
        {
            get
            {
                if (Rank == Rank.Ace) return 11;
                if ((int)Rank >= 10) return 10; // Jack, Queen, King
                return (int)Rank;
            }
        }

        /// <summary>Returns the expected sprite asset name, e.g. "ace_of_spades".</summary>
        public string SpriteName => $"{RankName()}_of_{SuitName()}";

        private string RankName()
        {
            return Rank switch
            {
                Rank.Ace   => "ace",
                Rank.Jack  => "jack",
                Rank.Queen => "queen",
                Rank.King  => "king",
                _          => ((int)Rank).ToString()
            };
        }

        private string SuitName() => Suit.ToString().ToLower();

        public override string ToString() => SpriteName;
    }

    public enum Suit { Hearts, Diamonds, Clubs, Spades }

    public enum Rank
    {
        Two   = 2,
        Three = 3,
        Four  = 4,
        Five  = 5,
        Six   = 6,
        Seven = 7,
        Eight = 8,
        Nine  = 9,
        Ten   = 10,
        Jack  = 11,
        Queen = 12,
        King  = 13,
        Ace   = 14
    }
}
