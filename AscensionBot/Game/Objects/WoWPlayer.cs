using AscensionBot.Game.Enums;
using System;

namespace AscensionBot.Game.Objects
{
    public class WoWPlayer : WoWUnit
    {
        internal WoWPlayer(
            IntPtr pointer,
            ulong guid,
            ObjectType objectType)
            : base(pointer, guid, objectType)
        {
        }

        public bool IsEating
        {
            get
            {
                return HasBuff("Food") || HasDebuff("Food");
            }
        }

        public bool IsDrinking
        {
            get
            {
                return HasBuff("Drink") || HasDebuff("Drink");
            }
        }
    }
}
