using AscensionBot.Game;
using AscensionBot.Game.Objects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AscensionBot.AI.SharedStates
{
    public class GrindState : IBotState
    {
        static readonly Random random = new Random();

        // How close to the anchor counts as "arrived" when roaming back.
        const float AnchorRadius = 20f;

        readonly Stack<IBotState> botStates;
        readonly IDependencyContainer container;
        readonly LocalPlayer player;

        // The spot where grinding started. When no mob is nearby we roam back toward it,
        // which keeps the bot in the chosen area and out of towns (no map/mmaps needed).
        readonly Position anchor;

        public GrindState(Stack<IBotState> botStates, IDependencyContainer container)
        {
            this.botStates = botStates;
            this.container = container;
            player = ObjectManager.Player;
            anchor = player.Position;
        }

        // Resurrection Sickness after a Spirit Healer res: -75% to all stats for ~10 min.
        // Fighting while sick = near-certain re-death (death loop), so we wait it out.
        static readonly string[] ResurrectionSicknessNames = { "Resurrection Sickness", "Enfermedad por resurrección" };

        public void Update()
        {
            // If we have resurrection sickness, don't engage anything — just wait (stand) for it
            // to expire. AntiAfk keeps firing from the main loop so we won't get kicked.
            if (ResurrectionSicknessNames.Any(n => player.HasDebuff(n) || player.HasBuff(n)))
            {
                player.StopAllMovement();
                return;
            }

            var enemyTarget = container.FindClosestTarget();

            if (enemyTarget != null)
            {
                player.SetTarget(enemyTarget.Guid);
                botStates.Push(container.CreateMoveToTargetState(botStates, container, enemyTarget));
            }
            else
            {
                var hotspot = container.GetCurrentHotspot();

                // No hotspot configured (e.g. DatabaseType "none"): roam back toward the
                // anchor (the spot where we pressed Start) to look for mobs. This keeps us
                // in the chosen grind area and out of towns without needing map data.
                // When we're already at the anchor, just wait for respawns.
                if (hotspot?.Waypoints == null || hotspot.Waypoints.Length == 0)
                {
                    if (player.Position.DistanceTo(anchor) > AnchorRadius)
                        player.MoveToward(anchor);
                    else
                        player.StopAllMovement();
                    return;
                }

                var waypointCount = hotspot.Waypoints.Length;
                var waypoint = hotspot.Waypoints[random.Next(0, waypointCount)];
                botStates.Push(new MoveToHotspotWaypointState(botStates, container, waypoint));
            }
        }
    }
}
