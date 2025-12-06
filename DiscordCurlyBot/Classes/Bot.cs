using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordCurlyBot.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace DiscordCurlyBot.Classes
{
    internal class Bot : IBot
    {
        private readonly ILogger<Bot> _logger;
        private readonly IConfiguration _configuration;
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly IServiceProvider _services;

        public Bot(ILogger<Bot> logger, IConfiguration configuration, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _configuration = configuration;


            DiscordSocketConfig socketConfig = new()
            {
                AlwaysDownloadUsers = true,
                GatewayIntents = GatewayIntents.All
            };
            _client = new DiscordSocketClient(socketConfig);
            _commands = new CommandService();
            _services = serviceProvider;

            _client.Log += msg => { _logger.LogInformation(msg.ToString()); return Task.CompletedTask; };
            _client.MessageReceived += HandleCommandAsync;
            _client.PresenceUpdated += PresenceUpdatedAsync;
        }

        public async Task StartAsync()
        {
            //Достаём переменную изначально из UserSecret-ов, а при неудаче - пытаемся получить из Преременных среды (для поднятия в Docker)
            string discordToken = _configuration["DiscordToken"]
                ?? Environment.GetEnvironmentVariable("DiscordToken")
                ?? throw new Exception("Missing Discord Token!");

            _logger.LogInformation($"Starting Bot...");

            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
            await _client.LoginAsync(TokenType.Bot, discordToken);
            await _client.StartAsync();
        }

        public async Task StopASync()
        {
            _logger.LogInformation("Shutting down bot");

            if (_client != null)
            {
                await _client.LogoutAsync();
                await _client.StopAsync();
            }
        }

        private async Task HandleCommandAsync(SocketMessage arg)
        {
            if (arg is not SocketUserMessage message || message.Author.IsBot) return;

            int pos = 0;
            if (message.HasCharPrefix('!', ref pos))
            {
                var context = new SocketCommandContext(_client, message);
                await _commands.ExecuteAsync(context, pos, _services);
            }
        }

        // <summary>
        // Здесь можно проверять activity.Name == "Hunt: Showdown"
        // и перемещать пользователя в нужный канал.
        // </summary>
        private async Task PresenceUpdatedAsync(SocketUser user, SocketPresence before, SocketPresence after)
        {
            var activity = after.Activities.FirstOrDefault(a => a is Game) as Game;
            if (activity == null) return;

            if (activity.Name.Contains("Hunt"))
            {
                // Логика перемещения в канал Ханта
            }
            else
            {
                // Логика перемещения в канал других игр
            }
        }

    }
}
