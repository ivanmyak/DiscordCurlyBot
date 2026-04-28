using Discord;
using Discord.WebSocket;
using DiscordCurlyBot.Interfaces;

namespace DiscordCurlyBot.Services
{
    internal class MoveUserManager
    {
        private readonly DiscordSocketClient _client;
        private readonly IVoiceQueue _voiceQueue;

        public MoveUserManager(DiscordSocketClient client, IVoiceQueue voiceQueue)
        {
            _client = client;
            _voiceQueue = voiceQueue;

            _client.PresenceUpdated += OnPresenceUpdatedAsync;
        }

        private async Task OnPresenceUpdatedAsync(SocketUser user, SocketPresence before, SocketPresence after)
        {
            if (user is not SocketGuildUser guildUser) return;
            if (guildUser.VoiceChannel == null || IgnoreManager.IsIgnored(guildUser.Id)) return;

            var currentActivity = after?.Activities
                .FirstOrDefault(a => a.Type == ActivityType.Playing);
            var previousActivity = before?.Activities
                .FirstOrDefault(a => a.Type == ActivityType.Playing);

            var guild = guildUser.Guild;

            var huntChannel = guild.VoiceChannels.FirstOrDefault(c => c.Name.Equals("Охота", StringComparison.OrdinalIgnoreCase))
                              ?? throw new Exception("Не найден голосовой канал 'Охота'");
            var otherGamesChannel = guild.VoiceChannels.FirstOrDefault(c => c.Name.Equals("Иные игрульки", StringComparison.OrdinalIgnoreCase))
                                    ?? throw new Exception("Не найден голосовой канал 'Иные игрульки'");
            var idleChannel = guild.VoiceChannels.FirstOrDefault(c => c.Name.Equals("Житьё-Бытьё", StringComparison.OrdinalIgnoreCase))
                              ?? throw new Exception("Не найден голосовой канал 'Житьё-Бытьё'");

            if (guildUser.VoiceChannel == idleChannel && currentActivity != null)
            {
                if (string.Equals(currentActivity.Name, "Hunt: Showdown", StringComparison.OrdinalIgnoreCase))
                {
                    await MoveUserAsync(guildUser, huntChannel,
                        $"Вы запустили {currentActivity.Name}, поэтому были перемещены в {huntChannel.Name}.");
                    await _voiceQueue.EnqueueJoinAsync(huntChannel, guildUser.Nickname ?? guildUser.Username, currentActivity.Name);
                }
                else
                {
                    await MoveUserAsync(guildUser, otherGamesChannel,
                        $"Вы запустили {currentActivity.Name}, поэтому были перемещены в {otherGamesChannel.Name}.");
                    await _voiceQueue.EnqueueJoinAsync(otherGamesChannel, guildUser.Nickname ?? guildUser.Username, currentActivity.Name);
                }
            }
            else if (guildUser.VoiceChannel == otherGamesChannel &&
                     string.Equals(currentActivity?.Name, "Hunt: Showdown", StringComparison.OrdinalIgnoreCase))
            {
                await MoveUserAsync(guildUser, huntChannel,
                    $"Вы запустили {currentActivity?.Name ?? "Не распознали"}, поэтому были перемещены в {huntChannel.Name}.");
                await _voiceQueue.EnqueueJoinAsync(huntChannel, guildUser.Nickname ?? guildUser.Username, currentActivity?.Name ?? "");
            }
            else if (guildUser.VoiceChannel == otherGamesChannel && currentActivity == null && previousActivity != null)
            {
                await MoveUserAsync(guildUser, idleChannel,
                    $"Вы завершили {previousActivity.Name}, поэтому были перемещены в {idleChannel.Name}.");
                await _voiceQueue.EnqueueJoinAsync(idleChannel, guildUser.Nickname ?? guildUser.Username, "");
            }
            else if (guildUser.VoiceChannel == huntChannel && currentActivity == null && previousActivity != null)
            {
                await MoveUserAsync(guildUser, idleChannel,
                    $"Вы завершили {previousActivity.Name}, поэтому были перемещены в {idleChannel.Name}");
                await _voiceQueue.EnqueueJoinAsync(idleChannel, guildUser.Nickname ?? guildUser.Username, "");
            }
        }

        private async Task MoveUserAsync(SocketGuildUser user, SocketVoiceChannel targetChannel, string message)
        {
            if (targetChannel == null || user.VoiceChannel == targetChannel) return;
            if (user.Guild.CurrentUser == null || !user.Guild.CurrentUser.GuildPermissions.MoveMembers) return;

            await user.ModifyAsync(x => x.Channel = targetChannel);

            try
            {
                await user.SendMessageAsync(message + "\nДля отключения авто-перемещений используйте команду `/ignore`.");
            }
            catch
            {
                var mainChannel = user.Guild.TextChannels.FirstOrDefault(c => c.Name.Equals("основная-основа-основ", StringComparison.OrdinalIgnoreCase));
                if (mainChannel != null)
                    await mainChannel.SendMessageAsync($"{user.Mention}: {message}\nДля отключения авто-перемещений используйте команду `/ignore`.");
            }
        }
    }
}
