using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DiscordCurlyBot.Services
{
    public static class IgnoreManager
    {
        private static readonly string FilePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "tracking.json");

        private static Dictionary<ulong, bool> tracking = new();
        private static readonly object _lock = new();

        static IgnoreManager()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    tracking = JsonSerializer.Deserialize<Dictionary<ulong, bool>>(json) ?? new();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки tracking.json: {ex.Message}");
                tracking = new();
            }
        }

        public static bool IsIgnored(ulong userId)
        {
            if (!tracking.ContainsKey(userId)) return false; // по умолчанию отслеживаем
            return !tracking[userId];
        }

        public static void EnsureUser(ulong userId)
        {
            lock (_lock)
            {
                if (!tracking.ContainsKey(userId))
                {
                    tracking[userId] = true;
                    Save();
                }
            }
        }

        public static void SetTracking(ulong userId, bool track)
        {
            lock (_lock)
            {
                tracking[userId] = track;
                Save();
            }
        }

        public static void RemoveUser(ulong userId)
        {
            lock (_lock)
            {
                tracking.Remove(userId);
                Save();
            }
        }

        public static IEnumerable<ulong> GetIgnoredUsers()
            => tracking.Where(kvp => !kvp.Value).Select(kvp => kvp.Key);

        private static void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(tracking, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка сохранения tracking.json: {ex.Message}");
            }
        }
    }

}
