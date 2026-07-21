using AscensionBot;
using AscensionBot.AI;
using AscensionBot.AI.SharedStates;
using AscensionBot.Game;
using AscensionBot.Game.Objects;
using System.Collections.Generic;

namespace MeleeBot
{
    // Combate CLASSLESS: no conoce clases ni hechizos. Machaca los slots de tu barra
    // de acción en orden de prioridad. La base (CombatStateBase) ya orienta, se acerca,
    // activa auto-ataque, detecta la muerte del objetivo y lootea.
    class CombatState : CombatStateBase, IBotState
    {
        // Slots de la barra principal a pulsar (1..12). WoW ignora los que estén en
        // cooldown / sin recurso / fuera de rango, así que es seguro spamearlos.
        // Ordénalos por prioridad. Coloca tus habilidades en esos slots en el juego.
        static readonly int[] ActionSlots = { 1, 2, 3, 4, 5, 6 };

        // 5 = melee. Súbelo a ~28 si tu build es a distancia.
        const int Range = 5;

        readonly LocalPlayer player;

        internal CombatState(
            Stack<IBotState> botStates,
            IDependencyContainer container,
            WoWUnit target,
            bool loot = true)
            : base(botStates, container, target, desiredRange: Range, loot)
        {
            player = ObjectManager.Player;
        }

        public new void Update()
        {
            // La base gestiona movimiento/orientación/auto-ataque/muerte/loot.
            if (base.Update())
                return;

            // No interrumpir un cast en curso.
            if (player.IsCasting || player.IsChanneling)
                return;

            // Throttle: no martillear el servidor con ~120 acciones/seg (inútil por el GCD, y
            // puede provocar desconexión por flood). Disparamos la barra cada ~250 ms.
            if (Wait.For("AscensionActionSpam", 250))
            {
                foreach (var slot in ActionSlots)
                    player.LuaCall($"UseAction({slot})");
            }
        }
    }
}
