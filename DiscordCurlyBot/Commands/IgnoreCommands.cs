using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordCurlyBot.Services;
using Microsoft.Extensions.Logging;

public class IgnoreCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger<LoggingService> _logger;

    public IgnoreCommands(ILogger<LoggingService> logger)
    {
        _logger = logger;
    }

    [SlashCommand("ignore", "Отключает авто-перемещения для текущего пользователя или указанного (только для админов).")]
    public async Task IgnoreAsync(SocketGuildUser? target = null)
    {
        var caller = Context.User as SocketGuildUser;
        var user = target ?? caller;

        // Проверка прав: если указан чужой пользователь, то только админ может
        if (target != null && !caller.GuildPermissions.Administrator)
        {
            await RespondAsync("У вас нет прав исключать других пользователей.", ephemeral: true);
            return;
        }

        IgnoreManager.SetTracking(user.Id, false);
        await RespondAsync($"{user.DisplayName}({user.Username}) теперь не будет отслеживаться ботом.", ephemeral: true);

        // Уведомление выбранному пользователю
        if (target != null)
        {
            try
            {
                await user.SendMessageAsync(
                    $"Администратор {caller.DisplayName}({caller.Username}) сервера {caller.Guild.Name} добавил Вас в список игнорирования бота по перемещениям.");
                _logger.LogInformation($"[Ignore] {caller.DisplayName}({caller.Username}) отключил отслеживание для {user.DisplayName}({user.Username}) на сервере {caller.Guild.Name}");
            }
            catch
            {
                await FollowupAsync($"Не удалось отправить уведомление пользователю {user.DisplayName}({user.Username}) (закрыты ЛС).", ephemeral: true);
            }
        }
    }

    [SlashCommand("unignore", "Включает авто-перемещения для текущего пользователя или указанного (только для админов).")]
    public async Task UnignoreAsync(SocketGuildUser? target = null)
    {
        var caller = Context.User as SocketGuildUser;
        var user = target ?? caller;

        if (target != null && !caller.GuildPermissions.Administrator)
        {
            await RespondAsync("У вас нет прав включать других пользователей.", ephemeral: true);
            return;
        }

        IgnoreManager.SetTracking(user.Id, true);
        await RespondAsync($"{user.DisplayName}({user.Username}) снова отслеживается ботом.", ephemeral: true);

        if (target != null)
        {
            try
            {
                await user.SendMessageAsync(
                    $"Администратор {caller.DisplayName}({caller.Username}) сервера {caller.Guild.Name} исключил Вас из списка игнорирования бота по перемещениям.");
                _logger.LogInformation($"[Unignore] {caller.DisplayName}({caller.Username}) включил отслеживание для {user.DisplayName}({user.Username}) на сервере {caller.Guild.Name}");
            }
            catch
            {
                await FollowupAsync($"Не удалось отправить уведомление пользователю {user.DisplayName}({user.Username}) (закрыты ЛС).", ephemeral: true);
            }
        }
    }

    [SlashCommand("listignored", "Выводит список всех пользователей, отключивших авто-перемещения.")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task ListIgnoredAsync()
    {
        var ignored = IgnoreManager.GetIgnoredUsers().ToList();

        if (!ignored.Any())
        {
            await RespondAsync("Нет пользователей, отключивших авто-перемещения.", ephemeral: true);
            return;
        }

        string list = string.Join("\n", ignored.Select(id =>
        {
            var user = Context.Guild.GetUser(id);
            return user != null ? $"- {user.DisplayName}({user.Username})" : $"- {id}";
        }));

        await RespondAsync($"**Игнорируемые пользователи:**\n{list}", ephemeral: true);
    }
}
