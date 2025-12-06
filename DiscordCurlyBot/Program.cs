using System.Reflection;
using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DiscordCurlyBot.Services;

internal class Program
{
    private static void Main(string[] args) => MainAsync(args).GetAwaiter().GetResult();

    private static async Task MainAsync(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(Assembly.GetExecutingAssembly())
            .AddEnvironmentVariables()
            .Build();

        // Конфиг клиента с нужными intent'ами
        var socketConfig = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds |
                             GatewayIntents.GuildMembers |
                             GatewayIntents.GuildVoiceStates |
                             GatewayIntents.GuildPresences
        };

        // Создаём клиент один раз
        var client = new DiscordSocketClient(socketConfig);

        var serviceProvider = new ServiceCollection()
            .AddLogging(options =>
            {
                options.ClearProviders();
                options.AddConsole();
            })
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton(client) // регистрируем именно этот экземпляр
            .AddSingleton(x => new InteractionService(client))
            .AddSingleton<MoveUserManager>() // сервис перемещений
            .BuildServiceProvider();

        // Инициализация MoveUserManager (подписка на PresenceUpdated)
        serviceProvider.GetRequiredService<MoveUserManager>();

        try
        {
            var interactions = serviceProvider.GetRequiredService<InteractionService>();

            client.Log += msg =>
            {
                Console.WriteLine(msg.ToString());
                return Task.CompletedTask;
            };

            client.Ready += async () =>
            {
                // Регистрируем все модули команд
                await interactions.AddModulesAsync(Assembly.GetEntryAssembly(), serviceProvider);
                await interactions.RegisterCommandsGloballyAsync();
                Console.WriteLine("Slash-команды зарегистрированы!");
            };

            client.InteractionCreated += async (interaction) =>
            {
                var ctx = new SocketInteractionContext(client, interaction);
                await interactions.ExecuteCommandAsync(ctx, serviceProvider);
            };

            string token = configuration["DiscordToken"]
                ?? Environment.GetEnvironmentVariable("DiscordToken")
                ?? throw new Exception("Missing Discord Token!");

            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();

            await Task.Delay(-1); // держим процесс живым
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal ERROR: {ex.Message}");
            Environment.Exit(-1);
        }
    }
}
