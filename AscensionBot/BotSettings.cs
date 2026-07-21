using Newtonsoft.Json;
using System.Collections.Generic;

namespace AscensionBot
{
    // Sensible defaults are set here so botSettings.json can stay tiny — it only needs to
    // override the few things you actually change (Telegram, Food/Drink, profile).
    public class BotSettings
    {
        public string DatabaseType { get; set; } = "none";
        public string DatabasePath { get; set; } = "";

        // Legacy Discord (unused, replaced by Telegram).
        public bool DiscordBotEnabled { get; set; } = false;
        public string DiscordBotToken { get; set; } = "";
        public string DiscordGuildId { get; set; } = "0";
        public string DiscordRoleId { get; set; } = "0";
        public string DiscordChannelId { get; set; } = "0";

        public bool TelegramEnabled { get; set; } = false;
        public string TelegramBotToken { get; set; } = "";
        public string TelegramChatId { get; set; } = "";

        public string Food { get; set; } = "";
        public string Drink { get; set; } = "";

        public string TargetingIncludedNames { get; set; } = "";
        public string TargetingExcludedNames { get; set; } = "";

        // Wide by default = level filtering effectively off (Ascension scales mobs).
        public int LevelRangeMin { get; set; } = 100;
        public int LevelRangeMax { get; set; } = 100;

        public bool CreatureTypeBeast { get; set; } = true;
        public bool CreatureTypeDragonkin { get; set; } = true;
        public bool CreatureTypeDemon { get; set; } = true;
        public bool CreatureTypeElemental { get; set; } = true;
        public bool CreatureTypeHumanoid { get; set; } = true;
        public bool CreatureTypeUndead { get; set; } = true;
        public bool CreatureTypeGiant { get; set; } = true;

        public bool UnitReactionHostile { get; set; } = true;
        public bool UnitReactionUnfriendly { get; set; } = true;
        public bool UnitReactionNeutral { get; set; } = true;

        public bool LootPoor { get; set; } = true;
        public bool LootCommon { get; set; } = true;
        public bool LootUncommon { get; set; } = true;
        public string LootExcludedNames { get; set; } = "";

        public bool SellPoor { get; set; } = true;
        public bool SellCommon { get; set; } = true;
        public bool SellUncommon { get; set; } = false;
        public string SellExcludedNames { get; set; } = "";

        public int? GrindingHotspotId { get; set; }
        public int? CurrentTravelPathId { get; set; }
        public int? CurrentGatherRouteId { get; set; }

        public string CurrentBotName { get; set; } = "Caster";

        // Killswitches off by default: calling Stop() also halts AntiAfk and disconnects us,
        // which is worse than staying connected for an unattended farmer. Real disconnects are
        // handled separately (SessionStats).
        public bool UseTeleportKillswitch { get; set; } = false;
        public bool UseStuckInPositionKillswitch { get; set; } = false;
        public bool UseStuckInStateKillswitch { get; set; } = false;
        public bool UsePlayerTargetingKillswitch { get; set; } = false;
        public bool UsePlayerProximityKillswitch { get; set; } = false;

        public string PowerlevelPlayerName { get; set; } = "";

        public int TargetingWarningTimer { get; set; } = 7500;
        public int TargetingStopTimer { get; set; } = 15000;
        public int ProximityWarningTimer { get; set; } = 10000;
        public int ProximityStopTimer { get; set; } = 20000;

        public bool UseVerboseLogging { get; set; } = false;
        public bool PermanentlyBlacklistUnreachableTargets { get; set; } = false;

        public string Username { get; set; } = "";
        public string Password { get; set; } = "";

        public BotType LastUsedBotType { get; set; } = BotType.Grinding;

        [JsonIgnore]
        public Hotspot GrindingHotspot { get; set; }

        [JsonIgnore]
        public TravelPath CurrentTravelPath { get; set; }

        [JsonIgnore]
        public GatherRoute CurrentGatherRoute { get; set; }

        [JsonIgnore]
        public IList<string> CreatureTypes
        {
            get
            {
                var creatureTypes = new List<string>();

                if (CreatureTypeBeast) creatureTypes.Add("Beast");
                if (CreatureTypeDragonkin) creatureTypes.Add("Dragonkin");
                if (CreatureTypeDemon) creatureTypes.Add("Demon");
                if (CreatureTypeElemental) creatureTypes.Add("Elemental");
                if (CreatureTypeHumanoid) creatureTypes.Add("Humanoid");
                if (CreatureTypeUndead) creatureTypes.Add("Undead");
                if (CreatureTypeGiant) creatureTypes.Add("Giant");

                return creatureTypes;
            }
        }

        [JsonIgnore]
        public IList<string> UnitReactions
        {
            get
            {
                var unitReactions = new List<string>();

                if (UnitReactionHostile) unitReactions.Add("Hostile");
                if (UnitReactionUnfriendly) unitReactions.Add("Unfriendly");
                if (UnitReactionNeutral) unitReactions.Add("Neutral");

                return unitReactions;
            }
        }

        public enum BotType
        {
            Grinding,
            Powerlevel,
            Gathering,
        }
    }
}
