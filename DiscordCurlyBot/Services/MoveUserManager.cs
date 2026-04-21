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

        /// <summary>
        /// Обработчик изменения присутствия пользователя.
        /// Отслеживает начало и окончание игровых активностей и перемещает пользователя между каналами.
        /// </summary>
        private async Task OnPresenceUpdatedAsync(SocketUser user, SocketPresence before, SocketPresence after)
        {
            if (user is not SocketGuildUser guildUser) return;
            if (guildUser.VoiceChannel == null || IgnoreManager.IsIgnored(guildUser.Id)) return;
            // не в голосовом канале или отключил отслеживание

            IActivity currentActivity = after?.Activities.First() ?? throw new Exception("Не удалось распознать текущую активность");
            var previousActivity = before?.Activities.FirstOrDefault();

            var guild = guildUser.Guild;
            var huntChannel = guild.VoiceChannels.FirstOrDefault(c => c.Name.Equals("Охота", StringComparison.OrdinalIgnoreCase)) ?? throw new Exception("Не найден голосовой канал 'Охота'");
            var otherGamesChannel = guild.VoiceChannels.FirstOrDefault(c => c.Name.Equals("Иные игрульки", StringComparison.OrdinalIgnoreCase)) ?? throw new Exception("Не найден голосовой канал 'Иные игрульки'");
            var idleChannel = guild.VoiceChannels.FirstOrDefault(c => c.Name.Equals("Житьё-Бытьё", StringComparison.OrdinalIgnoreCase)) ?? throw new Exception("Не найден голосовой канал 'Житьё-Бытьё'");

            // --- Логика перемещений ---

            // 1. Пользователь сидит в "Житьё-Бытьё" и запускает игру
            if (guildUser.VoiceChannel == idleChannel && currentActivity != null && currentActivity.Type == ActivityType.Playing)
            {
                if (string.Equals(currentActivity.Name, "Hunt: Showdown", StringComparison.OrdinalIgnoreCase))
                {
                    await MoveUserAsync(guildUser, huntChannel,
                        $"Вы запустили {currentActivity.Name}, поэтому были перемещены в {huntChannel?.Name}.");
                }
                else
                {
                    await MoveUserAsync(guildUser, otherGamesChannel,
                        $"Вы запустили {currentActivity.Name}, поэтому были перемещены в {otherGamesChannel?.Name}.");
                }
            }

            // 2. Пользователь сидит в "Иные игрульки" и запускает Hunt: Showdown
            else if (guildUser.VoiceChannel == otherGamesChannel &&
                     string.Equals(currentActivity?.Name, "Hunt: Showdown", StringComparison.OrdinalIgnoreCase))
            {
                await MoveUserAsync(guildUser, huntChannel,
                    $"Вы запустили {currentActivity?.Name ?? "Не распознали"}, поэтому были перемещены в {huntChannel?.Name}.");
            }

            // 3. Пользователь сидит в "Иные игрульки" и завершает игру
            else if (guildUser.VoiceChannel == otherGamesChannel && currentActivity == null && previousActivity != null)
            {
                await MoveUserAsync(guildUser, idleChannel,
                    $"Вы завершили {previousActivity.Name}, поэтому были перемещены в {idleChannel?.Name}.");
            }

            // 4. Пользователь сидит в "Охота" и завершает игру
            else if (guildUser.VoiceChannel == huntChannel && currentActivity == null && previousActivity != null)
            {
                await MoveUserAsync(guildUser, idleChannel,
                    $"Вы завершили {previousActivity.Name}, поэтому будете перемещены в {idleChannel?.Name}");
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
