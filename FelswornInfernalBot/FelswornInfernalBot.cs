using AscensionBot;
using AscensionBot.AI;
using AscensionBot.Game;
using AscensionBot.Game.Objects;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace FelswornInfernalBot
{
    // "Felsworn Infernal" profile: ranged classless caster for Ascension.
    //  - Engages from ~25 yds, doesn't go to melee.
    //  - Fixed rotation, cast BY SPELL NAME (independent of action-bar slots):
    //      "Hateforged Barrier" = shield + self-heal, recast every time its ~20s cooldown is up
    //      "Bane of Fire"       = damage-amplify debuff, kept on the target
    //      "Sargeron Smite"     = used whenever it's off cooldown
    //      "Fel Fireball"       = filler, spammed until the target dies
    //  - Only STARTS fights at >=75% HP and >=50% mana (defends if attacked regardless).
    //  - Rests after each fight. Loot disabled by default (toggle CasterLootEnabled in settings).
    //  - Shares the common behaviour: anti-stuck, never targets pets, gives up on targets it
    //    can't damage (behind a wall / no LoS) and switches, defends the attacker on interrupt.
    [Export(typeof(IBot))]
    class FelswornInfernalBot : Bot, IBot
    {
        public string Name => "Felsworn Infernal";

        public string FileName => "FelswornInfernalBot.dll";

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

            return container;
        }

        public void Test(IDependencyContainer container) { }
    }
}
