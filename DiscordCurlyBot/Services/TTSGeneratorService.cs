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
    internal class TTSGeneratorService : ITTSGenerator
    {
        private readonly HttpClient _httpClient;
        private readonly string _ttsEndpoint;
        private readonly ILogger<TTSGeneratorService> _logger;
        private readonly string _cachePath;

        private static readonly SemaphoreSlim _ttsLock = new(1, 1);

        public TTSGeneratorService(
            HttpClient httpClient,
            IConfiguration config,
            ILogger<TTSGeneratorService> logger,
            IHostEnvironment env)
        {
            _httpClient = httpClient;
            _logger = logger;
            _ttsEndpoint = config["TTS_ENDPOINT"] ?? throw new Exception("TTS_ENDPOINT not found!");

            _cachePath = Path.Combine(env.ContentRootPath, "Data", "tts-records");
            if (!Directory.Exists(_cachePath))
                Directory.CreateDirectory(_cachePath);
        }

        public async Task<string> GenerateFileAsync(string text, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("TTS text cannot be empty", nameof(text));

            string fileName = GetHash(text) + ".wav";
            string fullPath = Path.Combine(_cachePath, fileName);

            if (File.Exists(fullPath))
            {
                _logger.LogInformation("[TTS] Используем кэшированный файл для текста: {Text}", text);
                return fullPath;
            }

            _logger.LogInformation("[TTS] Генерируем новый файл для текста: {Text}", text);
            var url = $"{_ttsEndpoint}/?text={Uri.EscapeDataString(text)}";

            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                await using var networkStream = await response.Content.ReadAsStreamAsync(ct);

                await _ttsLock.WaitAsync(ct);
                try
                {
                    await using var fileStream = File.Create(fullPath);
                    await networkStream.CopyToAsync(fileStream, ct);
                }
                finally
                {
                    _ttsLock.Release();
                }

                return fullPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TTS] Ошибка при запросе к TTS-серверу");
                throw;
            }
        }

        private string GetHash(string input)
        {
            using var md5 = MD5.Create();
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);
            return Convert.ToHexString(hashBytes);
        }
    }
}
