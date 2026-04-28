using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordCurlyBot.Interfaces
{
    /// <summary>
    ///     Контракт генератора TTS-аудиофайлов.
    ///     Реализация отвечает только за получение WAV-файла по тексту,
    ///     включая кэширование, сетевые запросы и обработку ошибок.
    ///     Никакого воспроизведения — только генерация.
    /// </summary>
    public interface ITTSGenerator
    {
        /// <summary>
        ///     Генерирует WAV-файл по тексту и возвращает путь к файлу.
        ///     Реализация должна:
        ///     - использовать кэш, если файл уже существует,
        ///     - корректно обрабатывать сетевые ошибки,
        ///     - не возвращать null,
        ///     - создавать файл в потокобезопасном режиме.
        /// </summary>
        /// <param name="text">
        ///     Текст, который нужно озвучить.
        ///     Не может быть null или пустым.
        /// </param>
        /// <param name="ct">
        ///     Токен отмены. Используется только для сетевых операций.
        ///     Отмена не должна приводить к частично записанным файлам.
        /// </param>
        /// <returns>
        ///     Полный путь к существующему WAV-файлу.
        ///     Гарантируется, что файл существует и доступен для чтения.
        /// </returns>
        Task<string> GenerateFileAsync(string text, CancellationToken ct = default);
    }
}
