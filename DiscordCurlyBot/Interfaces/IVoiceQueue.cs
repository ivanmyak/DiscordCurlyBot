using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordCurlyBot.Interfaces
{
    /// <summary>
    ///     Контракт очереди голосовых задач.
    ///     Реализация обеспечивает строгое последовательное воспроизведение TTS,
    ///     корректное подключение/отключение бота к голосовым каналам,
    ///     обработку пустых каналов и fallback в личные сообщения.
    /// </summary>
    public interface IVoiceQueue
    {
        /// <summary>
        ///     Ставит в очередь задачу на озвучивание приветствия пользователя,
        ///     который вошёл в голосовой канал или сменил игровую активность.
        ///     Текст формируется через шаблоны и переводчик активности.
        /// </summary>
        /// <param name="channel">
        ///     Голосовой канал, в котором нужно воспроизвести озвучку.
        ///     Не может быть null. Если канал пустой — задача будет пропущена.
        /// </param>
        /// <param name="userName">
        ///     Имя пользователя (Nickname или Username), которое будет озвучено.
        /// </param>
        /// <param name="activity">
        ///     Название игры или активности. Может быть пустым.
        ///     Если не пустое — будет переведено и включено в шаблон.
        /// </param>
        Task EnqueueJoinAsync(IVoiceChannel channel, string userName, string activity);

        /// <summary>
        ///     Ставит в очередь задачу на озвучивание текста, присланного пользователем
        ///     через команду /talk или личное сообщение.
        ///     Если пользователь находится в голосовом канале — озвучка будет
        ///     воспроизведена там. Если нет — TTS-файл будет отправлен в личку.
        /// </summary>
        /// <param name="user">
        ///     Пользователь, инициировавший озвучку.
        ///     Используется для определения голосового канала или отправки DM.
        /// </param>
        /// <param name="text">
        ///     Текст, который нужно озвучить. Форматируется через шаблон TalkCommand.
        /// </param>
        Task EnqueueDirectMessageAsync(SocketUser user, string text);
    }
}
