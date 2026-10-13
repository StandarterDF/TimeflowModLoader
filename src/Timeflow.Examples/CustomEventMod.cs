using System;
using System.Collections.Generic;
using HarmonyLib;
using Timeflow.ModLoader.Modding;
using UnityEngine;

namespace Timeflow.Examples
{
    /// <summary>
    /// Difficulty: ★★★ (medium/advanced).
    ///
    /// <b>Adds a brand-new "happening" event.</b> When the player lands on a Happening cell the game
    /// builds <c>Player.cardPerTurn</c> and <c>EventHappening.ActivateEvent</c> randomly shows one of
    /// those cards. We hook that method with a Harmony <i>Prefix</i> and slip our own card
    /// ("Крипто-аирдроп") into the pool — with a configurable probability.
    ///
    /// Why a Prefix and not a Postfix? <c>ActivateEvent</c> is <c>async UniTask</c>: its body lives in
    /// a compiler-generated state machine. Harmony can only reliably wrap the *stub* that starts that
    /// machine. A Prefix runs before the state machine starts and the stub reads
    /// <c>player.cardPerTurn</c> synchronously at its very beginning, so our insertion is picked up.
    ///
    /// The reward is delivered through the card's optional <c>ActionCard</c> delegate
    /// (<c>Card.action</c>), which the game invokes inside <c>Player.TakeCard()</c> when the player
    /// accepts. That is the cleanest way to script a one-off effect; we also set <c>give</c> to drop
    /// a marker variable into the player's state.
    ///
    /// (Heads-up on money signs: <c>Player.moneyOp(value)</c> <i>adds</i> value, and a card with a
    /// negative <c>price</c> is therefore a *cost* — the shop wire does <c>moneyOp(card.price)</c>. A
    /// positive price would be a payout.)
    ///
    /// Press <b>F7</b> to show the event immediately (it is also offered naturally, with a
    /// configurable chance, whenever a Happening cell triggers).
    ///
    /// Demonstrates: patching an async game method with a Prefix, building a happening card, a card
    /// `ActionCard` delegate, config-driven probability.
    ///
    /// See <c>docs/modding-guides/04-events-and-cards.md</c> and chapter 09.
    /// </summary>
    [Mod(
        "example.custom-event",
        Name = "Своё событие",
        Version = "1.0.0",
        Author = "Timeflow examples",
        Description = "Добавляет кастомное событие-происшествие (крипто-аирдроп); F7 — показать его сейчас.")]
    public sealed class CustomEventMod : ModBase
    {
        private const string CardId = "mod:crypto_airdrop";

        private static CustomEventMod _current;

        private Card _card;
        private float _chance;
        private long _reward;
        private bool _forceNext;

        public override void OnLoad()
        {
            _current = this;
            _chance = Mathf.Clamp01(Config.GetFloat("chance", 0.25f));
            _reward = Config.GetInt("reward", 12000);
            Config.Set("chance", _chance);
            Config.Set("reward", _reward);
            Config.Save();

            // Manual patch on the async game method (see the class summary for why a Prefix works).
            var target = AccessTools.Method(typeof(EventHappening), nameof(EventHappening.ActivateEvent));
            Harmony.Patch(target, prefix: new HarmonyMethod(AccessTools.Method(typeof(CustomEventMod), nameof(BeforeHappening))));

            Log.Info($"Custom event loaded (chance={_chance:P0}, reward={_reward}). F7 forces the next one.");
        }

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F7))
                TriggerNow();
        }

        /// <summary>
        /// Shows the event immediately, without waiting for a real Happening cell. Handy for testing.
        /// <para>
        /// <c>ActivateEvent</c> returns <c>async UniTask</c>, so we call it through reflection — this
        /// avoids referencing the UniTask assembly from an example mod, and the returned task can
        /// simply be discarded.
        /// </para>
        /// </summary>
        private void TriggerNow()
        {
            var game = GameManager.instance;
            var player = game != null ? game.currentPlayerScript : null;
            var happening = UnityEngine.Object.FindObjectOfType<EventHappening>();
            if (player == null || happening == null)
            {
                Log.Warn("Cannot show the event yet (no active game or no board).");
                return;
            }

            _forceNext = true;
            try
            {
                AccessTools.Method(typeof(EventHappening), nameof(EventHappening.ActivateEvent))
                    .Invoke(happening, new object[] { player });
                Log.Info("Custom event triggered on demand.");
            }
            catch (Exception ex)
            {
                _forceNext = false;
                Log.Error("Could not trigger the event: " + ex.Message);
            }
        }

        public override void OnUnload()
        {
            _forceNext = false;
            _current = null;
            // The loader removes our Harmony patch via UnpatchSelf().
            Log.Info("Custom event unloaded.");
        }

        // ------------------------------------------------------------------ patch

        // Harmony Prefix: called right before EventHappening.ActivateEvent starts its state machine.
        private static void BeforeHappening(Player player)
        {
            var self = _current;
            if (self == null || player == null)
                return;

            var card = self.EnsureCard();
            if (card == null)
                return;

            if (player.cardPerTurn == null)
                player.cardPerTurn = new List<Card>();

            if (self._forceNext)
            {
                self._forceNext = false;
                player.cardPerTurn.Clear();
                player.cardPerTurn.Add(card);
                self.Log.Info("Forcing custom event for " + player.title + ".");
                return;
            }

            if (UnityEngine.Random.value < self._chance)
            {
                player.cardPerTurn.Add(card);
                self.Log.Info("Custom event offered to " + player.title + ".");
            }
        }

        // ------------------------------------------------------------------ content

        private Card EnsureCard()
        {
            if (_card != null)
                return _card;

            // Wait until the game has parsed the Happening pool so we can clone a valid template.
            if (!CardFactory.IsDeckLoaded(CardCategory.Happening))
                return null;

            _card = CardFactory.Create(CardCategory.Happening, CardId);
            CardFactory.SetTexts(
                _card,
                "Крипто-аирдроп",
                "Неожиданная удача",
                "Неизвестный спонсор подарил вам немного криптовалюты. Деньги уже на счету!");

            _card.price = 0;           // no price row in the UI; the reward is granted by the action below
            _card.income = 0;
            _card.time = 0;
            _card.duration = 0;       // never stored as a permanent card
            _card.canBeSaled = false;
            _card.reject = false;
            _card.book = false;
            _card.give = "airdrop=1";
            _card.need = string.Empty;
            // The card's optional ActionCard delegate runs in Player.TakeCard() when the player accepts.
            _card.action = AwardReward;

            Log.Info($"Built custom happening '{_card.id}' (reward={_reward}).");
            return _card;
        }

        // Runs inside Player.TakeCard() when the player accepts the event.
        private static void AwardReward()
        {
            var self = _current;
            var player = GameManager.instance != null ? GameManager.instance.currentPlayerScript : null;
            if (self == null || player == null)
                return;
            player.moneyOp(self._reward); // moneyOp ADDS the value
            self.Log.Info("Awarded the airdrop reward to " + player.title + ".");
        }
    }
}
