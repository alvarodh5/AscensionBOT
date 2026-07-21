using AscensionBot.AI;
using AscensionBot.Game;
using AscensionBot.Game.Objects;
using System.Collections.Generic;
using System.Linq;

namespace MeleeBot
{
    // Descanso CLASSLESS. Para minimizar el tiempo parado entre bichos:
    //  - SOLO se sienta a descansar si la vida baja de RestBelowThreshold.
    //  - Una vez descansando, sube hasta RestUntilThreshold (no hasta 100).
    //  - Si hay comida configurada (botSettings Food), come y recupera rápido.
    //  - Si tu build no usa maná, la parte de bebida se ignora sola.
    class RestState : IBotState
    {
        // Ajusta estos dos para el ritmo que quieras:
        const int RestBelowThreshold = 50;  // solo descansa si HP% < esto
        const int RestUntilThreshold = 75;  // y entonces hasta HP% >= esto
        const int ManaThreshold = 90;

        readonly Stack<IBotState> botStates;
        readonly IDependencyContainer container;
        readonly LocalPlayer player;

        bool resting;

        WoWItem foodItem;
        WoWItem drinkItem;

        public RestState(Stack<IBotState> botStates, IDependencyContainer container)
        {
            this.botStates = botStates;
            this.container = container;
            player = ObjectManager.Player;
        }

        public void Update()
        {
            var foodName = container.BotSettings.Food;
            var drinkName = container.BotSettings.Drink;

            var items = Inventory.GetAllItems();
            foodItem = string.IsNullOrWhiteSpace(foodName)
                ? null
                : items.FirstOrDefault(i => i.Info?.Name == foodName);
            drinkItem = string.IsNullOrWhiteSpace(drinkName)
                ? null
                : items.FirstOrDefault(i => i.Info?.Name == drinkName);

            if (player.IsChanneling)
                return;

            // Si algo nos ataca, dejar de descansar y volver a pelear.
            if (InCombat)
            {
                player.Stand();
                botStates.Pop();
                return;
            }

            // Decidir si merece la pena descansar. Con la vida por encima del umbral bajo
            // (y sin haber empezado ya), salimos de inmediato -> a por el siguiente bicho.
            if (!resting && player.HealthPercent >= RestBelowThreshold && ManaOk)
            {
                botStates.Pop();
                return;
            }

            resting = true;

            // Recuperado hasta el objetivo -> seguir.
            if (player.HealthPercent >= RestUntilThreshold && ManaOk)
            {
                player.Stand();
                botStates.Pop();
                return;
            }

            if (foodItem != null && !player.IsEating && player.HealthPercent < RestUntilThreshold)
                foodItem.Use();

            if (drinkItem != null && !player.IsDrinking && player.ManaPercent < ManaThreshold)
                drinkItem.Use();
        }

        bool ManaOk => drinkItem == null || player.ManaPercent >= ManaThreshold;

        bool InCombat => ObjectManager.Player.IsInCombat ||
            ObjectManager.Units.Any(u => u.TargetGuid == ObjectManager.Player.Guid);
    }
}
