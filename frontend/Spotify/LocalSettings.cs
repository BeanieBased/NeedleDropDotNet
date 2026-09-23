using System;
using System.IO;
using System.Text.Json;

namespace NeedleDrop.Spotify
{
    /// <summary>
    /// Persists just enough to skip the setup wizard on a returning launch:
    /// the app's Client ID and a Spotify refresh token, saved as plain JSON at
    /// %AppData%\NeedleDrop\settings.json.
    ///
    /// NOTE: this is NOT a secure credential store — there's no encryption,
    /// no OS credential vault, nothing beyond normal file permissions. That's
    /// an acceptable tradeoff for a class project talking to a user's own
    /// developer-dashboard app, but it's not how you'd want to store a token
    /// in anything shipped to the public.
    /// </summary>
    internal static class LocalSettings
    {
        private class SettingsFile
        {
            public string ClientId { get; set; } = "";
            public string RefreshToken { get; set; } = "";
        }

        private static string SettingsPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NeedleDrop", "settings.json");

        public static (string ClientId, string RefreshToken) Load()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return ("", "");
                var json = File.ReadAllText(SettingsPath);
                var data = JsonSerializer.Deserialize<SettingsFile>(json);
                return (data?.ClientId ?? "", data?.RefreshToken ?? "");
            }
            catch
            {
                // A corrupt or unreadable settings file just means "start fresh" — not a fatal error.
                return ("", "");
            }
        }

        public static void Save(string clientId, string refreshToken)
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath)!;
                Directory.CreateDirectory(dir);
                var data = new SettingsFile { ClientId = clientId, RefreshToken = refreshToken };
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(data));
            }
            catch
            {
                // Non-fatal: worst case the user has to click through the wizard again next time.
            }
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(SettingsPath)) File.Delete(SettingsPath);
            }
            catch
            {
                // Non-fatal.
            }
        }
    }
}
