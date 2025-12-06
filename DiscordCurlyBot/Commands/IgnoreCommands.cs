using Discord;
using Discord.Interactions;
using DiscordCurlyBot.Services;
using Microsoft.VisualBasic;
using System;
using System.Linq;

public class IgnoreCommands : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("ignore", "Отключает авто-перемещения для текущего пользователя.")]
    public async Task IgnoreAsync()
    {
        IgnoreManager.SetTracking(Context.User.Id, false);
        await RespondAsync($"{Context.User.Username}, теперь бот не будет вас отслеживать.", ephemeral: true);
    }

    [SlashCommand("unignore", "Включает авто-перемещения для текущего пользователя.")]
    public async Task UnignoreAsync()
    {
        IgnoreManager.SetTracking(Context.User.Id, true);
        await RespondAsync($"{Context.User.Username}, отслеживание снова включено.", ephemeral: true);
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
            return user != null ? $"- {user.Username}" : $"- {id}";
        }));

        await RespondAsync($"**Игнорируемые пользователи:**\n{list}", ephemeral: true);
    }
}
