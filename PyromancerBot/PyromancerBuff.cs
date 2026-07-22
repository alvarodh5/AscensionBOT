using AscensionBot;
using AscensionBot.Game.Objects;

namespace PyromancerBot
{
    // Improvement 2: keep two self-buffs ALWAYS active — "Seal of Al'ar" and "Ashen Skin".
    //
    // Every tick (in combat and while resting) we check the player's active auras; if either buff
    // is missing we recast it. In-game these are bound to keys C and V, but we re-apply them by
    // spell name with CastSpellByName so upkeep doesn't depend on which keybind/action slot they
    // sit on — this is exactly how the shared combat code casts spells elsewhere in the bot.
    static class PyromancerBuff
    {
        public const string SealOfAlar = "Seal of Al'ar";
        public const string AshenSkin = "Ashen Skin";

        // Try to keep both buffs up. Casts at most ONE buff per call (a global cooldown means a
        // second cast in the same tick would just fizzle) and throttles so we don't flood the
        // server. Returns true if it fired a cast this tick, so the caller can return and not
        // stomp the cast with an attack or other action.
        public static bool Maintain(LocalPlayer player)
        {
            if (player.IsCasting || player.IsChanneling)
                return false;

            string missing = null;
            if (!player.HasBuff(SealOfAlar))
                missing = SealOfAlar;
            else if (!player.HasBuff(AshenSkin))
                missing = AshenSkin;

            if (missing == null)
                return false;

            // Throttle so a buff that's still on cooldown (or a cast that hasn't registered yet)
            // doesn't get spammed every frame.
            if (!Wait.For("PyromancerBuff", 750))
                return false;

            player.LuaCall($"CastSpellByName(\"{missing}\")");
            return true;
        }
    }
}
