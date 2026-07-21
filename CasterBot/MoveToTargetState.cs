using AscensionBot.AI;
using AscensionBot.Game.Objects;
using System.Collections.Generic;

namespace CasterBot
{
    // The caster approaches inside CombatState (it walks to 25 yds before attacking), so this
    // just hands off to CombatState immediately.
    class MoveToTargetState : IBotState
    {
        readonly Stack<IBotState> botStates;
        readonly IDependencyContainer container;
        readonly WoWUnit target;

        internal MoveToTargetState(Stack<IBotState> botStates, IDependencyContainer container, WoWUnit target)
        {
            this.botStates = botStates;
            this.container = container;
            this.target = target;
        }

        public void Update()
        {
            botStates.Pop();
            botStates.Push(new CombatState(botStates, container, target));
        }
    }
}
