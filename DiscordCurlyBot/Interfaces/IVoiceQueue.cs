using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordCurlyBot.Interfaces
{
    /// <summary>
    /// Обработчик очереди озвучивания
    /// </summary>
    internal interface IVoiceQueue
    {
        /// <summary>
        /// Подключение нового человека в канал 
        /// </summary>
        /// <param name="channel">Канал, в который игрок заходит</param>
        /// <param name="userName">Наименование игрока. Может быть его ником, или именем на сервере></param>
        /// <param name="activity">Наименование активности</param>
        /// <returns></returns>
        Task EnqueueJoinAsync(IVoiceChannel channel, string userName, string activity);
    }
}
