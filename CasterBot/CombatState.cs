using AscensionBot;
using AscensionBot.AI;
using AscensionBot.AI.SharedStates;
using AscensionBot.Game;
using AscensionBot.Game.Objects;
using System;
using System.Collections.Generic;

namespace CasterBot
{
    // Caster combat (self-contained, no class logic):
    //  - Approaches to 25 yds, doesn't go to melee.
    //  - Attacks with a RANDOM ability from slots 1/2/3.
    //  - Heals on slot 4 when below 30% HP.
    //  - On kill: keeps fighting anything still attacking us (defends properly), otherwise
    //    loots (only if enabled in settings) and then rests before the next pull.
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
        readonly bool loot;

        internal CombatState(Stack<IBotState> botStates, IDependencyContainer container, WoWUnit target, bool loot = false)
        {
            this.botStates = botStates;
            this.container = container;
            this.target = target;
            this.loot = loot;
            player = ObjectManager.Player;
            SessionStats.OnCombatStart();
        }

        public void Update()
        {
            // Combat over?
            bool dead;
            try { dead = target == null || target.Health == 0 || target.TappedByOther; }
            catch { dead = true; }
            if (dead)
            {
                SessionStats.OnKill();
                player.StopAllMovement();

                // Small settle delay so the corpse/threat state is up to date.
                if (!Wait.For("CasterPopCombat", 400))
                    return;

                botStates.Pop();

                // If something else is still on us, keep fighting it instead of walking off to
                // rest (this is what makes us actually defend when we get jumped mid-fight).
                var threat = container.FindThreat();
                if (threat != null && threat.Health > 0 && !threat.TappedByOther)
                {
                    botStates.Push(new CombatState(botStates, container, threat, loot));
                    return;
                }

                // Rest after the fight (item 7). Loot first if enabled (off by default; see
                // CasterLootEnabled in settings). LootState runs before RestState.
                botStates.Push(new RestState(botStates, container));
                if (loot)
                    botStates.Push(new LootState(botStates, container, target));
                return;
            }

            if (player.TargetGuid != target.Guid)
                player.SetTarget(target.Guid);

            var distance = player.Position.DistanceTo(target.Position);

            // Approach to 25 yds. Straight line (no mmaps).
            //
            // NOTE: we deliberately do NOT gate on player.InLosWith() here. The native LoS
            // raycast (Functions.Intersect) is unreliable on Ascension and would report "no LoS"
            // even with a clear shot, leaving the caster walking in place forever — never
            // attacking, never healing, and looking like it wanders off. The melee profile has
            // the same policy (CombatStateBase relies on the client's LoS error message instead).
            if (distance > Range)
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

            // Self-heal below 30% (item 3), slot 4.
            if (player.HealthPercent < HealBelowPct)
            {
                if (Wait.For("CasterHeal", 300))
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
