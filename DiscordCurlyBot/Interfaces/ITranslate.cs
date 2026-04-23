using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordCurlyBot.Interfaces
{
    /// <summary>
    /// Переводчик английского в русский. 
    /// Используется для перевода в русский активностей для безпроблемного 
    /// голосового воспроизведения. 
    /// Переведённое хранит во внутреннем файле activity_mapping.json
    /// </summary>
    internal interface ITranslate
    {
        /// <summary>
        /// Перевод активности из английской в русскую
        /// </summary>
        /// <param name="engName">английский нейминг активности</param>
        /// <returns></returns>
        Task<string> GetRussianActivityAsync(string engName);
    }
}
