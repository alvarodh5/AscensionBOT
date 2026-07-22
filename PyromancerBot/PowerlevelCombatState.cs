using AscensionBot.AI;
using AscensionBot.Game.Objects;
using System.Collections.Generic;

namespace PyromancerBot
{
    // Not used by the Pyromancer profile.
    class PowerlevelCombatState : IBotState
    {
        readonly Stack<IBotState> botStates;

        public PowerlevelCombatState(Stack<IBotState> botStates, IDependencyContainer container, WoWUnit target, WoWPlayer powerlevelTarget)
        {
            this.botStates = botStates;
        }

        public void Update() => botStates.Pop();
    }
}
