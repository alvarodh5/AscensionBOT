using AscensionBot.Game;
using System.Collections.Generic;

namespace AscensionBot
{
    // A no-op repository. Stores nothing and returns empty results.
    // Used when DatabaseType is "none" so the bot never touches SQLite/MSSQL.
    // Fine for a simple grind bot that fights whatever is in the current zone.
    internal class NullRepository : IRepository
    {
        public void Initialize(string connectionString) { }

        public void AddBlacklistedMob(ulong guid) { }

        public Hotspot AddHotspot(string description, string zone = "", string faction = "", string waypointsJson = "", Npc innkeeper = null, Npc repairVendor = null, Npc ammoVendor = null, int minLevel = 0, TravelPath travelPath = null, bool safeForGrinding = false, Position[] waypoints = null) => null;

        public Npc AddNpc(string name, bool isInnkeeper, bool sellsAmmo, bool repairs, bool quest, bool horde, bool alliance, float positionX, float positionY, float positionZ, string zone) => null;

        public void AddReportSignature(string playerName, int commandId) { }

        public TravelPath AddTravelPath(string name, string waypointsJson) => null;

        public GatherRoute AddGatherRoute(string name, string nodeNames, TravelPath travelPath) => null;

        public bool BlacklistedMobExists(ulong guid) => false;

        public void DeleteCommand(int id) { }

        public void DeleteCommandsForPlayer(string player) { }

        public IList<CommandModel> GetCommandsForPlayer(string playerName) => new List<CommandModel>();

        // Return a safe empty summary (CommandId -1 makes SignLatestReport a no-op).
        // Returning null here causes a NullReferenceException in SignLatestReport.
        public ReportSummary GetLatestReportSignatures() => new ReportSummary(-1, new List<ReportSignature>());

        public List<ulong> ListBlacklistedMobs() => new List<ulong>();

        public List<Hotspot> ListHotspots() => new List<Hotspot>();

        public List<Npc> ListNPCs() => new List<Npc>();

        public List<TravelPath> ListTravelPaths() => new List<TravelPath>();

        public List<GatherRoute> ListGatherRoutes() => new List<GatherRoute>();

        public bool NpcExists(string name) => false;

        public void RemoveBlacklistedMob(ulong guid) { }

        public bool RowExistsSql(string sql) => false;

        public bool TravelPathExists(string name) => false;
    }
}
