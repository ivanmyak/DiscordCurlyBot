using Discord;
using Discord.WebSocket;

namespace DiscordCurlyBot.Services
{
    public class MoveUserManager
    {
        private readonly DiscordSocketClient _client;

        public MoveUserManager(DiscordSocketClient client)
        {
            _client = client;

            // Подписываемся на событие обновления присутствия
            _client.PresenceUpdated += OnPresenceUpdatedAsync;
        }

        private async Task OnPresenceUpdatedAsync(SocketUser user, SocketPresence before, SocketPresence after)
        {
            try
            {
                if (user is not SocketGuildUser guildUser) return;

                // Игнорируем, если пользователь отключил авто-перемещения
                if (IgnoreManager.IsIgnored(guildUser.Id)) return;

                var activity = after.Activities.FirstOrDefault();
                if (activity == null) return;

                // Пример: игра Hunt: Showdown → канал "Hunt"
                if (activity.Type == ActivityType.Playing && activity.Name == "Hunt: Showdown")
                {
                    var huntChannel = guildUser.Guild.VoiceChannels
                        .FirstOrDefault(c => c.Name.Equals("Hunt", StringComparison.OrdinalIgnoreCase));

                    if (huntChannel != null && guildUser.VoiceChannel != huntChannel)
                    {
                        await guildUser.ModifyAsync(x => x.Channel = huntChannel);
                        Console.WriteLine($"{guildUser.Username} перемещён в {huntChannel.Name}");
                    }
                }

                // Здесь можно добавить другие правила для разных игр/активностей
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в MoveUserManager: {ex.Message}");
            }
        }
    }
}
