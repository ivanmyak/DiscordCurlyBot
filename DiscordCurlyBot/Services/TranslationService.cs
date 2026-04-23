using DiscordCurlyBot.Interfaces;
using GTranslate.Translators;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace DiscordCurlyBot.Services
{
    internal class TranslationService: ITranslate
    {
        private readonly string _filePath;
        // Наш кэш, хранящийся в файле, по неймингу игр
        private Dictionary<string, string> _cache = new();
       // Попробую Яндекс
        private readonly YandexTranslator _translator = new();

        public TranslationService(IHostEnvironment env)
        {
            var dataFolder = Path.Combine(env.ContentRootPath, "Data");
            _filePath = Path.Combine(dataFolder, "activity_mapping.json");

            if (!Directory.Exists(dataFolder))
                Directory.CreateDirectory(dataFolder);

            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                _cache = JsonConvert.DeserializeObject<Dictionary<string, string>>(json) ?? new();
            }
        }

        public async Task<string> GetRussianActivityAsync(string engName)
        {
            if (string.IsNullOrEmpty(engName)) return string.Empty;
            if (_cache.TryGetValue(engName, out var ruName)) return ruName;

            try
            {
                var result = await _translator.TranslateAsync(engName, "ru", "en");
                _cache[engName] = result.Translation;

                await File.WriteAllTextAsync(_filePath, JsonConvert.SerializeObject(_cache, Formatting.Indented));
                return result.Translation;
            }
            catch
            {
                return engName; // Если перевод упал, возвращаем как есть
            }
        }
    }
}
