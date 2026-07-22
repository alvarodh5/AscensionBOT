using AscensionBot.AI;
using System.Collections.Generic;

namespace PyromancerBot
{
    // Unused (was mage-specific buffing). Kept as a no-op so the project compiles.
    class BuffSelfState : IBotState
    {
        readonly Stack<IBotState> botStates;

        public BuffSelfState(Stack<IBotState> botStates, IDependencyContainer container)
        {
            this.botStates = botStates;
        }

        public void Update() => botStates.Pop();
    }
}
