using AscensionBot;
using AscensionBot.AI;
using AscensionBot.AI.SharedStates;
using AscensionBot.Game;
using AscensionBot.Game.Objects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PyromancerBot
{
    // Pyromancer combat (self-contained, no class logic):
    //  - Approaches to 25 yds, doesn't go to melee (improvement 3).
    //  - Attacks with a RANDOM ability from slots 1/2/3.
    //  - Heals on slot 4 when below 30% HP.
    //  - Keeps "Seal of Al'ar" and "Ashen Skin" up at all times (improvement 2).
    //  - While closing on a target, switches to a nearer mob that's actually attacking us
    //    instead of chasing a patroller and ignoring the thing hitting us.
    //  - On kill: keeps fighting anything still attacking us (defends properly), otherwise
    //    loots (only if enabled in settings) and then rests before the next pull. RestState is
    //    what tops HP back up to 75% on slot 4 before we hunt again (improvement 1).
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
        readonly StuckHelper stuckHelper;
        readonly CombatWatchdog watchdog = new CombatWatchdog();

        internal CombatState(Stack<IBotState> botStates, IDependencyContainer container, WoWUnit target, bool loot = false)
        {
            this.botStates = botStates;
            this.container = container;
            this.target = target;
            this.loot = loot;
            player = ObjectManager.Player;
            stuckHelper = new StuckHelper(botStates, container);
            SessionStats.OnCombatStart();
        }

        public void Update()
        {
            // Don't run any game/Lua logic unless we're actually in the world. Calling into the
            // client (LuaCall/UseAction) during a loading screen / zoning / disconnect throws a
            // native AccessViolationException that isn't catchable and crashes the bot.
            if (!ObjectManager.IsLoggedIn)
                return;

            // Combat over? Track WHY: a mob tapped/killed by another player is "stolen", not ours.
            bool stolen = false;
            bool dead;
            try
            {
                stolen = target != null && target.TappedByOther;
                dead = target == null || target.Health == 0 || stolen;
            }
            catch { dead = true; }
            if (dead)
            {
                // Count only REAL kills. A mob someone else tapped goes to "abandoned" instead —
                // otherwise, in a busy zone, the Telegram kill counter (and kills/h) inflate wildly.
                if (stolen)
                    SessionStats.OnTargetAbandoned();
                else
                    SessionStats.OnKill();
                player.StopAllMovement();

                // Small settle delay so the corpse/threat state is up to date.
                if (!Wait.For("PyromancerPopCombat", 400))
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

                // Rest after the fight (improvement 1). RestState self-heals on slot 4 back up to
                // 75% HP before we go looking for the next enemy. Loot first if enabled (off by
                // default; see CasterLootEnabled in settings). LootState runs before RestState.
                botStates.Push(new RestState(botStates, container));
                if (loot && !stolen)
                    botStates.Push(new LootState(botStates, container, target));
                return;
            }

            // Never interrupt our own cast — not even to chase a target that walked out of range.
            // (This MUST be checked before the movement block below: otherwise chasing a moving
            // patroller cancels every cast, so we run after it forever without landing a spell.)
            if (player.IsCasting || player.IsChanneling)
                return;

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
                // While still closing on our target, if a DIFFERENT mob is actually attacking us
                // and is closer than the target we're chasing, switch to it. Otherwise we'd keep
                // running after a patroller while something else beats on us unanswered.
                var attacker = ObjectManager.Units.FirstOrDefault(u =>
                    u != null && u.Guid != target.Guid && u.Health > 0 && !u.TappedByOther &&
                    (u.TargetGuid == player.Guid || u.TargetGuid == ObjectManager.Pet?.Guid) &&
                    (!container.Probe?.BlacklistedMobIds?.Contains(u.Guid) ?? true) &&
                    u.Position.DistanceTo(player.Position) < distance);
                if (attacker != null)
                {
                    player.StopAllMovement();
                    botStates.Pop();
                    botStates.Push(new CombatState(botStates, container, attacker, loot));
                    return;
                }

                // Anti-stuck: if we're not making progress toward the target (wall/rock/ledge),
                // push a StuckState that jumps + strafes to break free instead of walking in place.
                if (stuckHelper.CheckIfStuck())
                    return;

                // Not attacking yet -> don't let travel time count toward the give-up timer.
                watchdog.Reset();
                player.MoveToward(target.Position);
                return;
            }
            if (player.IsMoving)
                player.StopAllMovement();

            if (!player.IsFacing(target.Position))
                player.Face(target.Position);

            // In range but can't damage it for a while (no line of sight, behind a wall, on a
            // ledge...)? Give up: blacklist it and pop back so we pick a different target instead
            // of casting into a wall forever. Same idea as the melee CombatStateBase.
            if (watchdog.ShouldGiveUp(target))
            {
                container.Probe?.BlacklistedMobIds?.Add(target.Guid);
                if (container.BotSettings.PermanentlyBlacklistUnreachableTargets)
                    Repository.AddBlacklistedMob(target.Guid);
                player.StopAllMovement();
                botStates.Pop();
                return;
            }

            // Self-heal below 30% (slot 4).
            if (player.HealthPercent < HealBelowPct)
            {
                if (Wait.For("PyromancerHeal", 300))
                    // Target ourselves to heal, then restore the enemy target so we keep attacking.
                    player.LuaCall($"TargetUnit('player'); UseAction({HealSlot}); TargetLastTarget();");
                return;
            }

            // Keep "Seal of Al'ar" and "Ashen Skin" up (improvement 2). Lower priority than
            // healing, higher than attacking so a dropped buff is refreshed promptly.
            if (PyromancerBuff.Maintain(player))
                return;

            // Random attack from slots 1/2/3, throttled so we don't flood the server.
            if (Wait.For("PyromancerAttack", 300))
            {
                var slot = AttackSlots[rng.Next(AttackSlots.Length)];
                player.LuaCall($"UseAction({slot})");
            }
        }
    }
}
