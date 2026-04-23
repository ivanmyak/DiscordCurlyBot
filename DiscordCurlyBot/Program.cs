using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordCurlyBot.Interfaces;
using DiscordCurlyBot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // Используем Host — это стандарт .NET 6/7/8+
        // Он под капотом настроит IConfiguration, ILogging и IHostEnvironment
        using IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: true);
                config.AddUserSecrets(Assembly.GetExecutingAssembly());
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                // Конфигурация Discord Client
                var socketConfig = new DiscordSocketConfig
                {
                    GatewayIntents = GatewayIntents.Guilds |
                                     GatewayIntents.GuildMembers |
                                     GatewayIntents.GuildVoiceStates |
                                     GatewayIntents.GuildPresences
                };

                // Регистрация сервисов
                services.AddSingleton(socketConfig);
                services.AddSingleton<DiscordSocketClient>();
                services.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()));

                // Перемещение и Логи
                services.AddSingleton<MoveUserManager>();
                services.AddSingleton<LoggingService>();

                // Озвучка, Перевод, Очередь озвучки
                services.AddHttpClient<IVoice, VoiceService>();
                services.AddSingleton<ITranslate, TranslationService>();
                services.AddSingleton<IVoiceQueue, VoiceQueueService>();
            })
            .Build();

        await RunBotAsync(host);
    }

    private static async Task RunBotAsync(IHost host)
    {
        var client = host.Services.GetRequiredService<DiscordSocketClient>();
        var interactions = host.Services.GetRequiredService<InteractionService>();
        var config = host.Services.GetRequiredService<IConfiguration>();
        var voiceQueue = host.Services.GetRequiredService<IVoiceQueue>();

        // Инициализация сервисов (подписки на события внутри них)
        host.Services.GetRequiredService<MoveUserManager>();
        host.Services.GetRequiredService<LoggingService>();

        // ПОДПИСКА НА СОБЫТИЕ ГОЛОСА (Твоя новая фича)
        client.UserVoiceStateUpdated += async (user, oldState, newState) =>
        {
            // Логика: человек зашел в канал, бот не реагирует на ботов
            if (oldState.VoiceChannel == null && newState.VoiceChannel != null && !user.IsBot)
            {
                var gUser = user as SocketGuildUser;
                var activity = user.Activities.FirstOrDefault(a => a.Type == ActivityType.Playing)?.Name ?? "";

                // Отправляем в нашу умную очередь
                await voiceQueue.EnqueueJoinAsync(newState.VoiceChannel, gUser?.Nickname ?? user.Username, activity);
            }
        };

        // Стандартные события Discord
        client.Log += msg => { Console.WriteLine(msg); return Task.CompletedTask; };

        client.Ready += async () =>
        {
            await interactions.AddModulesAsync(Assembly.GetEntryAssembly(), host.Services);
            await interactions.RegisterCommandsGloballyAsync();
        };

        client.InteractionCreated += async (interaction) =>
        {
            var ctx = new SocketInteractionContext(client, interaction);
            await interactions.ExecuteCommandAsync(ctx, host.Services);
        };

        string token = config["DiscordToken"] ?? throw new Exception("Missing Discord Token!");

        await client.LoginAsync(TokenType.Bot, token);
        await client.StartAsync();

        await host.RunAsync();
    }
}