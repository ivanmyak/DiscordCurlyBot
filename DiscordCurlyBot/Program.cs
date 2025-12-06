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

        var serviceProvider = new ServiceCollection()
            .AddLogging(options =>
            {
                options.ClearProviders();
                options.AddConsole();
            })
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton<DiscordSocketClient>()
            .AddSingleton(x =>
            {
                var client = x.GetRequiredService<DiscordSocketClient>();
                return new InteractionService(client);
            })
            .AddSingleton<MoveUserManager>()
            .BuildServiceProvider();

        serviceProvider.GetRequiredService<MoveUserManager>();

        try
        {
            var client = serviceProvider.GetRequiredService<DiscordSocketClient>();
            var interactions = serviceProvider.GetRequiredService<InteractionService>();

            client.Log += msg => { Console.WriteLine(msg.ToString()); return Task.CompletedTask; };

            client.Ready += async () =>
            {
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

            await Task.Delay(-1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal ERROR: {ex.Message}");
            Environment.Exit(-1);
        }
    }
}
