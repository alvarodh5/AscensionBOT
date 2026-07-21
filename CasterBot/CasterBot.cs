using AscensionBot;
using AscensionBot.AI;
using AscensionBot.Game;
using AscensionBot.Game.Objects;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace CasterBot
{
    // "Caster" profile: ranged classless caster for Ascension.
    //  - Attacks from 25 yds with random abilities on action slots 1/2/3.
    //  - Self-heal on slot 4 below 30% HP.
    //  - Self-buff on slot 5, recast every ~30 minutes.
    //  - Only STARTS fights at >=75% HP and >=50% mana (defends if attacked regardless).
    //  - Rests after each fight. Loot disabled by default (toggle CasterLootEnabled in settings).
    [Export(typeof(IBot))]
    class CasterBot : Bot, IBot
    {
        public string Name => "Caster";

        public string FileName => "CasterBot.dll";

        // Only initiate VOLUNTARY combat when healthy enough. Threats bypass this filter
        // (FindClosestTarget returns FindThreat() before applying this), so we still defend
        // if something attacks us even when below these thresholds.
        bool AdditionalTargetingCriteria(WoWUnit unit)
        {
            var p = ObjectManager.Player;
            var healthy = p.HealthPercent >= 75;
            var hasMana = p.MaxMana <= 0 || p.ManaPercent >= 50;
            return healthy && hasMana;
        }

        IBotState CreateRestState(Stack<IBotState> botStates, IDependencyContainer container) =>
            new RestState(botStates, container);

        IBotState CreateMoveToTargetState(Stack<IBotState> botStates, IDependencyContainer container, WoWUnit target) =>
            new MoveToTargetState(botStates, container, target);

        IBotState CreatePowerlevelCombatState(Stack<IBotState> botStates, IDependencyContainer container, WoWUnit target, WoWPlayer powerlevelTarget) =>
            new PowerlevelCombatState(botStates, container, target, powerlevelTarget);

        IBotState CreateCombatState(
            Stack<IBotState> botStates,
            IDependencyContainer container,
            WoWUnit target,
            bool loot = false) => new CombatState(botStates, container, target, loot);

        public IDependencyContainer GetDependencyContainer(BotSettings botSettings, Probe probe, IEnumerable<Hotspot> hotspots)
        {
            var container = new DependencyContainer(
                AdditionalTargetingCriteria,
                CreateRestState,
                CreateMoveToTargetState,
                CreatePowerlevelCombatState,
                CreateCombatState,
                botSettings,
                probe,
                hotspots);

            // Start session stats + Telegram reporting + disconnect detection (Caster only).
            SessionStats.Start(this, container);

            return container;
        }

        public void Test(IDependencyContainer container) { }
    }
}
