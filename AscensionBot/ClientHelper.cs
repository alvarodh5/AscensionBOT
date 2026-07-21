using AscensionBot.Game.Enums;
using System;
using System.Diagnostics;
using System.Linq;

namespace AscensionBot
{
    public static class ClientHelper
    {
        public static readonly ClientVersion ClientVersion;

        static ClientHelper()
        {
            // Project Ascension ships the client as "Ascension.exe". Also accept the
            // stock names as a fallback for testing on a vanilla emulator client.
            var candidateNames = new[] { "Ascension", "WoW", "Wow" };
            var process = candidateNames.SelectMany(Process.GetProcessesByName).FirstOrDefault();
            if (process == null)
                throw new InvalidOperationException("Game client process not found. Is Ascension running?");

            var clientVersion = process.MainModule.FileVersionInfo.FileVersion ?? "";

            // Match on the build number instead of an exact string. Custom clients
            // (e.g. Project Ascension) report the version with a different format
            // ("3.3.5.12340" vs "3, 3, 5, 12340") but keep the original build number.
            if (clientVersion.Contains("12340"))
            {
                ClientVersion = ClientVersion.WotLK;
            }
            else if (clientVersion.Contains("8606"))
            {
                ClientVersion = ClientVersion.TBC;
            }
            else if (clientVersion.Contains("5875"))
            {
                ClientVersion = ClientVersion.Vanilla;
            }
            else
                throw new InvalidOperationException($"Unknown client version: '{clientVersion}'.");
        }
    }
}
