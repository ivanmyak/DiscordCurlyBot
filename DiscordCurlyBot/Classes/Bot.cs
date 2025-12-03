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
        private ServiceProvider? _serviceProvider;

        private readonly ILogger<Bot> _logger;
        private readonly IConfiguration _configuration;
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;

        public Bot(ILogger<Bot> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            DiscordSocketConfig socketConfig = new()
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
            };

            _client = new DiscordSocketClient(socketConfig);
            _commands = new CommandService();
        }

        public async Task StartAsync(ServiceProvider serviceProvider)
        {
            string discordToken = _configuration["DiscordToken"] ?? throw new Exception("Missing Discord Token!");
            _logger.LogInformation($"Starting with token {discordToken}");

            await _commands.AddModulesAsync(Assembly.GetExecutingAssembly(), _serviceProvider);

            await _client.LoginAsync(TokenType.Bot, discordToken);
            await _client.StartAsync();

            _client.MessageReceived += HandleCommandAsync;
        }

        public Task StopASync()
        {
            throw new NotImplementedException();
        }

        private async Task HandleCommandAsync(SocketMessage arg)
        {
            if (arg is not SocketUserMessage message || message.Author.IsBot)
            {
                return;
            }

            _logger.LogInformation($"{DateTime.Now.ToShortTimeString()} = {message.Author}: {message.Content}");


            int position = 0;
            bool messageIsCommand = message.HasCharPrefix('!', ref position);

            if (messageIsCommand)
            {
                await _commands.ExecuteAsync(
                    new SocketCommandContext(_client, message),
                    position,
                    _serviceProvider);
            }
        }
    }
}
