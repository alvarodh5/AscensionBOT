using AscensionBot.Game;
using AscensionBot.Game.Enums;
using AscensionBot.Game.Frames;
using AscensionBot.Game.Objects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AscensionBot.AI.SharedStates
{
    public class LootState : IBotState
    {
        readonly Stack<IBotState> botStates;
        readonly IDependencyContainer container;
        readonly WoWUnit target;
        readonly LocalPlayer player;
        readonly StuckHelper stuckHelper;
        readonly int startTime = Environment.TickCount;

        int stuckCount;
        LootFrame lootFrame;
        int lootIndex;
        LootStates currentState;

        public LootState(
            Stack<IBotState> botStates,
            IDependencyContainer container,
            WoWUnit target)
        {
            this.botStates = botStates;
            this.container = container;
            this.target = target;
            player = ObjectManager.Player;
            stuckHelper = new StuckHelper(botStates, container);
        }

        public void Update()
        {
            // Hard safety exit, checked BEFORE touching the loot frame. Guarantees we can never
            // get permanently stuck on the loot screen (e.g. if reading Ascension's loot data
            // throws, which would otherwise make the normal exit check below throw every tick).
            // Always close any open loot window on the way out.
            if (stuckCount > 5 || Environment.TickCount - startTime > 4000)
            {
                player.StopAllMovement();
                player.LuaCall("CloseLoot()");
                botStates.Pop();
                botStates.Push(new SkinningState(botStates, container, target));
                return;
            }

            if (player.Position.DistanceTo(target.Position) >= 5)
            {
                var nextWaypoint = Navigation.GetNextWaypoint(ObjectManager.MapId, player.Position, target.Position, false);
                player.MoveToward(nextWaypoint);

                if (!player.IsImmobilized)
                {
                    if (stuckHelper.CheckIfStuck())
                        stuckCount++;
                }
            }

            if (target.CanBeLooted && currentState == LootStates.Initial && player.Position.DistanceTo(target.Position) < 5)
            {
                player.StopAllMovement();

                if (Wait.For("StartLootDelay", 100))
                {
                    target.Interact();
                    currentState = LootStates.RightClicked;
                    return;
                }
            }

            // State Transition Conditions:
            //  - target can't be looted (no items to loot)
            //  - loot frame is open, but we've already looted everything we want
            //  - stuck count is greater than 5 (perhaps the corpse is in an awkward position the character can't reach)
            //  - we've been in the loot state for over 10 seconds (again, perhaps the corpse is unreachable. most common example of this is when a mob dies on a cliff that we can't climb)
            if ((currentState == LootStates.Initial && !target.CanBeLooted) || (lootFrame != null && lootIndex >= lootFrame.LootItems.Count))
            {
                player.StopAllMovement();
                player.LuaCall("CloseLoot()");   // don't leave the loot window open (we skip greys now)
                botStates.Pop();
                botStates.Push(new SkinningState(botStates, container, target));
                return;
            }

            if (currentState == LootStates.RightClicked && Wait.For("LootFrameDelay", 500))
            {
                lootFrame = new LootFrame();
                currentState = LootStates.LootFrameReady;
            }

            if (currentState == LootStates.LootFrameReady && Wait.For("LootDelay", 75))
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
                    // Never get stuck on a single weird item (e.g. Ascension custom loot with no
                    // cache info). Log and move on to the next slot.
                    Logger.Log($"LootState: skipping loot item {lootIndex} due to {e.GetType().Name}: {e.Message}");
                }

                // Always advance so we can't loop forever on one item.
                lootIndex++;
            }
        }
    }

    enum LootStates
    {
        Initial,
        RightClicked,
        LootFrameReady
    }
}
