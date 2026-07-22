using AscensionBot;
using AscensionBot.AI;
using AscensionBot.Game;
using AscensionBot.Game.Objects;
using System.Collections.Generic;
using System.Linq;

namespace PyromancerBot
{
    // Recover after combat (improvement 1): back up to >=75% HP and >=50% mana before hunting the
    // next enemy. Instead of just waiting for HP to regenerate, we ACTIVELY self-heal on slot 4
    // (the client refuses the cast if we can't afford it, so it naturally stops when mana runs
    // low). If a drink is configured we top mana up to 90%. If attacked, stop resting and let
    // combat resume. We also keep "Seal of Al'ar" and "Ashen Skin" up while resting (improvement 2).
    class RestState : IBotState
    {
        const int HpTarget = 75;
        const int ManaTargetMin = 50;
        const int ManaTargetWithDrink = 90;
        const int HealSlot = 4;

        readonly Stack<IBotState> botStates;
        readonly IDependencyContainer container;
        readonly LocalPlayer player;

        // Only ONE post-combat heal per rest: cast it once if we're below 75%, then stop (a single
        // heal usually overshoots and a second just wastes mana). Resets each fight (new RestState).
        bool healCast;

        public RestState(Stack<IBotState> botStates, IDependencyContainer container)
        {
            this.botStates = botStates;
            this.container = container;
            player = ObjectManager.Player;
        }

        public void Update()
        {
            // Don't run any game/Lua logic unless we're actually in the world. Calling into the
            // client (LuaCall/UseAction/food) during a loading screen / zoning / disconnect throws
            // a native AccessViolationException that isn't catchable and crashes the bot.
            if (!ObjectManager.IsLoggedIn)
                return;

            if (player.IsChanneling)
                return;

            // Being attacked -> stop resting and fight the attacker.
            //
            // We require an ACTUAL live aggressor (a unit targeting us), NOT the bare IsInCombat
            // flag: that flag lingers for a few seconds after a kill, so RestState used to bail
            // out the instant it started while still low on HP.
            //
            // Crucially, we engage the attacker DIRECTLY here instead of just popping to
            // GrindState. While we're hurt, GrindState's health gate (>=75% HP / >=50% mana)
            // rejects every voluntary target, and if FindThreat misses the attacker for even one
            // tick (a momentary leash/evade) GrindState roams us all the way back to the anchor at
            // low HP while the mob keeps hitting us. Going straight into combat avoids that; the
            // follow-up RestState after the kill then heals us in place.
            var me = ObjectManager.Player;
            var attacker = ObjectManager.Units.FirstOrDefault(u =>
                u.Health > 0 && !u.TappedByOther &&
                (u.TargetGuid == me.Guid || u.TargetGuid == ObjectManager.Pet?.Guid) &&
                (!container.Probe?.BlacklistedMobIds?.Contains(u.Guid) ?? true));
            if (attacker != null)
            {
                player.Stand();
                botStates.Pop();
                botStates.Push(container.CreateMoveToTargetState(botStates, container, attacker));
                return;
            }

            // Keep "Seal of Al'ar" and "Ashen Skin" up while we're safe between pulls (improvement 2).
            if (PyromancerBuff.Maintain(player))
                return;

            var items = Inventory.GetAllItems();
            var foodName = container.BotSettings.Food;
            var drinkName = container.BotSettings.Drink;
            var foodItem = string.IsNullOrWhiteSpace(foodName) ? null : items.FirstOrDefault(i => i.Info?.Name == foodName);
            var drinkItem = string.IsNullOrWhiteSpace(drinkName) ? null : items.FirstOrDefault(i => i.Info?.Name == drinkName);

            var manaTarget = drinkItem != null ? ManaTargetWithDrink : ManaTargetMin;

            var hpOk = player.HealthPercent >= HpTarget;
            var manaOk = player.MaxMana <= 0 || player.ManaPercent >= manaTarget;

            // With no food configured we only cast ONE heal per rest, so HP counts as "done" once
            // that single heal has gone out — we don't keep topping up to 75% and burning mana.
            var hpDone = hpOk || (foodItem == null && healCast);

            if (hpDone && manaOk)
            {
                player.Stand();
                botStates.Pop();
                return;
            }

            // Restore HP.
            if (!hpOk)
            {
                if (foodItem != null)
                {
                    // A configured food heals for free (no mana) — prefer it, eat until full.
                    if (!player.IsEating)
                        foodItem.Use();
                }
                else if (!healCast && player.Mana > 0 && !player.IsCasting && !player.IsChanneling)
                {
                    // No food: cast slot 4 exactly ONCE. Select ourselves first so the heal has a
                    // valid friendly target (no enemy around while resting), then press slot 4.
                    if (Wait.For("PyromancerRestHeal", 500))
                    {
                        player.LuaCall($"TargetUnit('player'); UseAction({HealSlot});");
                        healCast = true;
                    }
                }
            }

            // Recover mana with a drink only once HP is handled, so we don't interrupt heals.
            if (drinkItem != null && !player.IsDrinking && hpOk && player.ManaPercent < manaTarget)
                drinkItem.Use();
        }
    }
}
