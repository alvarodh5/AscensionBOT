using AscensionBot.Game.Objects;
using System;

namespace CasterBot
{
    // Periodic self-buff for the Caster: presses action slot 5 every ~30 minutes. The timer is
    // static so it's shared across states — the buff refreshes whether we're fighting or resting,
    // and the first cast fires as soon as the bot gets going.
    static class CasterBuff
    {
        const int BuffSlot = 5;
        const int BuffIntervalMs = 30 * 60 * 1000; // 30 minutes

        static int lastBuffTime;
        static bool everBuffed;

        // Casts the buff if it's due (and we're not mid-cast). Returns true if it fired this tick
        // so the caller can return and not stomp the cast with an attack/other action.
        public static bool TryBuff(LocalPlayer player)
        {
            if (player.IsCasting || player.IsChanneling)
                return false;

            if (everBuffed && Environment.TickCount - lastBuffTime < BuffIntervalMs)
                return false;

            player.LuaCall($"UseAction({BuffSlot})");
            lastBuffTime = Environment.TickCount;
            everBuffed = true;
            return true;
        }
    }
}
