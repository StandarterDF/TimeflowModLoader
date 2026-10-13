using System.Collections.Generic;
using UnityEngine;

namespace Timeflow.Examples
{
    /// <summary>
    /// Helpers for building brand-new <see cref="Card"/> objects at runtime.
    ///
    /// The game's own cards are parsed from JSON by <c>CardsDeck.CreateCard</c> (see
    /// <c>docs/modding-guides/09-cards-and-business-internals.md</c>). Here we do the same thing in
    /// code: take an existing card of the target category and <c>DeepCopy()</c> it, which gives us a
    /// valid <c>image</c>, correct default flags and the loan/business fields the engine expects —
    /// then we overwrite everything that makes our card unique.
    ///
    /// If no template is available we fall back to a bare <see cref="Card"/>.
    /// </summary>
    internal static class CardFactory
    {
        /// <summary>
        /// Creates a fresh card of <paramref name="category"/>. Prefer a template card without a
        /// "delayed" (in-process) stage so we do not inherit a half-built business state.
        /// </summary>
        public static Card Create(CardCategory category, string id)
        {
            Card card = CloneTemplate(category) ?? new Card();
            card.category = category;
            card.id = id; // the setter lower-cases it, exactly like the JSON parser
            if (string.IsNullOrEmpty(card.image))
                card.image = category.ToString();

            // Neutralise anything we may have inherited from the template.
            card.action = null;
            card.upgrades = new List<BusinessUpgrade>();
            card.delayed = new List<DelayedEvent>();
            card.momentalEvents = new List<DelayedEvent>();
            card.options = new List<CardOption>();
            card.linkedCards = null;
            card.give = string.Empty;
            card.need = string.Empty;
            card.shield = string.Empty;
            card.remove = string.Empty;
            card.removeActive = string.Empty;
            card.targets = string.Empty;
            card.targetIndustry = string.Empty;
            card.targetRealty = string.Empty;
            card.priceAdjust = 0f;
            card.bonusBusiness = 0f;
            card.bonusBasket = 0f;
            card.bonusMargin = 0f;
            card.bonusSalary = 0f;
            card.chance = 1f;
            card.botRate = 1f;
            card.predictable = true;
            card.forAllPlayers = false;
            card.instant = false;
            card.holy = false;
            card.isNamedCard = false;
            card.name = null;
            card.reuse = false;

            // These two make the *price* getter compute a value from basePrice/economy instead of
            // using the price we set. Reset them so our explicit price wins.
            card.realtyFactor = false;
            card.basePrice = 0;

            // Drop the template's localised strings, otherwise the UI/logs show the taxi business.
            card.SetLostDescDict(new Dictionary<string, string>());
            card.SetShortDescriptionDict(new Dictionary<string, string>());
            card.SetShortNameDict(new Dictionary<string, string>());
            card.SetSubcategoryDict(new Dictionary<string, string>());
            card.SetLogTakedDict(new Dictionary<string, string>());
            card.SetLogRejectDict(new Dictionary<string, string>());
            return card;
        }

        /// <summary>Clones the first "plain" card of a category, or null if the deck is not ready.</summary>
        public static Card CloneTemplate(CardCategory category)
        {
            var deck = CardsDeck.instance;
            if (deck == null)
                return null;

            List<Card> cards;
            try { cards = deck.GetDeck(category); }
            catch { return null; }
            if (cards == null || cards.Count == 0)
                return null;

            Card best = null;
            foreach (var candidate in cards)
            {
                if (candidate == null)
                    continue;
                if (best == null)
                {
                    best = candidate;
                    continue;
                }
                // Prefer a card with no delayed stage (a simple, "ready" business/event).
                if (candidate.delayed.Count == 0 && best.delayed.Count != 0)
                    best = candidate;
            }

            return best == null ? null : best.DeepCopy();
        }

        /// <summary>True once the game has finished parsing its card database.</summary>
        public static bool IsDeckLoaded(CardCategory category)
        {
            var deck = CardsDeck.instance;
            if (deck == null)
                return false;
            try { return deck.GetDeck(category).Count > 0; }
            catch { return false; }
        }

        /// <summary>Sets localized title/subtitle/description for every language the game knows.</summary>
        public static void SetTexts(Card card, string title, string subtitle, string description)
        {
            card.SetTitleDictForAllLangs(title);
            card.SetSubtitleDict(Localized(subtitle));
            card.SetDescriptionDictForAllLangs(description);
        }

        /// <summary>Builds a "same text in all languages" dictionary (used where only a dict setter exists).</summary>
        public static Dictionary<string, string> Localized(string text)
        {
            var dict = new Dictionary<string, string>();
            var languages = Localization.Localizations;
            if (languages == null || languages.Count == 0)
            {
                dict["ru"] = text;
                dict["en"] = text;
                return dict;
            }

            foreach (var language in languages)
                if (!dict.ContainsKey(language))
                    dict[language] = text;
            return dict;
        }
    }
}
