using System;
using System.IO;
using Sanguo.Data;
using Sanguo.GameModes;

namespace Sanguo.Save
{
    /// <summary>
    /// Locally saved player data: identity, audio/quality settings, last server and last room setup.
    /// Stored as JSON under the platform's persistent data path; future account/cloud saves can sync
    /// the same document.
    /// </summary>
    public sealed class PlayerProfile
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public string Nickname = "玩家";
        public int AvatarId;
        public float MusicVolume = 0.6f;
        public float SfxVolume = 0.8f;
        /// <summary>0 = low, 1 = medium, 2 = high.</summary>
        public int Quality = 1;
        public string LastServerAddress = string.Empty;
        public string LastInviteCode = string.Empty;
        public GameModeConfig LastRoomConfig;
        /// <summary>Stable random id of this install (lets the host recognise returning devices).</summary>
        public string DeviceId = string.Empty;

        public JsonValue ToJson()
        {
            var j = JsonValue.NewObject()
                .Set("version", Version)
                .Set("nickname", Nickname)
                .Set("avatarId", AvatarId)
                .Set("musicVolume", MusicVolume)
                .Set("sfxVolume", SfxVolume)
                .Set("quality", Quality)
                .Set("lastServerAddress", LastServerAddress)
                .Set("lastInviteCode", LastInviteCode)
                .Set("deviceId", DeviceId);
            if (LastRoomConfig != null) j.Set("lastRoomConfig", LastRoomConfig.ToJson());
            return j;
        }

        public static PlayerProfile FromJson(JsonValue j)
        {
            var p = new PlayerProfile
            {
                Version = j.GetInt("version", CurrentVersion),
                Nickname = Sanitize(j.GetString("nickname"), "玩家"),
                AvatarId = Math.Max(0, j.GetInt("avatarId")),
                MusicVolume = Clamp01((float)j.GetDouble("musicVolume", 0.6)),
                SfxVolume = Clamp01((float)j.GetDouble("sfxVolume", 0.8)),
                Quality = Math.Max(0, Math.Min(2, j.GetInt("quality", 1))),
                LastServerAddress = j.GetString("lastServerAddress", string.Empty),
                LastInviteCode = j.GetString("lastInviteCode", string.Empty),
                DeviceId = j.GetString("deviceId", string.Empty)
            };
            if (j["lastRoomConfig"].IsObject) p.LastRoomConfig = GameModeConfig.FromJson(j["lastRoomConfig"]);
            return p;
        }

        private static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;

        private static string Sanitize(string s, string fallback)
        {
            s = (s ?? string.Empty).Trim();
            if (s.Length > 16) s = s.Substring(0, 16);
            return s.Length == 0 ? fallback : s;
        }
    }

    /// <summary>Loads and saves the profile atomically; a corrupt or missing file yields defaults.</summary>
    public static class ProfileStore
    {
        public const string FileName = "profile.json";

        public static PlayerProfile Load(string directory)
        {
            string path = Path.Combine(directory, FileName);
            try
            {
                if (File.Exists(path)) return EnsureDeviceId(PlayerProfile.FromJson(JsonValue.Parse(File.ReadAllText(path))));
            }
            catch (Exception ex) when (ex is IOException || ex is FormatException || ex is UnauthorizedAccessException)
            {
                // Fall back to defaults; the next save overwrites the broken file.
            }
            return EnsureDeviceId(new PlayerProfile());
        }

        public static void Save(string directory, PlayerProfile profile)
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, FileName);
            string temp = path + ".tmp";
            File.WriteAllText(temp, profile.ToJson().ToJson(pretty: true));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }

        private static PlayerProfile EnsureDeviceId(PlayerProfile p)
        {
            if (string.IsNullOrEmpty(p.DeviceId)) p.DeviceId = Guid.NewGuid().ToString("N");
            return p;
        }
    }
}
