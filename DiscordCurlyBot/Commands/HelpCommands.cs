using Discord;
using Discord.Commands;
using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordCurlyBot.Commands
{
    public class HelpCommands : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly InteractionService _interactions;

        public HelpCommands(InteractionService interactions)
        {
            _interactions = interactions;
        }

        [SlashCommand("help", "Выводит список всех доступных slash-команд.")]
        public async Task HelpAsync()
        {
            var embedBuilder = new EmbedBuilder()
                .WithTitle("📜 Список команд")
                .WithColor(Color.DarkBlue)
                .WithDescription("Все доступные команды бота:");

            foreach (var cmd in _interactions.Modules.SelectMany(m => m.SlashCommands))
            {
                string summary = cmd.Description ?? "Нет описания";
                embedBuilder.AddField($"/{cmd.Name}", summary, inline: false);
            }

            await RespondAsync(embed: embedBuilder.Build(), ephemeral: true);
        }
    }
}
