using AscensionBot;
using AscensionBot.AI;
using AscensionBot.Game;
using AscensionBot.Game.Enums;
using AscensionBot.Game.Objects;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace MeleeBot
{
    [Export(typeof(IBot))]
    class MeleeBot : Bot, IBot
    {
        public string Name => "Melee";

        public string FileName => "MeleeBot.dll";

        bool AdditionalTargetingCriteria(WoWUnit u) => true;

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
            bool loot = true) => new CombatState(botStates, container, target, loot);

        public IDependencyContainer GetDependencyContainer(BotSettings botSettings, Probe probe, IEnumerable<Hotspot> hotspots) =>
            new DependencyContainer(
                AdditionalTargetingCriteria,
                CreateRestState,
                CreateMoveToTargetState,
                CreatePowerlevelCombatState,
                CreateCombatState,
                botSettings,
                probe,
                hotspots);

        public void Test(IDependencyContainer container)
        {
            Console.WriteLine("");
            Console.WriteLine("========== ASCENSION TEST: boton pulsado ==========");

            ThreadSynchronizer.RunOnMainThread(() =>
            {
                Console.WriteLine(">>> Dentro de RunOnMainThread (el hook del hilo principal FUNCIONA) <<<");

                Dump("Player pointer", () => ObjectManager.Player.Pointer.ToString("X"));
                Dump("Player GUID", () => ObjectManager.Player.Guid.ToString("X"));
                Dump("Name", () => ObjectManager.Player.Name);                    // GetName (vtable)
                Dump("Level", () => ObjectManager.Player.Level);
                Dump("Health", () => $"{ObjectManager.Player.Health} / {ObjectManager.Player.MaxHealth}");
                Dump("Mana", () => $"{ObjectManager.Player.Mana} / {ObjectManager.Player.MaxMana}");
                Dump("Position", () => ObjectManager.Player.Position.ToString());  // GetPosition (vtable)
                Dump("Class (byte)", () => ObjectManager.Player.Class.ToString());
                Dump("TargetGuid", () => ObjectManager.Player.TargetGuid.ToString("X"));
                Dump("MapId", () => ObjectManager.MapId);
                Dump("Visible units", () => ObjectManager.Units.Count());          // EnumerateVisibleObjects
                Dump("Visible players", () => ObjectManager.Players.Count());
                Dump("LuaCall", () =>
                {
                    ObjectManager.Player.LuaCall("DEFAULT_CHAT_FRAME:AddMessage('AscensionBot LuaCall OK')");
                    return "enviado (mira el chat del juego)";
                });

                Console.WriteLine("--- Unidades cercanas (nombre | reaccion | flags | dist | hp) ---");
                try
                {
                    var me = ObjectManager.Player;
                    foreach (var u in ObjectManager.Units
                        .Where(x => x.Health > 0)
                        .OrderBy(x => x.Position.DistanceTo(me.Position))
                        .Take(10))
                    {
                        Console.WriteLine($"    {u.Name,-22} | lvl {u.Level,3} | {u.CreatureType,-9} | {u.UnitReaction,-9} | flags=0x{(uint)u.UnitFlags:X8} | {(int)u.Position.DistanceTo(me.Position),3}m");
                    }
                }
                catch (System.Exception e)
                {
                    Console.WriteLine($"    [FALLA unidades] {e.GetType().Name} - {e.Message}");
                }

                Console.WriteLine("===================================================");
            });

            Console.WriteLine("========== Test() retornado ==========");
        }

        static void Dump(string label, Func<object> read)
        {
            try
            {
                Console.WriteLine($"[ OK ]  {label,-16}: {read()}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[FALLA] {label,-16}: {e.GetType().Name} - {e.Message}");
            }
        }
    }
}

