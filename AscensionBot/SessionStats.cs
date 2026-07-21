using AscensionBot.AI;
using AscensionBot.Game;
using System;
using System.Text;

namespace AscensionBot
{
    // Session statistics + Telegram reporting + disconnect detection for the Caster profile.
    // Only active while Running (started by the Caster; the Melee profile never starts it, so
    // everything here is a no-op for other profiles). Tick() is driven from the bot's main loop
    // (main thread) so all game reads are safe — no extra threads, no deadlocks.
    public static class SessionStats
    {
        static readonly object gate = new object();

        public static bool Running { get; private set; }
        public static bool DisconnectRequested { get; private set; }

        static Bot bot;
        static IDependencyContainer container;

        const int ReportIntervalMs = 30 * 60 * 1000;
        const int DisconnectMs = 5000;

        static int startTick, lastTick, lastReportTick, notLoggedMs;

        static long combatMs, restMs, moveMs, otherMs;

        static int startLevel, startXp, startXpMax;

        static int enemyKills, combatsStarted, characterDeaths, targetsAbandoned;
        static long combatDurTotalMs; static int combatDurCount; static long combatStartTick;
        static long manaSpentTotal; static int manaSamples; static int combatStartManaPct;

        public static void Start(Bot b, IDependencyContainer c)
        {
            lock (gate)
            {
                if (Running) return;
                bot = b; container = c;
                var now = Environment.TickCount;
                startTick = lastTick = lastReportTick = now;
                notLoggedMs = 0;
                combatMs = restMs = moveMs = otherMs = 0;
                enemyKills = combatsStarted = characterDeaths = targetsAbandoned = 0;
                combatDurTotalMs = 0; combatDurCount = 0; combatStartTick = 0;
                manaSpentTotal = 0; manaSamples = 0; combatStartManaPct = 0;
                Running = true; DisconnectRequested = false;
                ReadXp(out startLevel, out startXp, out startXpMax);
                TelegramClientWrapper.SendMessage("▶️ Sesión de farmeo iniciada (Caster).");
            }
        }

        // Called every iteration of the bot's main loop (main thread). stateName = current state.
        public static void Tick(string stateName)
        {
            if (!Running) return;
            try
            {
                var now = Environment.TickCount;
                var delta = now - lastTick; if (delta < 0) delta = 0;
                lastTick = now;

                if (stateName != null)
                {
                    if (stateName.IndexOf("Combat", StringComparison.OrdinalIgnoreCase) >= 0) combatMs += delta;
                    else if (stateName.IndexOf("Rest", StringComparison.OrdinalIgnoreCase) >= 0) restMs += delta;
                    else if (stateName.IndexOf("Move", StringComparison.OrdinalIgnoreCase) >= 0 || stateName.IndexOf("Grind", StringComparison.OrdinalIgnoreCase) >= 0) moveMs += delta;
                    else otherMs += delta;
                }

                if (now - lastReportTick >= ReportIntervalMs)
                {
                    lastReportTick = now;
                    Report("📊 Informe (cada 30 min)");
                }

                bool loggedIn;
                try { loggedIn = ObjectManager.IsLoggedIn; } catch { loggedIn = false; }
                if (!loggedIn)
                {
                    notLoggedMs += delta;
                    if (notLoggedMs >= DisconnectMs && !DisconnectRequested)
                        HandleDisconnect();
                }
                else notLoggedMs = 0;
            }
            catch (Exception e) { Logger.Log("[Stats] Tick: " + e.Message); }
        }

        public static void OnCombatStart()
        {
            if (!Running) return;
            lock (gate)
            {
                combatsStarted++;
                combatStartTick = Environment.TickCount;
                try { combatStartManaPct = ObjectManager.Player.ManaPercent; } catch { combatStartManaPct = 0; }
            }
        }

        public static void OnKill()
        {
            if (!Running) return;
            lock (gate)
            {
                enemyKills++;
                if (combatStartTick > 0)
                {
                    combatDurTotalMs += Environment.TickCount - combatStartTick;
                    combatDurCount++;
                    try { manaSpentTotal += combatStartManaPct - ObjectManager.Player.ManaPercent; manaSamples++; } catch { }
                    combatStartTick = 0;
                }
            }
        }

