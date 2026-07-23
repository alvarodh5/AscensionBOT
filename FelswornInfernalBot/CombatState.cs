using AscensionBot;
using AscensionBot.AI;
using AscensionBot.AI.SharedStates;
using AscensionBot.Game;
using AscensionBot.Game.Objects;
using System.Collections.Generic;
using System.Linq;

namespace FelswornInfernalBot
{
    // Felsworn Infernal combat (self-contained). Fixed rotation, cast BY SPELL NAME (so it doesn't
    // depend on action-bar slots and can check real cooldowns / debuff presence):
    //   "Hateforged Barrier" — shield + self-heal, recast every time its ~20s cooldown is up.
    //   "Bane of Fire"       — damage-amplify debuff; kept on the target.
    //   "Sargeron Smite"     — used whenever it's off cooldown.
    //   "Fel Fireball"       — filler, spammed until the target dies.
    // Plus the shared behaviour: approach to range, defend the real attacker, anti-stuck, and
    // give up on targets we can't damage (behind a wall / no LoS).
    class CombatState : IBotState
    {
        internal const string Barrier = "Hateforged Barrier";
        internal const string BaneOfFire = "Bane of Fire";
        internal const string SargeronSmite = "Sargeron Smite";
        internal const string FelFireball = "Fel Fireball";

        const int Range = 25; // abilities reach 30 yds; keep a safe margin inside that.

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
            // client during a loading screen / zoning / disconnect throws a native
            // AccessViolationException that isn't catchable and crashes the bot.
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
                if (!Wait.For("FelswornPopCombat", 400))
                    return;

                botStates.Pop();

                // If something else is still on us, keep fighting it instead of walking off to rest.
                var threat = container.FindThreat();
                if (threat != null && threat.Health > 0 && !threat.TappedByOther)
                {
                    botStates.Push(new CombatState(botStates, container, threat, loot));
                    return;
                }

                // Rest after the fight. Loot first if enabled (off by default; see
                // CasterLootEnabled in settings). LootState runs before RestState.
                botStates.Push(new RestState(botStates, container));
                if (loot && !stolen)
                    botStates.Push(new LootState(botStates, container, target));
                return;
            }

            // Never interrupt our own cast — not even to chase a target that walked out of range.
            if (player.IsCasting || player.IsChanneling)
                return;

            if (player.TargetGuid != target.Guid)
                player.SetTarget(target.Guid);

            var distance = player.Position.DistanceTo(target.Position);

            // Approach to ~25 yds. Straight line (no mmaps). We deliberately do NOT gate on
            // player.InLosWith() — the native LoS raycast is unreliable on Ascension.
            if (distance > Range)
            {
                // While closing in, if a DIFFERENT mob is actually attacking us and is closer than
                // the target we're chasing, switch to it instead of ignoring the thing hitting us.
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

                // Anti-stuck: jump/strafe out if we stop making progress against terrain.
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

            // In range but can't damage it for a while (no line of sight, behind a wall...)? Give
            // up: blacklist it and pop back so we pick a different target.
            if (watchdog.ShouldGiveUp(target))
            {
                container.Probe?.BlacklistedMobIds?.Add(target.Guid);
                if (container.BotSettings.PermanentlyBlacklistUnreachableTargets)
                    Repository.AddBlacklistedMob(target.Guid);
                player.StopAllMovement();
                botStates.Pop();
                return;
            }

            // --- ROTATION (by spell name) ---
            //
            // IMPORTANT: we do NOT gate on player.IsSpellReady() here. The client cooldown read
            // (IsSpellOnCooldown) is unreliable on Ascension — it tends to always report "ready",
            // which made the top-priority spell (the Barrier) fire every single tick and starve
            // the Fel Fireball filler (it never got cast). Instead we drive the fixed-cooldown
            // spells on our own timers and let the game's global cooldown decide what actually
            // fires when we press two spells in one tick (an on-cooldown cast simply fizzles).

            // 1) "Hateforged Barrier" — shield + self-heal, every 20s no matter what.
            if (Wait.For("FelswornBarrier", 20000))
            {
                player.LuaCall($"CastSpellByName(\"{Barrier}\")");
                return;
            }

            // 2) "Bane of Fire" — keep the damage-amplify debuff on the target. Re-checked on a
            //    throttle so a name/aura mismatch can't starve the filler by recasting every tick.
            if (!target.HasDebuff(BaneOfFire) && Wait.For("FelswornBane", 1500))
            {
                player.LuaCall($"CastSpellByName(\"{BaneOfFire}\")");
                return;
            }

            // 3+4) "Sargeron Smite" when it's available, otherwise spam "Fel Fireball". Pressing
            //      both in one tick lets the GCD choose: Smite goes off if it's ready (and blocks
            //      the fireball via the GCD); if Smite is on cooldown it fizzles and the fireball
            //      casts. No cooldown read needed.
            if (Wait.For("FelswornAttack", 300))
                player.LuaCall($"CastSpellByName(\"{SargeronSmite}\"); CastSpellByName(\"{FelFireball}\");");
        }
    }
}
