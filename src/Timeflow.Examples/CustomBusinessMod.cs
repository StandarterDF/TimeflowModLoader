using System;
using Timeflow.ModLoader.Modding;
using UnityEngine;

namespace Timeflow.Examples
{
    /// <summary>
    /// Difficulty: ★★★ (medium/advanced).
    ///
    /// <b>Adds a brand-new business to the game.</b> The business ("Timeflow Studio", a small
    /// business) is registered in the master card database, so it appears in the normal *Income*
    /// choice next to the game's own businesses and can be bought like any other. Press <b>F6</b>
    /// to skip the shop and grant it directly (handy for testing).
    ///
    /// This is the "add content" recipe: build a <see cref="Card"/> (here by cloning a template so
    /// the icon and flags are valid), register it with <see cref="DeckRegistry"/>, and the rest of
    /// the game — income, bookkeeping, mood, the shop — just works.
    ///
    /// Demonstrates: creating game content at runtime, injecting into <c>CardsDeck._cardDeck</c>,
    /// granting a card via <c>Player.TakeCard</c>, config-driven numbers, runtime enable/disable.
    ///
    /// See <c>docs/modding-guides/09-cards-and-business-internals.md</c>.
    /// </summary>
    [Mod(
        "example.custom-business",
        Name = "Свой бизнес",
        Version = "1.0.0",
        Author = "Timeflow examples",
        Description = "Добавляет в игру собственный малый бизнес; F6 — выдать его игроку.")]
    public sealed class CustomBusinessMod : ModBase
    {
        private const string CardId = "mod:timeflow_studio";
        private const CardCategory Category = CardCategory.BusinessSmall;

        private Card _card;
        private long _cost;
        private long _monthlyIncome;
        private bool _chargeOnGrant;

        public override void OnLoad()
        {
            _cost = Config.GetInt("cost", 45000);
            _monthlyIncome = Config.GetInt("monthlyIncome", 3200);
            _chargeOnGrant = Config.GetBool("chargeOnGrant", false);

            Config.Set("cost", _cost);
            Config.Set("monthlyIncome", _monthlyIncome);
            Config.Set("chargeOnGrant", _chargeOnGrant);
            Config.Save();

            Log.Info($"Custom business loaded (cost={_cost}, income/bucket={_monthlyIncome}). " +
                     "It joins the normal Income choice; press F6 to grant it instantly.");
        }

        public override void OnUpdate()
        {
            var game = GameManager.instance;
            if (game == null || !game.InGame)
                return;

            // Build once the game has parsed the card database (so we can clone a valid template).
            if (_card == null && DeckRegistry.CategoryLoaded(Category))
                BuildCard();

            // Keep it registered (a fresh game can rebuild the database from scratch).
            if (_card != null && !DeckRegistry.Contains(_card) && DeckRegistry.Add(_card))
                Log.Info("Custom business registered in the Income deck.");

            if (Input.GetKeyDown(KeyCode.F6))
                GrantToCurrentPlayer();
        }

        public override void OnUnload()
        {
            if (_card != null)
                DeckRegistry.Remove(_card);
            _card = null;
            Log.Info("Custom business unloaded.");
        }

        // ------------------------------------------------------------------ content

        private void BuildCard()
        {
            _card = CardFactory.Create(Category, CardId);
            CardFactory.SetTexts(
                _card,
                "Timeflow Studio",
                "Малый бизнес",
                "Студия делает моды на заказ и приносит стабильный доход каждый ход.");

            // For non-realty assets the price is stored as a *negative* number (the player pays it),
            // and income is positive.
            _card.price = -_cost;
            _card.income = _monthlyIncome;
            _card.time = 0;             // the business does not consume personal time
            _card.duration = -1;        // permanent
            _card.upgradeLevel = 1;     // already "opened", not an in-process build
            _card.canBeSaled = true;    // can be sold on the marketplace
            _card.reject = true;        // the shop asks for confirmation / needs money
            _card.book = true;          // goes through bookkeeping like a normal business
            _card.SetLogTakedDict(CardFactory.Localized("Вы приобрели бизнес «Timeflow Studio»."));
            _card.give = string.Empty;
            _card.need = string.Empty;

            Log.Info($"Built custom card '{_card.id}' (image={_card.image}).");
        }

        private void GrantToCurrentPlayer()
        {
            var game = GameManager.instance;
            var player = game != null ? game.currentPlayerScript : null;
            if (player == null)
                return;

            if (player.HasCard(CardId))
            {
                Log.Info("Player already owns the custom business.");
                return;
            }

            if (_card == null)
                BuildCard();
            if (_card == null)
                return;

            if (_chargeOnGrant)
                player.moneyOp(-_cost); // negative value = spend money

            // TakeCard deep-copies the template, applies mood/bookkeeping and starts its income.
            player.TakeCard(_card, instantAction: false, book: false);

            SafeLog($"Получен бизнес «{_card.title}» (+{_monthlyIncome} к доходу).");
            Log.Info($"Granted '{CardId}' to {player.title}.");
        }

        private void SafeLog(string message)
        {
            try { LogManager.instance?.Message(message); }
            catch (Exception ex) { Log.Error("Could not post game log message: " + ex.Message); }
        }
    }
}
