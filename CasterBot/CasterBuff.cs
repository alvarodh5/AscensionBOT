using AscensionBot;
using AscensionBot.Game;
using AscensionBot.Game.Objects;

namespace CasterBot
{
    // Primalist self-buff upkeep: keeps "Grove Instinct" active by pressing action slot 5
    // whenever the buff is missing (throttled so a cooldown/latency doesn't spam it).
    //
    // This is PRESENCE-based (HasBuff), not a fixed 30-min timer: a timer would leave us
    // unbuffed for a long time after the buff was cleared — most importantly after dying and
    // resurrecting at the Spirit Healer, which strips all buffs. Checking HasBuff re-applies it
    // the moment it drops, exactly like the Pyromancer profile does with its buffs.
    static class CasterBuff
    {
        public const string BuffName = "Grove Instinct";
        const int BuffSlot = 5;

        // Presses slot 5 if "Grove Instinct" is not currently up. Returns true if it fired a cast
        // this tick, so the caller can return and not stomp the cast with an attack/other action.
        public static bool TryBuff(LocalPlayer player)
        {
            // Never touch the game's Lua engine unless we're safely in the world and alive.
            if (player == null || !ObjectManager.IsLoggedIn || player.InGhostForm)
                return false;

            if (player.IsCasting || player.IsChanneling)
                return false;

            if (player.HasBuff(BuffName))
                return false;

            // Throttle so a buff still on cooldown (or a cast that hasn't registered yet) isn't
            // spammed every frame.
            if (!Wait.For("PrimalistBuff", 750))
                return false;

            player.LuaCall($"UseAction({BuffSlot})");
            return true;
        }
    }
}
