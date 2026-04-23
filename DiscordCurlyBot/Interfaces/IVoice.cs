using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordCurlyBot.Interfaces
{
    /// <summary>
    /// Сервис по озвучиванию
    /// </summary>
    internal interface IVoice
    {
        /// <summary>
        /// Озвучивание переданного сообщения
        /// </summary>
        /// <param name="channel">Канал, к которому пользователь подключается</param>
        /// <param name="text">сообщение, которое нужно озвучить</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<double> SpeakAsync(IVoiceChannel channel, string text, CancellationToken ct);
    }
}
