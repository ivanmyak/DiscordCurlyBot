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
            if (user is not SocketGuildUser guildUser) return;
            if (guildUser.VoiceChannel == null) return; // не в голосовом канале
            if (IgnoreManager.IsIgnored(guildUser.Id)) return; // отключил отслеживание

            var activity = after.Activities.FirstOrDefault();
            var beforeActivity = before?.Activities.FirstOrDefault();

            var guild = guildUser.Guild;
            var huntChannel = guild.VoiceChannels.FirstOrDefault(c => c.Name.Equals("Охота", StringComparison.OrdinalIgnoreCase));
            var otherGamesChannel = guild.VoiceChannels.FirstOrDefault(c => c.Name.Equals("Иные игрульки", StringComparison.OrdinalIgnoreCase));
            var idleChannel = guild.VoiceChannels.FirstOrDefault(c => c.Name.Equals("Житьё-Бытьё", StringComparison.OrdinalIgnoreCase));

            // Логика перемещений
            if (guildUser.VoiceChannel == idleChannel && activity != null)
            {
                if (activity.Type == ActivityType.Playing && activity.Name == "Hunt: Showdown")
                    await MoveUserAsync(guildUser, huntChannel, $"Вы запустили {activity.Name}, поэтому были перемещены в {huntChannel.Name}.");
                else if (activity.Type == ActivityType.Playing)
                    await MoveUserAsync(guildUser, otherGamesChannel, $"Вы запустили {activity.Name}, поэтому были перемещены в {otherGamesChannel.Name}.");
            }
            else if (guildUser.VoiceChannel == otherGamesChannel && activity?.Name == "Hunt: Showdown")
            {
                await MoveUserAsync(guildUser, huntChannel, $"Вы запустили {activity.Name}, поэтому были перемещены в {huntChannel.Name}.");
            }
            else if (guildUser.VoiceChannel == huntChannel && activity == null && beforeActivity != null)
            {
                await MoveUserAsync(guildUser, idleChannel, $"Вы завершили {beforeActivity.Name}, поэтому были перемещены в {idleChannel.Name}.");
            }
        }

        private async Task MoveUserAsync(SocketGuildUser user, SocketVoiceChannel targetChannel, string message)
        {
            if (targetChannel == null || user.VoiceChannel == targetChannel) return;

            await user.ModifyAsync(x => x.Channel = targetChannel);

            // Отправляем уведомление в личку
            try
            {
                await user.SendMessageAsync(message + "\nДля отключения авто-перемещений используйте команду `/ignore`.");
            }
            catch
            {
                // Если личка закрыта, пишем в главный текстовый канал
                var mainChannel = user.Guild.TextChannels.FirstOrDefault(c => c.Name.Equals("основная-основа-основ", StringComparison.OrdinalIgnoreCase));
                if (mainChannel != null)
                    await mainChannel.SendMessageAsync($"{user.Mention}: {message}\nДля отключения авто-перемещений используйте команду `/ignore`.");
            }
        }
    }
}
