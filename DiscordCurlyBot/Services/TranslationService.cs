using DiscordCurlyBot.Interfaces;
using GTranslate.Translators;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace DiscordCurlyBot.Services
{
    internal class TranslationService : ITranslate
    {
        private readonly string _filePath;
        private readonly Dictionary<string, string> _cache = new();
        private readonly YandexTranslator _translator = new();
        private readonly ILogger<TranslationService> _logger;

        public TranslationService(IHostEnvironment env, ILogger<TranslationService> logger)
        {
            _logger = logger;
            var dataFolder = Path.Combine(env.ContentRootPath, "Data");
            _filePath = Path.Combine(dataFolder, "activity_mapping.json");

            if (!Directory.Exists(dataFolder))
                Directory.CreateDirectory(dataFolder);

            if (File.Exists(_filePath))
            {
                try
                {
                    var json = File.ReadAllText(_filePath);
                    var loaded = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    if (loaded != null)
                        _cache = loaded;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[TRANSLATE] Ошибка чтения activity_mapping.json, начинаем с пустого кэша");
                }
            }
        }

        public async Task<string> GetRussianActivityAsync(string engName)
        {
            if (string.IsNullOrEmpty(engName)) return string.Empty;
            if (_cache.TryGetValue(engName, out var ruName)) return ruName;

            try
            {
                var result = await _translator.TranslateAsync(engName, "en", "ru");
                _cache[engName] = result.Translation;

                await File.WriteAllTextAsync(_filePath, JsonConvert.SerializeObject(_cache, Formatting.Indented));
                return result.Translation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TRANSLATE] Ошибка перевода активности {Eng}", engName);
                return engName;
            }
        }
    }
}
