using AscensionBot;
using AscensionBot.AI;
using AscensionBot.AI.SharedStates;
using AscensionBot.Game;
using AscensionBot.Game.Objects;
using System.Collections.Generic;

namespace MeleeBot
{
    // Acercarse al objetivo y pasar a combate. Sin hechizo de "pull" (no sabemos qué
    // hechizos tienes): caminamos hasta el rango y dejamos que CombatState haga el resto.
    class MoveToTargetState : MoveToTargetStateBase, IBotState
    {
        const string waitKey = "AscensionPull";

        // Debe coincidir (o ser >=) con el Range de CombatState.
        const int Range = 5;

        readonly Stack<IBotState> botStates;
        readonly IDependencyContainer container;
        readonly WoWUnit target;
        readonly LocalPlayer player;
        readonly StuckHelper stuckHelper;

        internal MoveToTargetState(Stack<IBotState> botStates, IDependencyContainer container, WoWUnit target)
            : base(botStates, container, target)
        {
            this.botStates = botStates;
            this.container = container;
            this.target = target;
            player = ObjectManager.Player;
            stuckHelper = new StuckHelper(botStates, container);
        }

        public new void Update()
        {
            if (player.IsCasting)
                return;

            if (base.Update())
                return;

            stuckHelper.CheckIfStuck();

            var distanceToTarget = player.Position.DistanceTo(target.Position);
            if (distanceToTarget <= Range && player.InLosWith(target.Position))
            {
                if (player.IsMoving)
                    player.StopAllMovement();

                if (Wait.For(waitKey, 100))
                {
                    player.StopAllMovement();
                    Wait.Remove(waitKey);

                    botStates.Pop();
                    botStates.Push(new CombatState(botStates, container, target));
                    return;
                }
            }
            else
            {
                // Movimiento en LÍNEA RECTA (sin navmesh/mmaps): mirar y andar hacia el objetivo.
                // Suficiente para farmear a corta distancia; evita el pathfinding nativo (que
                // peta con SEHException al no haber mmaps generados para Ascension).
                player.MoveToward(target.Position);
            }
        }
    }
}
