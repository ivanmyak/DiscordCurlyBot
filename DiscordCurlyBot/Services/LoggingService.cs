using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DiscordCurlyBot.Services
{
    public class LoggingService
    {
        private readonly ILogger<LoggingService> _logger;
        private readonly DiscordSocketClient _client;

        public LoggingService(ILogger<LoggingService> logger, DiscordSocketClient client)
        {
            _logger = logger;
            _client = client;

            // Подписка на события
            _client.MessageReceived += OnBotMessageReceivedAsync;
            _client.InteractionCreated += OnInteractionCreatedAsync;
            _client.UserJoined += OnUserJoinedAsync;
            _client.UserLeft += OnUserLeftAsync;
            _client.PresenceUpdated += OnPresenceUpdatedAsync;
            _logger.LogInformation("Зарегистрировали Лог-сервис!");
        }

        /// <summary>
        /// Логирует сообщения, адресованные боту (личные сообщения или упоминания).
        /// </summary>
        private Task OnBotMessageReceivedAsync(SocketMessage message)
        {
            if (message.Channel is SocketDMChannel)
            {
                _logger.LogInformation($"[BotMessage] {message.Author.GlobalName}({message.Author.Username}) написал боту в личку: {message.Content}");
            }

            return Task.CompletedTask;
        }

        private Task OnInteractionCreatedAsync(SocketInteraction interaction)
        {
            string commandInfo = interaction switch
            {
                SocketSlashCommand slash => $"/{slash.CommandName}",
                SocketMessageComponent component => $"Component {component.Data.CustomId}",
                SocketUserCommand userCmd => $"UserCommand {userCmd.CommandName}",
                SocketMessageCommand msgCmd => $"MessageCommand {msgCmd.CommandName}",
                _ => "Не удалось определить...."
            };

            _logger.LogInformation($"[Interaction] {interaction.User.GlobalName}({interaction.User.Username}) вызвал {commandInfo} в #{interaction.Channel?.Name}");
            return Task.CompletedTask;
        }

        private Task OnUserJoinedAsync(SocketGuildUser user)
        {
            _logger.LogInformation($"[Join] {user.DisplayName}({user.Username}) присоединился к серверу {user.Guild.Name}");
            return Task.CompletedTask;
        }

        private Task OnUserLeftAsync(SocketGuild guild, SocketUser user)
        {
            _logger.LogWarning($"[Leave] {user.GlobalName}({user.Username}) покинул сервер {guild.Name}");
            return Task.CompletedTask;
        }

        private Task OnPresenceUpdatedAsync(SocketUser user, SocketPresence before, SocketPresence after)
        {
            var beforeActivities = before?.Activities ?? Enumerable.Empty<IActivity>();
            var afterActivities = after?.Activities ?? Enumerable.Empty<IActivity>();

            var beforeActivity = beforeActivities.FirstOrDefault();
            var afterActivity = afterActivities.FirstOrDefault();


            if (beforeActivity?.Name != afterActivity?.Name)
            {
                if (afterActivity != null)
                    _logger.LogInformation($"[Activity] {user.GlobalName}({user.Username}) начал {afterActivity.Name}");
                else if (beforeActivity != null)
                    _logger.LogInformation($"[Activity] {user.GlobalName}({user.Username}) завершил {beforeActivity.Name}");
            }
            else
            {

                _logger.LogInformation($"[Activity] У {user.GlobalName}({user.Username}) не удалось оценить состояние прошлой:{beforeActivity?.Name} и будущей: {afterActivity?.Name}");
            }

            return Task.CompletedTask;
        }

    }
}
