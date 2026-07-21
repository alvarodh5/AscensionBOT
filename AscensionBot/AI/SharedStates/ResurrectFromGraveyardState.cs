using System;
using System.Collections.Generic;
using System.Linq;
using AscensionBot.Game;
using AscensionBot.Game.Objects;

namespace AscensionBot.AI.SharedStates
{
    public class ResurrectFromGraveyardState : IBotState
    {
        readonly Stack<IBotState> botStates;
        readonly IDependencyContainer container;
        readonly LocalPlayer player;

        WoWUnit spiritHealer;
        int lastInteractTime;
        bool logged;

        public ResurrectFromGraveyardState(Stack<IBotState> botStates, IDependencyContainer container)
        {
            this.botStates = botStates;
            this.container = container;
            player = ObjectManager.Player;
        }

        bool deathNotified;

        public void Update()
        {
            // Notify death once, when we first enter recovery.
            if (!deathNotified)
            {
                deathNotified = true;
                TelegramClientWrapper.SendMessage($"☠️ {player.Name} ha muerto. Resucitando en el cementerio...");
            }

            // Already alive -> done.
            if (!player.InGhostForm)
            {
                TelegramClientWrapper.SendMessage($"❤️ {player.Name} ha resucitado. Volviendo a farmear.");
                Wait.RemoveAll();
                botStates.Pop();
                return;
            }

            // Find the spirit healer. Try the standard name first, then any nearby unit whose
            // name contains "Spirit" (custom servers may name it differently).
            if (spiritHealer == null)
            {
                spiritHealer = ObjectManager.Units.FirstOrDefault(u => u.Name == "Spirit Healer")
                    ?? ObjectManager.Units
                        .Where(u => u.Name != null && u.Name.IndexOf("Spirit", StringComparison.OrdinalIgnoreCase) >= 0)
                        .OrderBy(u => u.Position.DistanceTo(player.Position))
                        .FirstOrDefault();

                if (spiritHealer == null)
                {
                    if (!logged)
                    {
                        Logger.Log("[Rez] No encuentro al Spirit Healer en la lista de objetos. Unidades cercanas:");
                        foreach (var u in ObjectManager.Units.OrderBy(u => u.Position.DistanceTo(player.Position)).Take(8))
                            Logger.Log($"    '{u.Name}' dist={(int)u.Position.DistanceTo(player.Position)} reaction={u.UnitReaction}");
                        logged = true;
                    }
                    return;
                }
                Logger.Log($"[Rez] Spirit Healer encontrado: '{spiritHealer.Name}' dist={(int)spiritHealer.Position.DistanceTo(player.Position)}");
            }

            var distance = player.Position.DistanceTo(spiritHealer.Position);

            // Walk (straight line) to the spirit healer if we're not close enough to interact.
            if (distance > 5)
            {
                player.MoveToward(spiritHealer.Position);
                return;
            }

            player.StopAllMovement();

            // Interact + fire every plausible resurrect-confirm, once per second, repeatedly.
            if (Environment.TickCount - lastInteractTime > 1000)
            {
                lastInteractTime = Environment.TickCount;
                Logger.Log($"[Rez] Interactuando con el Spirit Healer e intentando confirmar. Ghost={player.InGhostForm}");

                spiritHealer.Interact();
                player.LuaCall("AcceptResurrect()");
                player.LuaCall("AcceptXPLoss()");
                player.LuaCall("RetrieveCorpse()");
                player.LuaCall("for i=1,4 do local b=_G['StaticPopup'..i..'Button1']; if b and b:IsVisible() then b:Click() end end");
                player.LuaCall("for i=1,4 do local p=_G['StaticPopup'..i]; if p and p:IsShown() and p.which then StaticPopup_OnClick(p, 1) end end");
            }
        }
    }
}
