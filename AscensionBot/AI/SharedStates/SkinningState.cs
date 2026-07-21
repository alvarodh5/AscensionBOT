using System;
using System.Collections.Generic;
using System.Linq;
using AscensionBot.Game;
using AscensionBot.Game.Enums;
using AscensionBot.Game.Frames;
using AscensionBot.Game.Objects;

namespace AscensionBot.AI.SharedStates
{
    public class SkinningState : IBotState
    {
        const string Skinning = "Skinning";

        readonly Stack<IBotState> botStates;
        readonly IDependencyContainer container;
        readonly WoWUnit target;
        readonly LocalPlayer player;
        readonly int startTime = Environment.TickCount;

        State state = State.Initial;
        LootFrame lootFrame;
        int lootIndex = 0;

        public SkinningState(
            Stack<IBotState> botStates,
            IDependencyContainer container,
            WoWUnit target)
        {
            this.botStates = botStates;
            this.container = container;
            this.target = target;
            player = ObjectManager.Player;
        }

        public void Update()
        {
            // Hard safety exit checked FIRST, before touching the loot frame. After skinning,
            // the corpse/loot-frame pointers can go invalid on Ascension and reading item
            // strings throws every tick (address 462EF... spam) — this guarantees we bail out.
            if (!player.KnowsSpell(Skinning) || Environment.TickCount - startTime > 4000)
            {
                Exit();
                return;
            }

            // Finished looting everything in the frame.
            try
            {
                if (lootFrame != null && lootIndex >= lootFrame.LootItems.Count)
                {
                    Exit();
                    return;
                }
            }
            catch (Exception)
            {
                Exit();
                return;
            }

            if (state == State.Initial)
            {
                if (Wait.For("StartSkinningDelay", 800))
                {
                    target.Interact();
                    state = State.StartingSkinning;
                }
            }
            else if (state == State.StartingSkinning)
            {
                if (Wait.For("WaitingForSkinningCastDelay", 500))
                {
                    if (!player.IsCasting)
                    {
                        // We probably failed to skin for some reason (e.g. insufficient skill level).
                        Exit();
                        return;
                    }
                    state = State.Skinning;
                }
            }
            else if (state == State.Skinning)
            {
                if (Wait.For("SkinningDelay", 2000))
                {
                    state = State.LootFrameReady;
                    lootFrame = new LootFrame();
                }
            }
            else if (state == State.LootFrameReady)
            {
                try
                {
                    var itemToLoot = lootFrame.LootItems.ElementAt(lootIndex);
                    var itemQuality = ItemQuality.Common;
                    if (itemToLoot.Info != null)
                    {
                        itemQuality = itemToLoot.Info.Quality;
                    }

                    var poorQualityCondition = itemToLoot.IsCoins || itemQuality == ItemQuality.Poor && container.BotSettings.LootPoor;
                    var commonQualityCondition = itemToLoot.IsCoins || itemQuality == ItemQuality.Common && container.BotSettings.LootCommon;
                    var uncommonQualityCondition = itemToLoot.IsCoins || itemQuality == ItemQuality.Uncommon && container.BotSettings.LootUncommon;
                    var other = itemQuality != ItemQuality.Poor && itemQuality != ItemQuality.Common && itemQuality != ItemQuality.Uncommon;

                    if (itemQuality == ItemQuality.Rare || itemQuality == ItemQuality.Epic)
                        TelegramClientWrapper.SendItemNotification(player.Name, itemQuality, itemToLoot.ItemId);

                    if (itemToLoot.IsCoins
                        || ((string.IsNullOrWhiteSpace(container.BotSettings.LootExcludedNames) || !container.BotSettings.LootExcludedNames.Split('|').Any(en => (itemToLoot.Info?.Name ?? string.Empty).Contains(en)))
                        && (poorQualityCondition || commonQualityCondition || uncommonQualityCondition || other)))
                    {
                        itemToLoot.Loot();
                    }
                }
                catch (Exception e)
                {
                    Logger.Log($"SkinningState: skipping loot item {lootIndex} due to {e.GetType().Name}: {e.Message}");
                }

                // Always advance so we can't loop forever on one item.
                lootIndex++;
            }
        }

        void Exit()
        {
            player.StopAllMovement();
            player.LuaCall("CloseLoot()");
            botStates.Pop();
            botStates.Push(new EquipBagsState(botStates, container));
            if (player.IsSwimming)
            {
                var nearestWaypoint = container
                    .Hotspots
                    .Where(h => h != null)
                    .SelectMany(h => h.Waypoints)
                    .OrderBy(w => player.Position.DistanceTo(w))
                    .FirstOrDefault();
                if (nearestWaypoint != null)
                    botStates.Push(new MoveToPositionState(botStates, container, nearestWaypoint));
            }
        }

        enum State
        {
            Initial,
            StartingSkinning,
            Skinning,
            LootFrameReady,
        }
    }
}
