using AscensionBot;
using AscensionBot.AI;
using AscensionBot.Game;
using AscensionBot.Game.Objects;
using System.Collections.Generic;
using System.Linq;

namespace CasterBot
{
    // Recover after combat (item 7): back up to >=75% HP and >=50% mana. Instead of just
    // waiting for HP to regenerate, we ACTIVELY self-heal on slot 4 (the client refuses the
    // cast if we can't afford it, so it naturally stops when mana runs low). If a drink is
    // configured we top mana up to 90%. If attacked, stop resting and let combat resume.
    class RestState : IBotState
    {
        const int HpTarget = 75;
        const int ManaTargetMin = 50;
        const int ManaTargetWithDrink = 90;
        const int HealSlot = 4;

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
            if (player.IsChanneling)
                return;

            // Being attacked -> stop resting, go fight.
            if (ObjectManager.Player.IsInCombat || ObjectManager.Units.Any(u => u.TargetGuid == ObjectManager.Player.Guid))
            {
                player.Stand();
                botStates.Pop();
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
                    // No food: self-heal with slot 4 instead of waiting for regen. "Si el maná
                    // se lo permite" is enforced by the client — UseAction is ignored when we
                    // can't afford the heal (or it's on cooldown), so healing simply stops when
                    // mana runs low and we let it regen (or drink) for the next pull.
                    if (Wait.For("CasterRestHeal", 500))
                        player.LuaCall($"UseAction({HealSlot})");
                }
            }

            // Recover mana with a drink only once HP is handled, so we don't interrupt heals.
            if (drinkItem != null && !player.IsDrinking && hpOk && player.ManaPercent < manaTarget)
                drinkItem.Use();
        }
    }
}
