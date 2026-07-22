using AscensionBot.AI;
using System.Collections.Generic;

namespace PyromancerBot
{
    // Unused (was mage-specific conjuring). Kept as a no-op so the project compiles.
    class ConjureItemsState : IBotState
    {
        readonly Stack<IBotState> botStates;

        public ConjureItemsState(Stack<IBotState> botStates, IDependencyContainer container)
        {
            this.botStates = botStates;
        }

        public void Update() => botStates.Pop();
    }
}
