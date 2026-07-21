using AscensionBot.AI;
using AscensionBot.Game;
using AscensionBot.Game.Objects;
using System.Collections.Generic;
using System.Linq;

namespace CasterBot
{
    // Recover after combat (item 7): back up to >=75% HP and >=50% mana. If a drink is
    // configured (botSettings Drink), keep drinking up to 90% mana. If attacked, stop resting
    // and let combat resume (item 5). Uses food/drink from bags if configured; otherwise just
    // waits for natural regen.
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

            if (foodItem != null && !player.IsEating && player.HealthPercent < HpTarget)
                foodItem.Use();

            if (drinkItem != null && !player.IsDrinking && player.ManaPercent < manaTarget)
                drinkItem.Use();
        }
    }
}
