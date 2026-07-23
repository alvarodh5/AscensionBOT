using AscensionBot.Game.Objects;
using System;

namespace AscensionBot.AI
{
    // Shared "am I actually able to kill this?" watchdog for the ranged profiles (the melee base,
    // CombatStateBase, already has its own equivalent 30s check). If the target's health stops
    // dropping for a while we almost certainly can't reach or see it — it ran behind a wall, we
    // have no line of sight, it's on a ledge, etc. The caller then blacklists it and switches
    // targets instead of standing there casting into a wall forever.
    //
    // Only meaningful while we're in range and trying to attack, so the owner calls Reset() during
    // the approach (movement) so travel time never counts against the give-up timer.
    public class CombatWatchdog
    {
        readonly int giveUpMs;
        int lastProgressTick;
        int lastHealth;
        bool haveBaseline;

        public CombatWatchdog(int giveUpMs = 20000)
        {
            this.giveUpMs = giveUpMs;
            lastProgressTick = Environment.TickCount;
        }

        // Call while approaching / not in a position to attack: keeps the timer from firing on
        // travel time.
        public void Reset()
        {
            lastProgressTick = Environment.TickCount;
            haveBaseline = false;
        }

        // Call once per combat tick while in range and attacking. Returns true when the target's
        // health hasn't decreased for longer than giveUpMs (we can't damage it -> give up).
        public bool ShouldGiveUp(WoWUnit target)
        {
            int hp;
            try { hp = target?.Health ?? 0; }
            catch { return false; }

            if (!haveBaseline)
            {
                haveBaseline = true;
                lastHealth = hp;
                lastProgressTick = Environment.TickCount;
                return false;
            }

            // Any drop in health = real progress; reset the timer. (A rise means it regened/reset;
            // we track the new value but don't treat it as progress.)
            if (hp != lastHealth)
            {
                if (hp < lastHealth)
                    lastProgressTick = Environment.TickCount;
                lastHealth = hp;
            }

            return Environment.TickCount - lastProgressTick > giveUpMs;
        }
    }
}
