using AscensionBot.Game;
using System.Collections.Generic;
using System.Linq;

namespace AscensionBot
{
    public class TravelPath
    {
        public TravelPath(int id, string name, IEnumerable<Position> waypoints)
        {
            Id = id;
            Name = name;
            Waypoints = waypoints.ToArray();
        }

        public int Id { get; }

        public string Name { get; }

        public Position[] Waypoints { get; }
    }
}
