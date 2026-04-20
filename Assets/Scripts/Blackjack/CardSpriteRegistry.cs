using System.Collections.Generic;
using UnityEngine;

namespace Blackjack
{
    /// <summary>
    /// Loads all card sprites from Resources and provides O(1) lookup by CardData.
    /// Expects sprites to live in  Resources/Cards/  with names like "ace_of_spades", "card_back".
    /// </summary>
    [CreateAssetMenu(fileName = "CardSpriteRegistry", menuName = "Blackjack/Card Sprite Registry")]
    public class CardSpriteRegistry : ScriptableObject
    {
        [Tooltip("Drag all 52 card sprites here, plus the card-back sprite.")]
        [SerializeField] private Sprite[] cardSprites;
        [SerializeField] private Sprite cardBackSprite;

        private Dictionary<string, Sprite> _lookup;

        private void OnEnable() => BuildLookup();

        private void BuildLookup()
        {
            _lookup = new Dictionary<string, Sprite>();
            if (cardSprites == null || cardSprites.Length == 0) return;
            foreach (Sprite s in cardSprites)
            {
                if (s != null)
                    _lookup[s.name] = s;
            }
        }

        public Sprite GetSprite(CardData card)
        {
            if (_lookup == null) BuildLookup();
            _lookup.TryGetValue(card.SpriteName, out Sprite sprite);
            if (sprite == null)
                Debug.LogWarning($"[CardSpriteRegistry] Sprite not found: {card.SpriteName}");
            return sprite;
        }

        public Sprite GetBackSprite() => cardBackSprite;
    }
}