        public static void OnCharacterDeath() { if (Running) lock (gate) characterDeaths++; }
        public static void OnTargetAbandoned() { if (Running) lock (gate) targetsAbandoned++; }

        public static void Report(string title) { if (Running) SendReport(title); }

        // Called from Bot.Stop() for any stop (manual, killswitch...). Sends the final report once.
        public static void StopAndReport(string reason)
        {
            lock (gate)
            {
                if (!Running) return;
                Running = false;
            }
            SendReport($"⏹️ Sesión finalizada ({reason})");
        }

        static void HandleDisconnect()
        {
            DisconnectRequested = true;
            TelegramClientWrapper.SendMessage("🔌 Desconexión detectada (personaje perdido >5s). No se reconecta.");
            SendReport("📊 Informe final (desconexión)");
            lock (gate) Running = false;
            try { bot?.Stop(); } catch { }
        }

        static void SendReport(string title)
        {
            try { TelegramClientWrapper.SendMessage(BuildReport(title)); }
            catch (Exception e) { Logger.Log("[Stats] SendReport: " + e.Message); }
        }

        static string BuildReport(string title)
        {
            var now = Environment.TickCount;
            var hours = Math.Max((now - startTick) / 3600000.0, 0.0001);

            ReadXp(out int level, out int xp, out int xpMax);
            long xpGained = level == startLevel
                ? Math.Max(0, xp - startXp)
                : Math.Max(0, startXpMax - startXp) + xp; // aprox. (ignora niveles intermedios completos)
            var xpPct = xpMax > 0 ? (int)(xp * 100.0 / xpMax) : 0;

            var killsPerHour = enemyKills / hours;
            var avgCombat = combatDurCount > 0 ? combatDurTotalMs / (double)combatDurCount / 1000.0 : 0;
            var deathsPer100 = combatsStarted > 0 ? characterDeaths * 100.0 / combatsStarted : 0;
            var manaPerEnemy = manaSamples > 0 ? manaSpentTotal / (double)manaSamples : 0;
            var blacklist = 0;
            try { blacklist = container?.Probe?.BlacklistedMobIds?.Count ?? 0; } catch { }

            var sb = new StringBuilder();
            sb.AppendLine(title);
            sb.AppendLine($"Sesión: {Fmt(now - startTick)}");
            sb.AppendLine($"Nivel: {level} ({xpPct}% XP)");
            sb.AppendLine($"XP obtenida: {xpGained}  |  XP/h: {(long)(xpGained / hours)}");
            sb.AppendLine($"Muertes enemigos: {enemyKills}  |  /h: {killsPerHour:0.0}");
            sb.AppendLine($"Combates iniciados: {combatsStarted}  |  dur. media: {avgCombat:0.0}s");
            sb.AppendLine($"Tiempo en combate: {Fmt(combatMs)}");
            sb.AppendLine($"Tiempo en movimiento: {Fmt(moveMs)}");
            sb.AppendLine($"Tiempo descansando: {Fmt(restMs)}");
            sb.AppendLine("Loot: desactivado");
            sb.AppendLine($"Maná neto medio/enemigo: {manaPerEnemy:0.0}%");
            sb.AppendLine($"Muertes del personaje: {characterDeaths}  |  /100 combates: {deathsPer100:0.0}");
            sb.AppendLine($"Objetivos abandonados: {targetsAbandoned}  |  en blacklist: {blacklist}");
            return sb.ToString();
        }

        static string Fmt(long ms)
        {
            var t = TimeSpan.FromMilliseconds(ms < 0 ? 0 : ms);
            return t.Hours > 0 ? $"{t.Hours}h {t.Minutes}m" : $"{t.Minutes}m {t.Seconds}s";
        }

        static void ReadXp(out int level, out int xp, out int xpMax)
        {
            level = 0; xp = 0; xpMax = 1;
            try
            {
                var p = ObjectManager.Player;
                level = p.Level;
                var r = p.LuaCallWithResults("{0} = UnitXP('player'); {1} = UnitXPMax('player')");
                if (r != null)
                {
                    if (r.Length >= 1) int.TryParse(r[0], out xp);
                    if (r.Length >= 2) int.TryParse(r[1], out xpMax);
                }
                if (xpMax <= 0) xpMax = 1;
            }
            catch { }
        }
    }
}
