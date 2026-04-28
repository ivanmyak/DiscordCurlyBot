using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordCurlyBot.Interfaces
{
    /// <summary>
    ///     Контракт сервиса шаблонов голосовых сообщений.
    ///     Реализация отвечает за формирование итоговых строк,
    ///     используя шаблоны из конфигурации (appsettings.json).
    ///     Никакой логики перевода или очереди — только форматирование.
    /// </summary>
    public interface IVoiceTemplate
    {
        /// <summary>
        ///     Формирует фразу приветствия одного пользователя.
        ///     Использует шаблон VoiceTemplates:SingleUser.
        /// </summary>
        /// <param name="user">Имя пользователя.</param>
        string FormatSingle(string user);

        /// <summary>
        ///     Формирует фразу приветствия пользователя с указанием активности.
        ///     Использует шаблон VoiceTemplates:SingleUserWithActivity.
        /// </summary>
        /// <param name="user">Имя пользователя.</param>
        /// <param name="activity">Название активности (уже переведённое).</param>
        string FormatSingleWithActivity(string user, string activity);

        /// <summary>
        ///     Формирует фразу приветствия группы пользователей.
        ///     Использует шаблон VoiceTemplates:GroupUsers.
        /// </summary>
        /// <param name="users">Список имён пользователей.</param>
        string FormatGroup(IEnumerable<string> users);

        /// <summary>
        ///     Формирует фразу для команды /talk.
        ///     Использует шаблон VoiceTemplates:TalkCommand.
        /// </summary>
        /// <param name="text">Текст, который нужно озвучить.</param>
        string FormatTalk(string text);
    }
}
