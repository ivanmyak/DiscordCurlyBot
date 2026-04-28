using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordCurlyBot.Interfaces;
using DiscordCurlyBot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("=== ЗАПУСК СИСТЕМЫ ЗАГРУЗКИ БИБЛИОТЕК ===");

        string[] libs = { "libdave.so" };
        foreach (var lib in libs)
        {
            try
            {
                // 1. Проверяем папку запуска
                string path = Path.Combine(AppContext.BaseDirectory, lib);

                // 2. Если не нашли, проверяем вложенную папку libs (как сейчас создалось в VS)
                if (!File.Exists(path))
                    path = Path.Combine(AppContext.BaseDirectory, "libs", lib);

                // 3. Для продакшена в Docker (без VS) проверяем корень /app
                if (!File.Exists(path))
                    path = Path.Combine("/app", lib);

                var handle = NativeLibrary.Load(path);
                if (handle != IntPtr.Zero)
                    Console.WriteLine($"=== УСПЕХ: {lib} загружена! ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== ПРОВАЛ: {lib} не загружена: {ex.Message} ===");
            }
        }

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
                    EnableVoiceDaveEncryption = true,

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
        // Принудительно включаем поддержку нового протокола
        // Убедимся, что путь к папке бота стоит ПЕРВЫМ в списке поиска библиотек
        var currentLdPath = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH") ?? "";
        Environment.SetEnvironmentVariable("LD_LIBRARY_PATH", $"/app:{currentLdPath}");


        var client = host.Services.GetRequiredService<DiscordSocketClient>();
        var interactions = host.Services.GetRequiredService<InteractionService>();
        var config = host.Services.GetRequiredService<IConfiguration>();
        var voiceQueue = host.Services.GetRequiredService<IVoiceQueue>();

        // 1. АКТИВАЦИЯ СЕРВИСОВ
        host.Services.GetRequiredService<MoveUserManager>();
        host.Services.GetRequiredService<LoggingService>();

        client.Log += (msg) =>
        {
            if (msg.Exception is Discord.Net.WebSocketClosedException wsEx && wsEx.CloseCode == 4017)
            {
                Console.WriteLine("КРИТИЧЕСКАЯ ОШИБКА: Discord требует шифрование DAVE. Проверь наличие libdave.so в /usr/lib/");
            }
            return Task.CompletedTask;
        };

        // 2. СОБЫТИЕ: Вход или Перемещение
        // Для UserVoiceStateUpdated:
        client.UserVoiceStateUpdated += (user, oldState, newState) =>
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    if (user.IsBot) return;

                    if (newState.VoiceChannel != null && oldState.VoiceChannel?.Id != newState.VoiceChannel.Id)
                    {
                        var gUser = user as SocketGuildUser;
                        // Формируем фразу прямо тут
                        string text = $"Приветствуем {gUser?.Nickname ?? user.Username}";

                        // Отправляем в нашу умную очередь
                        await voiceQueue.EnqueueJoinAsync(newState.VoiceChannel, gUser?.Nickname ?? user.Username, "");
                    }
                }
                catch (Exception ex) { Console.WriteLine($"Voice Error: {ex.Message}"); }
            });
            return Task.CompletedTask;
        };

        // 3. СОБЫТИЕ: Изменение активности
        client.PresenceUpdated += (user, oldPresence, newPresence) =>
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    if (user is not SocketGuildUser gUser || gUser.IsBot) return;

                    if (gUser.VoiceChannel != null)
                    {
                        var oldGame = oldPresence?.Activities.FirstOrDefault(a => a.Type == ActivityType.Playing)?.Name;
                        var newGame = newPresence?.Activities.FirstOrDefault(a => a.Type == ActivityType.Playing)?.Name;

                        if (!string.IsNullOrEmpty(newGame) && oldGame != newGame)
                        {
                            await voiceQueue.EnqueueJoinAsync(gUser.VoiceChannel, gUser.Nickname ?? gUser.Username, newGame);
                        }
                    }
                }
                catch (Exception ex) { Console.WriteLine($"Presence Error: {ex.Message}"); }
            });
            return Task.CompletedTask;
        };

        // --- Остальной код без изменений ---
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