using AscensionBot;
using AscensionBot.AI;
using AscensionBot.Game;
using AscensionBot.Game.Objects;
using System;
using System.Collections.Generic;

namespace CasterBot
{
    // Caster combat (self-contained, no loot/skin):
    //  - Approaches to 25 yds (needs line of sight), doesn't go to melee.
    //  - Attacks with a RANDOM ability from slots 1/2/3.
    //  - Heals on slot 4 when below 30% HP.
    //  - On kill: no loot/skin — pushes RestState so we recover before the next pull.
    class CombatState : IBotState
    {
        static readonly Random rng = new Random();
        static readonly int[] AttackSlots = { 1, 2, 3 };
        const int HealSlot = 4;
        const int Range = 25;
        const int HealBelowPct = 30;

        readonly Stack<IBotState> botStates;
        readonly IDependencyContainer container;
        readonly WoWUnit target;
        readonly LocalPlayer player;

        internal CombatState(Stack<IBotState> botStates, IDependencyContainer container, WoWUnit target, bool loot = true)
        {
            this.botStates = botStates;
            this.container = container;
            this.target = target;
            player = ObjectManager.Player;
            SessionStats.OnCombatStart();
        }

        public void Update()
        {
            // Combat over? -> rest before looking for the next target (item 6).
            bool dead;
            try { dead = target == null || target.Health == 0 || target.TappedByOther; }
            catch { dead = true; }
            if (dead)
            {
                SessionStats.OnKill();
                player.StopAllMovement();
                botStates.Pop();
                botStates.Push(new RestState(botStates, container));
                return;
            }

            if (player.TargetGuid != target.Guid)
                player.SetTarget(target.Guid);

            var distance = player.Position.DistanceTo(target.Position);

            // Approach to 25 yds with line of sight (item 1). Straight line (no mmaps).
            if (distance > Range || !player.InLosWith(target.Position))
            {
                player.MoveToward(target.Position);
                return;
            }
            if (player.IsMoving)
                player.StopAllMovement();

            if (!player.IsFacing(target.Position))
                player.Face(target.Position);

            // Never interrupt our own cast.
            if (player.IsCasting || player.IsChanneling)
                return;

            // Self-heal below 30% (item 3).
            if (player.HealthPercent < HealBelowPct)
            {
                player.LuaCall($"UseAction({HealSlot})");
                return;
            }

            // Random attack from slots 1/2/3 (item 2), throttled so we don't flood the server.
            if (Wait.For("CasterAttack", 300))
            {
                var slot = AttackSlots[rng.Next(AttackSlots.Length)];
                player.LuaCall($"UseAction({slot})");
            }
        }
    }
}
