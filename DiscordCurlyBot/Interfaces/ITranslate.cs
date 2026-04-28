using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordCurlyBot.Interfaces
{
    /// <summary>
    ///     Контракт сервиса перевода игровых активностей.
    ///     Реализация должна обеспечивать:
    ///     - кэширование переводов в локальном файле,
    ///     - устойчивость к сетевым ошибкам,
    ///     - возврат исходного текста при сбое.
    /// </summary>
    public interface ITranslate
    {
        /// <summary>
        ///     Переводит название активности (игры) на русский язык.
        ///     Если перевод уже есть в кэше — возвращает его.
        ///     Если перевод недоступен — возвращает исходный текст.
        /// </summary>
        /// <param name="engName">
        ///     Название игры на английском языке.
        ///     Может быть пустым — тогда возвращается пустая строка.
        /// </param>
        /// <returns>
        ///     Переведённое название игры.
        ///     Никогда не возвращает null.
        /// </returns>
        Task<string> GetRussianActivityAsync(string engName);
    }
}
