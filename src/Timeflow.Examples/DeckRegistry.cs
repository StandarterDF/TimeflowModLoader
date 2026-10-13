using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace Timeflow.Examples
{
    /// <summary>
    /// Adds and removes cards from the game's *master* card database
    /// (<c>CardsDeck._cardDeck</c>, a <c>Dictionary&lt;CardCategory, HashSet&lt;Card&gt;&gt;</c>).
    ///
    /// Everything the game exposes to players — the business/income choice, the happening pool,
    /// the market — is read from this dictionary, so a card placed here behaves like a real,
    /// officially-shipped card. It is the runtime equivalent of dropping a JSON file into the
    /// database (see the modding guide, chapter 09).
    /// </summary>
    internal static class DeckRegistry
    {
        private static readonly FieldInfo DeckField = AccessTools.Field(typeof(CardsDeck), "_cardDeck");

        /// <summary>Adds the card to its category. Safe to call repeatedly; returns true if it was added now.</summary>
        public static bool Add(Card card)
        {
            var deck = GetCategorySet(card.category);
            if (deck == null)
                return false;
            return deck.Add(card);
        }

        /// <summary>Removes the card from its category.</summary>
        public static bool Remove(Card card)
        {
            var deck = GetCategorySet(card.category);
            if (deck == null)
                return false;
            return deck.Remove(card);
        }

        /// <summary>True if this exact card instance is already registered.</summary>
        public static bool Contains(Card card)
        {
            var deck = GetCategorySet(card.category);
            return deck != null && deck.Contains(card);
        }

        /// <summary>True if the database has been parsed for at least one card of the category.</summary>
        public static bool CategoryLoaded(CardCategory category)
        {
            var deck = GetCategorySet(category);
            return deck != null && deck.Count > 0;
        }

        private static HashSet<Card> GetCategorySet(CardCategory category)
        {
            var instance = CardsDeck.instance;
            if (instance == null || DeckField == null)
                return null;

            var table = DeckField.GetValue(instance) as Dictionary<CardCategory, HashSet<Card>>;
            if (table == null)
                return null;

            if (!table.TryGetValue(category, out var set) || set == null)
            {
                set = new HashSet<Card>();
                table[category] = set;
            }
            return set;
        }
    }
}
