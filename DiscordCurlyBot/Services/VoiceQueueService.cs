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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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

        private const int DelayBetweenMessagesMs = 200;

        public VoiceQueueService(
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

            Task.Run(ProcessQueueAsync);
        }

        public async Task EnqueueJoinAsync(IVoiceChannel channel, string userName, string activity)
        {
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

            _signal.Release();
        }

        public async Task EnqueueDirectMessageAsync(SocketUser user, string text)
        {
            var gUser = _client.Guilds
                .Select(g => g.GetUser(user.Id))
                .FirstOrDefault(u => u?.VoiceChannel != null);

            if (gUser?.VoiceChannel != null)
            {
                _queue.Enqueue(new VoiceQueueItem
                {
                    Channel = gUser.VoiceChannel,
                    Text = _templates.FormatTalk(text)
                });
            }
            else
            {
                _queue.Enqueue(new VoiceQueueItem
                {
                    Channel = null,
                    DmUser = user,
                    Text = _templates.FormatTalk(text)
                });
            }

            _signal.Release();
            await Task.CompletedTask;
        }

        private async Task ProcessQueueAsync()
        {
            while (true)
            {
                await _signal.WaitAsync();

                if (!_queue.TryDequeue(out var item))
                    continue;

                try
                {
                    if (string.IsNullOrWhiteSpace(item.Text))
                        continue;

                    if (item.Channel == null && item.DmUser != null)
                    {
                        await HandleDmItemAsync(item);
                        continue;
                    }

                    if (item.Channel == null)
                        continue;

                    if (!ChannelHasRealUsers(item.Channel))
                    {
                        _logger.LogInformation("[VOICE] Канал {Channel} пуст, пропускаем: {Text}",
                            item.Channel.Name, item.Text);
                        continue;
                    }

                    await PlayToVoiceChannelAsync(item);

                    await Task.Delay(DelayBetweenMessagesMs);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[VOICE] Ошибка при обработке очереди");
                }

                if (_queue.IsEmpty)
                {
                    foreach (var kv in _connections)
                    {
                        try
                        {
                            await kv.Value.Client.StopAsync();
                        }
                        catch { }

                        _connections.TryRemove(kv.Key, out _);
                    }
                }
            }
        }

        private bool ChannelHasRealUsers(IVoiceChannel channel)
        {
            if (channel is not SocketVoiceChannel vc)
                return true;

            return vc.Users.Any(u => !u.IsBot);
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
            if (item.Channel == null)
                throw new Exception("[PLAYTOVOICE] Не удалось найти голосовой канал");

            var audioClient = await GetOrConnectAsync(item.Channel);

            if (!ChannelHasRealUsers(item.Channel))
            {
                await audioClient.StopAsync();
                _connections.TryRemove(item.Channel.GuildId, out _);
                return;
            }

            string path = await _tts.GenerateFileAsync(item.Text);

            using var ffmpeg = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true
            });

            if (ffmpeg == null)
                throw new Exception("Не удалось запустить ffmpeg");

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

        private async Task HandleDmItemAsync(VoiceQueueItem item)
        {
            string path = await _tts.GenerateFileAsync(item.Text);

            try
            {
                await item.DmUser.SendFileAsync(path, "Вот ваша озвучка:");
            }
            catch
            {
                await item.DmUser.SendMessageAsync("Не удалось отправить аудиофайл.");
            }
        }
    }
}
