using Discord;
using Discord.Audio;
using DiscordCurlyBot.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;


namespace DiscordCurlyBot.Services
{
    internal class VoiceService : IVoice
    {
        private readonly HttpClient _httpClient;
        private readonly string _ttsEndpoint;
        private readonly ILogger<VoiceService> _logger;
        private readonly string _cachePath;

        public VoiceService(HttpClient httpClient, IConfiguration config, ILogger<VoiceService> logger, IHostEnvironment env)
        {
            _logger = logger;
            _httpClient = httpClient;
            _ttsEndpoint = config["TTS_ENDPOINT"] ?? throw new Exception("TTS_ENDPOINT not found!");
            _cachePath = Path.Combine(env.ContentRootPath, "Data", "tts-records");

            if (!Directory.Exists(_cachePath))
                Directory.CreateDirectory(_cachePath);
        }

        public async Task<double> SpeakAsync(IVoiceChannel channel, string text, CancellationToken ct)
        {
            try
            {
                string fileName = GetHash(text) + ".wav";
                string fullPath = Path.Combine(_cachePath, fileName);

                // 1. Проверка отмены ПЕРЕД запросом
                ct.ThrowIfCancellationRequested();

                if (!File.Exists(fullPath))
                {
                    _logger.LogInformation("Файл не найден. Запрашиваем озвучку: {text}", text);
                    var url = $"{_ttsEndpoint}/?text={Uri.EscapeDataString(text)}";

                    // Оставляем ct, чтобы прервать ЗАГРУЗКУ из сети, если пришел новый человек
                    using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                    response.EnsureSuccessStatusCode();

                    using var networkStream = await response.Content.ReadAsStreamAsync(ct);
                    using var fileStream = File.Create(fullPath);
                    await networkStream.CopyToAsync(fileStream, ct);
                }

                using var audioFileStream = File.OpenRead(fullPath);
                byte[] header = new byte[44];
                await audioFileStream.ReadExactlyAsync(header, 0, 44, ct);
                double duration = CalculateWavDuration(header);

                // 2. Вторая точка отмены ПЕРЕД входом в канал
                ct.ThrowIfCancellationRequested();

                _logger.LogInformation("Подключаемся к каналу {Name} для воспроизведения", channel.Name);
                audioFileStream.Position = 0; // Сброс на начало WAV

                // Сеньорский фикс №1: игнорируем ct при подключении, чтобы DAVE-шифрование прошло гладко
                using var audioClient = await channel.ConnectAsync(selfDeaf: true, selfMute: false, external: false);

                // Сеньорский фикс №2: Ждем стабилизации DAVE протокола (особенно актуально при > 1 участника)
                await Task.Delay(1500, CancellationToken.None);

                using var ffmpeg = CreateStream();
                using var output = ffmpeg.StandardOutput.BaseStream;

                // Сеньорский фикс №3: bufferMillis: 1 отключает декодирование чужого голоса (экономит ОЗУ и CPU)
                using var discord = audioClient.CreatePCMStream(AudioApplication.Mixed, bufferMillis: 1);

                try
                {
                    // Направляем данные из файла в FFmpeg (тоже без ct)
                    var pumpTask = audioFileStream.CopyToAsync(ffmpeg.StandardInput.BaseStream, CancellationToken.None)
                        .ContinueWith(_ => ffmpeg.StandardInput.BaseStream.Close());

                    // Читаем из FFmpeg и пишем в Discord
                    await output.CopyToAsync(discord, CancellationToken.None);

                    // Ждём завершения процесса FFmpeg
                    await ffmpeg.WaitForExitAsync(CancellationToken.None);
                }
                finally
                {
                    await discord.FlushAsync(CancellationToken.None);
                    await Task.Delay(1000, CancellationToken.None); // Даем договорить последние байты
                    await audioClient.StopAsync(); // Отключаемся (или комментируем, чтобы остаться в канале)
                }

                return duration;
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogError(ex, "[VOICE] Озвучка была отменена до начала воспроизведения");
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[VOICE] Ошибка в SpeakAsync");
                return 0;
            }
        }

        private string GetHash(string input)
        {
            using var md5 = MD5.Create();
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);
            return Convert.ToHexString(hashBytes);
        }

        private double CalculateWavDuration(byte[] header)
        {
            int sampleRate = BitConverter.ToInt32(header, 24);
            int dataSize = BitConverter.ToInt32(header, 40);
            return (double)dataSize / (sampleRate * 2 * 2);
        }

        private Process CreateStream() => Process.Start(new ProcessStartInfo
        {
            FileName = "ffmpeg",
            // Senior-way: абсолютный путь в Ubuntu лучше (/usr/bin/ffmpeg), но и просто ffmpeg подойдет
            Arguments = "-hide_banner -loglevel panic -f wav -i pipe:0 -ac 2 -f s16le -ar 48000 pipe:1",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true
        }) ?? throw new Exception("Не удалось запустить FFmpeg");
    }
}
