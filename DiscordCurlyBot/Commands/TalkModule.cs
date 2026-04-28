using Discord.Interactions;
using Discord.WebSocket;
using DiscordCurlyBot.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordCurlyBot.Commands
{
    public class TalkModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly IVoiceQueue _voiceQueue;
        private readonly IVoiceTemplate _templates;

        public TalkModule(IVoiceQueue voiceQueue, IVoiceTemplate templates)
        {
            _voiceQueue = voiceQueue;
            _templates = templates;
        }

        [SlashCommand("talk", "Озвучить текст голосом бота")]
        public async Task TalkAsync([Summary("text", "Текст для озвучки")] string text)
        {
            // 1. Проверка текста
            if (string.IsNullOrWhiteSpace(text))
            {
                await RespondAsync("Текст пустой.", ephemeral: true);
                return;
            }

            if (text.Length > 300)
            {
                await RespondAsync("Текст слишком длинный (максимум 300 символов).", ephemeral: true);
                return;
            }

            // 2. Формируем текст по шаблону
            string formatted = _templates.FormatTalk(text);

            // 3. Если команда вызвана в ЛИЧКЕ
            if (Context.Guild == null)
            {
                // В личке пользователь НЕ может быть в голосовом канале
                // Поэтому сразу отправляем TTS-файл в DM
                await _voiceQueue.EnqueueDirectMessageAsync(Context.User, formatted);

                await RespondAsync(
                    "Команда вызвана в личных сообщениях. Озвучка будет отправлена вам в DM.",
                    ephemeral: true
                );
                return;
            }

            // 4. Команда вызвана на сервере → пытаемся получить SocketGuildUser
            var gUser = Context.User as SocketGuildUser;
            if (gUser == null)
            {
                await RespondAsync("Не удалось определить пользователя.", ephemeral: true);
                return;
            }

            // 5. Если пользователь находится в голосовом канале → озвучиваем там
            if (gUser.VoiceChannel != null)
            {
                await _voiceQueue.EnqueueDirectMessageAsync(gUser, formatted);

                await RespondAsync(
                    "Добавлено в очередь озвучки в вашем голосовом канале.",
                    ephemeral: true
                );
                return;
            }

            // 6. Пользователь НЕ в голосовом канале → пробуем отправить TTS в личку
            try
            {
                await _voiceQueue.EnqueueDirectMessageAsync(gUser, formatted);

                await RespondAsync(
                    "Вы не в голосовом канале. Озвучка будет отправлена вам в личные сообщения.",
                    ephemeral: true
                );
            }
            catch
            {
                // 7. Если личка закрыта → сообщаем об ошибке
                await RespondAsync(
                    "Не удалось воспроизвести ваше сообщение: вы не находитесь ни в одном голосовом канале, а личные сообщения недоступны.",
                    ephemeral: true
                );
            }
        }
    }
}
