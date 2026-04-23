using Discord;
using DiscordCurlyBot.Interfaces;
using Microsoft.Extensions.Configuration;
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
        private readonly IVoice _voiceService;
        private readonly ITranslate _translator;
        private readonly ConcurrentDictionary<ulong, (List<string> Names, CancellationTokenSource CTS)> _queues = new();

        private readonly Stopwatch _speechTimer = new Stopwatch();
        private double _currentDuration = 0;
        private bool _isSpeaking = false;
        private readonly int _debounceMs;

        public VoiceQueueService(IVoice voiceService, ITranslate translator, IConfiguration config)
        {
            _voiceService = voiceService;
            _translator = translator;
            _debounceMs = config.GetValue("VoiceSettings:DebounceDelayMilliseconds", 3000);
        }

        public async Task EnqueueJoinAsync(IVoiceChannel channel, string userName, string activity)
        {
            var entry = _queues.GetOrAdd(channel.Id, _ => (new List<string>(), new CancellationTokenSource()));

            // Логика умного прерывания (50%)
            if (_isSpeaking && _currentDuration > 0)
            {
                double progress = _speechTimer.Elapsed.TotalSeconds / _currentDuration;
                if (progress < 0.5)
                {
                    entry.CTS.Cancel(); // Прерываем текущую читку
                    entry.CTS = new CancellationTokenSource();
                }
                else
                {
                    // Дочитываем, а этого юзера добавим в "очередь"
                    var name = await FormatUserAsync(userName, activity);
                    lock (entry.Names) { if (!entry.Names.Contains(name)) entry.Names.Add(name); }
                    return;
                }
            }

            var currentName = await FormatUserAsync(userName, activity);
            lock (entry.Names) { if (!entry.Names.Contains(currentName)) entry.Names.Add(currentName); }

            try
            {
                await Task.Delay(_debounceMs, entry.CTS.Token);
                await ProcessQueueAsync(channel, entry);
            }
            catch (OperationCanceledException) { }
        }

        private async Task<string> FormatUserAsync(string name, string act)
        {
            var ruAct = await _translator.GetRussianActivityAsync(act);
            return string.IsNullOrEmpty(ruAct) ? name : $"{name} (играет в {ruAct})";
        }

        private async Task ProcessQueueAsync(IVoiceChannel channel, (List<string> Names, CancellationTokenSource CTS) entry)
        {
            string[] names;
            lock (entry.Names) { names = entry.Names.ToArray(); entry.Names.Clear(); }
            if (names.Length == 0) return;

            string text = names.Length > 1
                ? $"Приветствуем компанию: {string.Join(", ", names)}"
                : $"Приветствуем {names[0]}";

            try
            {
                _isSpeaking = true;
                _speechTimer.Restart();
                _currentDuration = await _voiceService.SpeakAsync(channel, text, entry.CTS.Token);
            }
            finally
            {
                _isSpeaking = false;
                _speechTimer.Stop();
            }
        }
    }
}
