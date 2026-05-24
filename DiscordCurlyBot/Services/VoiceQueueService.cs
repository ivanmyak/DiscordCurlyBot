using Discord;
using Discord.Audio;
using Discord.WebSocket;
using DiscordCurlyBot.Classes;
using DiscordCurlyBot.Interfaces;
using GTranslate.Translators;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordCurlyBot.Services
{
    internal class VoiceQueueService : IVoiceQueue
    {
        private readonly ITTSGenerator _tts;
        private readonly ITranslate _translator;
        private readonly IVoiceTemplate _templates;
        private readonly DiscordSocketClient _client;
        private readonly ILogger<VoiceQueueService> _logger;

        private readonly ConcurrentQueue<VoiceQueueItem> _queue = new();
        private readonly SemaphoreSlim _signal = new(0);

        private readonly ConcurrentDictionary<ulong, (IAudioClient Client, ulong ChannelId)> _connections = new();

        private readonly int _delayBetweenMessagesMs;

        public VoiceQueueService(
            IConfiguration config,
            ITTSGenerator tts,
            DiscordSocketClient client,
            ITranslate translator,
            IVoiceTemplate templates,
            ILogger<VoiceQueueService> logger)
        {
            _tts = tts;
            _client = client;
            _translator = translator;
            _templates = templates;
            _logger = logger;

            var DebounceDelayMillisecondsStr = config["VoiceSettings:DebounceDelayMilliseconds"];

            if (!int.TryParse(DebounceDelayMillisecondsStr, out _delayBetweenMessagesMs) || _delayBetweenMessagesMs <= 0)
            {
                _delayBetweenMessagesMs = 200;
                _logger.LogWarning("[VOICEINIT] VoiceSettings:DebounceDelayMilliseconds отсутствует или некорректен. Используем значение по умолчанию: 200");
            }
            else
            {
                _logger.LogInformation("[VOICEINIT] MaxTextLength загружен из конфигурации: {Value}", _delayBetweenMessagesMs);
            }

            Task.Run(ProcessQueueAsync);
        }

        // ================================
        //  ПРИВЕТСТВИЯ
        // ================================
        public async Task EnqueueJoinAsync(IVoiceChannel channel, string userName, string activity)
        {
            _logger.LogInformation("[VOICEJOIN] EnqueueJoinAsync: channel={Channel}, user={User}, activity={Activity}",
                channel.Name, userName, activity ?? "<null>");

            string text;

            if (string.IsNullOrEmpty(activity))
            {
                text = _templates.FormatSingle(userName);
            }
            else
            {
                var ruAct = await _translator.GetRussianActivityAsync(activity);
                text = _templates.FormatSingleWithActivity(userName, ruAct);
            }

            _queue.Enqueue(new VoiceQueueItem
            {
                Channel = channel,
                Text = text
            });

            _logger.LogInformation("[VOICEJOIN] В очередь добавлен Join: Channel={Channel}, Text=\"{Text}\"",
                channel.Name, text);

            _signal.Release();
        }

        // ================================
        //  /TALK и DM
        // ================================
        public async Task EnqueueDirectMessageAsync(
    SocketUser sender,
    string text,
    bool forceDM = false,
    SocketUser? recipient = null)
        {
            _logger.LogInformation(
                "[VOICEDMQUEUE] sender={Sender}({SenderId}), recipient={Recipient}, forceDM={Force}, rawText=\"{Text}\"",
                sender.Username, sender.Id,
                recipient?.Username ?? "<null>",
                forceDM,
                text
            );

            var formatted = _templates.FormatTalk(text);

            // Если recipient не указан — отправляем самому себе
            recipient ??= sender;
            bool isSelf = recipient.Id == sender.Id;

            // === 1. Попытка в голосовой канал (если не forceDM) ===
            SocketGuildUser? recipientGuildUser = recipient as SocketGuildUser;
            if (!forceDM && recipientGuildUser?.VoiceChannel != null)
            {
                // Озвучка в голосовом канале
                _queue.Enqueue(new VoiceQueueItem
                {
                    Channel = recipientGuildUser.VoiceChannel,
                    Text = formatted,
                    Sender = sender,
                    Recipient = recipient
                });

                // DM отправителю (копия) — даже если он тот же самый, текст будет «самому себе»
                _queue.Enqueue(new VoiceQueueItem
                {
                    DmUser = sender,
                    Text = formatted,
                    ForceDM = true,
                    Sender = sender,
                    Recipient = recipient
                });

                _signal.Release(2);
                return;
            }

            // === 2. Режим DM (forceDM или нет голосового канала) ===

            if (isSelf)
            {
                // Один DM: «Вы отправили самому себе:»
                _queue.Enqueue(new VoiceQueueItem
                {
                    DmUser = sender,
                    Text = formatted,
                    ForceDM = true,
                    Sender = sender,
                    Recipient = recipient
                });

                _signal.Release(1);
                return;
            }

            // Не self → два DM: получателю и отправителю
            _queue.Enqueue(new VoiceQueueItem
            {
                DmUser = recipient,
                Text = formatted,
                ForceDM = true,
                Sender = sender,
                Recipient = recipient
            });

            _queue.Enqueue(new VoiceQueueItem
            {
                DmUser = sender,
                Text = formatted,
                ForceDM = true,
                Sender = sender,
                Recipient = recipient
            });

            _signal.Release(2);
        }


        // ================================
        //  ОСНОВНОЙ ОБРАБОТЧИК ОЧЕРЕДИ
        // ================================
        private async Task ProcessQueueAsync()
        {
            _logger.LogInformation("[VOICEPROCQUEUE] Обработчик очереди запущен");

            while (true)
            {
                await _signal.WaitAsync();

                if (!_queue.TryDequeue(out var item))
                    continue;

                _logger.LogInformation(
                    "[VOICEPROCQUEUE] Извлечена задача: Text=\"{Text}\", Channel={Channel}, DM={Dm}, ForceDM={ForceDM}, Sender={Sender}, Recipient={Recipient}",
                    item.Text,
                    item.Channel?.Name ?? "<null>",
                    item.DmUser?.Username ?? "<null>",
                    item.ForceDM,
                    item.Sender?.Username ?? "<null>",
                    item.Recipient?.Username ?? "<null>"
                );

                try
                {
                    if (string.IsNullOrWhiteSpace(item.Text))
                    {
                        _logger.LogInformation("[VOICEPROCQUEUE] Пустой текст в задаче — пропускаем");
                        continue;
                    }

                    // 1. ForceDM → всегда DM
                    if (item.ForceDM && item.DmUser != null)
                    {
                        await HandleDmItemAsync(item);
                        continue;
                    }

                    // 2. Нет голосового канала → DM
                    if (item.Channel == null && item.DmUser != null)
                    {
                        await HandleDmItemAsync(item);
                        continue;
                    }

                    // 3. Нет ни канала, ни DM — мусорная задача
                    if (item.Channel == null)
                    {
                        _logger.LogWarning("[VOICEPROCQUEUE] Задача без канала и без DM-пользователя — пропускаем");
                        continue;
                    }

                    // 4. Проверяем, есть ли живые пользователи в канале
                    if (!ChannelHasRealUsers(item.Channel))
                    {
                        _logger.LogInformation("[VOICEPROCQUEUE] Канал {Channel} пуст, пропускаем: {Text}",
                            item.Channel.Name, item.Text);
                        continue;
                    }

                    // 5. Проигрываем в голосовом канале
                    await PlayToVoiceChannelAsync(item);

                    await Task.Delay(_delayBetweenMessagesMs);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[VOICEPROCQUEUE] Ошибка при обработке очереди");
                }

                // 6. Если очередь пуста — закрываем все подключения
                if (_queue.IsEmpty)
                {
                    _logger.LogInformation("[VOICEPROCQUEUE] Очередь пуста — закрываем все голосовые подключения");

                    foreach (var kv in _connections)
                    {
                        try
                        {
                            _logger.LogInformation("[VOICEPROCQUEUE] Отключаемся от GuildId={GuildId}", kv.Key);
                            await kv.Value.Client.StopAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "[VOICEPROCQUEUE] Ошибка при отключении от GuildId={GuildId}", kv.Key);
                        }

                        _connections.TryRemove(kv.Key, out _);
                    }
                }
            }
        }

        // ================================
        //  DM-ОТПРАВКА
        // ================================
        private async Task HandleDmItemAsync(VoiceQueueItem item)
        {
            _logger.LogInformation("[VOICEDMHANDLER] DM → {User}", item.DmUser.Username);

            string path = await _tts.GenerateFileAsync(item.Text);

            try
            {
                bool isSelf = item.Sender != null &&
                              item.Recipient != null &&
                              item.Sender.Id == item.Recipient.Id &&
                              item.DmUser.Id == item.Sender.Id;

                // === SELF ===
                if (isSelf)
                {
                    await item.DmUser.SendMessageAsync(_templates.FormatTalkNotifySelf());
                    await item.DmUser.SendFileAsync(path);
                    return;
                }

                // === DM отправителю ===
                if (item.DmUser.Id == item.Sender?.Id && item.Recipient != null)
                {
                    await item.DmUser.SendMessageAsync(
                        _templates.FormatTalkNotifySender(item.Recipient)
                    );
                    await item.DmUser.SendFileAsync(path);
                    return;
                }

                // === DM получателю ===
                if (item.DmUser.Id == item.Recipient?.Id && item.Sender != null)
                {
                    await item.DmUser.SendMessageAsync(
                        _templates.FormatTalkNotifyRecipient(item.Sender)
                    );
                    await item.DmUser.SendFileAsync(path);
                    return;
                }

                // fallback
                await item.DmUser.SendFileAsync(path, "Вот ваша озвучка:");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[VOICEDMHANDLER] Ошибка отправки DM пользователю {User}", item.DmUser.Username);

                try
                {
                    await item.DmUser.SendMessageAsync("Не удалось отправить аудиофайл.");
                }
                catch (Exception ex2)
                {
                    _logger.LogWarning(ex2, "[VOICEDMHANDLER] Ошибка отправки fallback‑сообщения");
                }
            }
        }


        // ================================
        //  ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        // ================================
        private bool ChannelHasRealUsers(IVoiceChannel channel)
        {
            if (channel is not SocketVoiceChannel vc)
                return true;

            var has = vc.Users.Any(u => !u.IsBot);
            _logger.LogInformation("[VOICECHANNELNONEMPTY] ChannelHasRealUsers: Channel={Channel}, HasRealUsers={Has}",
                vc.Name, has);
            return has;
        }

        private async Task<IAudioClient> GetOrConnectAsync(IVoiceChannel channel)
        {
            if (_connections.TryGetValue(channel.GuildId, out var existing))
            {
                if (existing.Client.ConnectionState == ConnectionState.Connected &&
                    existing.ChannelId == channel.Id)
                {
                    return existing.Client;
                }

                try { await existing.Client.StopAsync(); } catch { }
                _connections.TryRemove(channel.GuildId, out _);
            }

            var client = await channel.ConnectAsync(selfDeaf: true);
            _connections[channel.GuildId] = (client, channel.Id);
            return client;
        }

        private async Task PlayToVoiceChannelAsync(VoiceQueueItem item)
        {
            var audioClient = await GetOrConnectAsync(item.Channel);

            string path = await _tts.GenerateFileAsync(item.Text);

            using var ffmpeg = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true
            });

            using var output = ffmpeg.StandardOutput.BaseStream;
            using var discord = audioClient.CreatePCMStream(AudioApplication.Mixed, bufferMillis: 1);

            try
            {
                await output.CopyToAsync(discord);
            }
            finally
            {
                await discord.FlushAsync();
            }

            if (!ChannelHasRealUsers(item.Channel))
            {
                await audioClient.StopAsync();
                _connections.TryRemove(item.Channel.GuildId, out _);
            }
        }
    }
}
