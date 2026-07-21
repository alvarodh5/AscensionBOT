using AscensionBot.Game.Enums;
using System;
using System.Net;
using System.Threading.Tasks;

namespace AscensionBot
{
    // Drop-in replacement for DiscordClientWrapper that sends notifications to Telegram.
    // Only sends when TelegramEnabled is true and a token + chat id are configured.
    // Uses the Telegram Bot API: https://api.telegram.org/bot<TOKEN>/sendMessage
    public static class TelegramClientWrapper
    {
        static bool enabled;
        static string botToken;
        static string chatId;

        static internal void Initialize(BotSettings botSettings)
        {
            enabled = botSettings.TelegramEnabled;
            botToken = botSettings.TelegramBotToken;
            chatId = botSettings.TelegramChatId;

            Logger.Log($"[Telegram] enabled={enabled} tokenSet={!string.IsNullOrWhiteSpace(botToken)} chatIdSet={!string.IsNullOrWhiteSpace(chatId)}");

            if (enabled)
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                // Immediate test message so the user can confirm their config works on startup.
                SendMessage("✅ AscensionBot conectado a Telegram.");
            }
        }

        static public void SendMessage(string message)
        {
            if (!enabled)
                return;
            if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId))
            {
                Logger.Log("[Telegram] No envío: falta TelegramBotToken o TelegramChatId en botSettings.json.");
                return;
            }

            // Fire-and-forget on a background thread so we never block the game thread.
            Task.Run(() =>
            {
                try
                {
                    var url = $"https://api.telegram.org/bot{botToken}/sendMessage" +
                              $"?chat_id={Uri.EscapeDataString(chatId)}" +
                              $"&text={Uri.EscapeDataString(message ?? string.Empty)}";
                    using (var client = new WebClient())
                        client.DownloadString(url);
                    Logger.Log("[Telegram] Mensaje enviado OK.");
                }
                catch (WebException we)
                {
                    // Telegram returns the reason in the response body (e.g. "chat not found",
                    // "Unauthorized" = bad token, "chat_id is empty" = bad id).
                    var detail = we.Message;
                    try
                    {
                        using (var resp = we.Response)
                        using (var sr = new System.IO.StreamReader(resp.GetResponseStream()))
                            detail = sr.ReadToEnd();
                    }
                    catch { }
                    Logger.Log($"[Telegram] FALLO: {detail}");
                }
                catch (Exception e)
                {
                    Logger.Log($"[Telegram] FALLO: {e.GetType().Name} - {e.Message}");
                }
            });
        }

        static internal void KillswitchAlert(string playerName) =>
            SendMessage($"🚨 ALERTA: {playerName} ha llegado a GM Island. Parando.");

        static internal void TeleportAlert(string playerName) =>
            SendMessage($"🚨 ALERTA: {playerName} ha sido teletransportado. Parando.");

        static public void SendItemNotification(string playerName, ItemQuality quality, int itemId) =>
            SendMessage($"{playerName}: encontrado item {quality}! https://classic.wowhead.com/item={itemId}");
    }
}
