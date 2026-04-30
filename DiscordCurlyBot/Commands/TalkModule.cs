using Discord.Interactions;
using Discord.WebSocket;
using DiscordCurlyBot.Interfaces;
using DiscordCurlyBot.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordCurlyBot.Commands
{
    public class TalkModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly IVoiceQueue _voiceQueue;
        private readonly IVoiceTemplate _templates;
        private readonly ILogger<LoggingService> _logger;

        public TalkModule(
            IVoiceQueue voiceQueue,
            IVoiceTemplate templates,
            ILogger<LoggingService> logger)
        {
            _voiceQueue = voiceQueue;
            _templates = templates;
            _logger = logger;
        }

        [SlashCommand("talk", "Озвучить текст голосом бота")]
        public async Task TalkAsync(
            [Summary("text", "Текст для озвучки")] string text,
            [Summary("user", "Пользователь, которому озвучить (необязательно)")] SocketGuildUser? targetUser = null)
        {
            // 1. Проверка текста
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogInformation("[TALK] Пустой текст от {User} ({Id})", Context.User.Username, Context.User.Id);
                await RespondAsync("Текст пустой.", ephemeral: true);
                return;
            }

            if (text.Length > 300)
            {
                _logger.LogInformation("[TALK] Слишком длинный текст ({Len}) от {User} ({Id})", text.Length, Context.User.Username, Context.User.Id);
                await RespondAsync("Текст слишком длинный (максимум 300 символов).", ephemeral: true);
                return;
            }

            // 2. Формируем текст по шаблону
            string formatted = _templates.FormatTalk(text);

            // 3. Если команда вызвана в ЛИЧКЕ
            if (Context.Guild == null)
            {
                _logger.LogInformation("[TALK] Команда вызвана в DM. User={User} ({Id}), Text=\"{Text}\"",
                    Context.User.Username, Context.User.Id, text);

                await _voiceQueue.EnqueueDirectMessageAsync(Context.User, formatted);

                await RespondAsync(
                    "Команда вызвана в личных сообщениях. Озвучка будет отправлена вам в DM.",
                    ephemeral: true
                );
                return;
            }

            // 4. Команда вызвана на сервере → определяем вызывающего
            var caller = Context.User as SocketGuildUser;
            if (caller == null)
            {
                _logger.LogWarning("[TALK] Не удалось привести Context.User к SocketGuildUser. UserId={Id}", Context.User.Id);
                await RespondAsync("Не удалось определить пользователя.", ephemeral: true);
                return;
            }

            // 5. Определяем целевого пользователя (target)
            var target = targetUser ?? caller;

            _logger.LogInformation("[TALK] Вызов /talk. Caller={CallerName}({CallerId}), Target={TargetName}({TargetId}), Text=\"{Text}\"",
                caller.DisplayName, caller.Id,
                target.DisplayName, target.Id,
                text);

            // 6. Если target в голосовом канале → озвучиваем там
            if (target.VoiceChannel != null)
            {
                _logger.LogInformation("[TALK] Target в голосовом канале {Channel} (Guild {GuildId})",
                    target.VoiceChannel.Name, target.Guild.Id);

                await _voiceQueue.EnqueueDirectMessageAsync(target, formatted);

                if (targetUser == null)
                {
                    await RespondAsync(
                        "Добавлено в очередь озвучки в вашем голосовом канале.",
                        ephemeral: true
                    );
                }
                else
                {
                    await RespondAsync(
                        $"Добавлено в очередь озвучки в голосовом канале пользователя **{target.DisplayName}**.",
                        ephemeral: true
                    );
                }

                return;
            }

            // 7. Target НЕ в голосовом канале → пробуем отправить TTS в личку
            _logger.LogInformation("[TALK] Target не в голосовом канале. Пытаемся отправить DM. Target={TargetName}({TargetId})",
                target.DisplayName, target.Id);

            try
            {
                await _voiceQueue.EnqueueDirectMessageAsync(target, formatted);

                if (targetUser == null)
                {
                    await RespondAsync(
                        "Вы не в голосовом канале. Озвучка будет отправлена вам в личные сообщения.",
                        ephemeral: true
                    );
                }
                else
                {
                    await RespondAsync(
                        $"Пользователь **{target.DisplayName}** не в голосовом канале. Озвучка отправлена ему в личные сообщения.",
                        ephemeral: true
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[TALK] Не удалось отправить DM озвучку. Target={TargetName}({TargetId})",
                    target.DisplayName, target.Id);

                await RespondAsync(
                    "Не удалось воспроизвести сообщение: пользователь не в голосовом канале, а личные сообщения недоступны.",
                    ephemeral: true
                );
            }
        }
    }
}
