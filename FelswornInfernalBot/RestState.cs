using AscensionBot;
using AscensionBot.AI;
using AscensionBot.Game;
using AscensionBot.Game.Objects;
using System.Collections.Generic;
using System.Linq;

namespace FelswornInfernalBot
{
    // Recover after combat: back up to >=75% HP and >=50% mana before hunting the next enemy.
    // Instead of just waiting for HP to regenerate, we ACTIVELY cast the shield/heal on slot 3
    // (the client refuses the cast if we can't afford it, so it naturally stops when mana runs
    // low). If a drink is configured we top mana up to 90%. If attacked, we engage the attacker
    // directly.
    class RestState : IBotState
    {
        const int HpTarget = 75;
        const int ManaTargetMin = 50;
        const int ManaTargetWithDrink = 90;

        readonly Stack<IBotState> botStates;
        readonly IDependencyContainer container;
        readonly LocalPlayer player;

        public RestState(Stack<IBotState> botStates, IDependencyContainer container)
        {
            this.botStates = botStates;
            this.container = container;
            player = ObjectManager.Player;
        }

        public void Update()
        {
            // Don't run any game/Lua logic unless we're actually in the world (loading/zoning
            // would otherwise crash on the native Lua call).
            if (!ObjectManager.IsLoggedIn)
                return;

            if (player.IsChanneling)
                return;

            // Interrupted by a real attacker -> engage IT directly instead of popping to
            // GrindState (whose health gate would reject targets while we're hurt and could roam
            // us back to the anchor). The follow-up RestState after the kill heals us in place.
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

            var items = Inventory.GetAllItems();
            var foodName = container.BotSettings.Food;
            var drinkName = container.BotSettings.Drink;
            var foodItem = string.IsNullOrWhiteSpace(foodName) ? null : items.FirstOrDefault(i => i.Info?.Name == foodName);
            var drinkItem = string.IsNullOrWhiteSpace(drinkName) ? null : items.FirstOrDefault(i => i.Info?.Name == drinkName);

            var manaTarget = drinkItem != null ? ManaTargetWithDrink : ManaTargetMin;

            var hpOk = player.HealthPercent >= HpTarget;
            var manaOk = player.MaxMana <= 0 || player.ManaPercent >= manaTarget;

            if (hpOk && manaOk)
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
                    // A configured food heals for free (no mana) — prefer it.
                    if (!player.IsEating)
                        foodItem.Use();
                }
                else if (player.Mana > 0 && !player.IsCasting && !player.IsChanneling)
                {
                    // No food: cast "Hateforged Barrier" (shield + self-heal) to top up instead of
                    // just waiting for regen. It's on a ~20s cooldown; the client ignores the cast
                    // while it's still on cooldown, so trying every ~2s is harmless and we regen
                    // normally in between. (We don't gate on IsSpellReady — the cooldown read is
                    // unreliable on Ascension.)
                    if (Wait.For("FelswornRestHeal", 2000))
                        player.LuaCall($"CastSpellByName(\"{CombatState.Barrier}\")");
                }
            }

            // Recover mana with a drink only once HP is handled, so we don't interrupt heals.
            if (drinkItem != null && !player.IsDrinking && hpOk && player.ManaPercent < manaTarget)
                drinkItem.Use();
        }
    }
}
