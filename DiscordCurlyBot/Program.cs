using System.Reflection;

using Discord;
using Discord.WebSocket;
using DiscordCurlyBot.Classes;
using DiscordCurlyBot.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;



internal class Program
{
    private static DiscordSocketClient _client;

    private static void Main(string[] args) => MainAsync(args).GetAwaiter().GetResult();

    private static async Task MainAsync(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(Assembly.GetExecutingAssembly())
            .Build();

        var serviceProvider = new ServiceCollection()
            .AddLogging(options =>
            {
                options.ClearProviders();
                options.AddConsole();
            })
            .AddSingleton<IConfiguration>(configuration)
            .AddScoped<IBot, Bot>()
            .BuildServiceProvider();

        try
        {
            IBot bot = serviceProvider.GetRequiredService<IBot>();
            await bot.StartAsync(serviceProvider);

            while (true)
            {
                var keyInfo = Console.ReadKey();

                if (keyInfo.Key == ConsoleKey.Q)
                {
                    await bot.StopASync();
                    return;
                }
            }
        }
        catch (Exception exeption)
        {
            Console.WriteLine(exeption.Message);
            Environment.Exit(-1);
        }

    }

}