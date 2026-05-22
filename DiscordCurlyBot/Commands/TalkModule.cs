using Discord.Interactions;
using Discord.WebSocket;
using DiscordCurlyBot.Interfaces;
using DiscordCurlyBot.Services;
using Microsoft.Extensions.Configuration;
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
        private readonly int _maxTextLength;

        public TalkModule(
            IConfiguration config,
            IVoiceQueue voiceQueue,
            IVoiceTemplate templates,
            ILogger<LoggingService> logger)
        {
            _voiceQueue = voiceQueue;
            _templates = templates;
            _logger = logger;

            var maxTextLengthStr = config["VoiceSettings:MaxTextLength"];

            if (!int.TryParse(maxTextLengthStr, out _maxTextLength) || _maxTextLength <= 0)
            {
                _maxTextLength = 300;
                _logger.LogWarning("[TALK] VoiceSettings:MaxTextLength отсутствует или некорректен. Используем значение по умолчанию: 300");
            }
            else
            {
                _logger.LogInformation("[TALK] MaxTextLength загружен из конфигурации: {Value}", _maxTextLength);
            }
        }

        [SlashCommand("talk", "Озвучить текст голосом бота")]
        public async Task TalkAsync(
            [Summary("text", "Текст для озвучки")] string text,
            [Summary("user", "Пользователь, которому озвучить (необязательно)")] SocketGuildUser? targetUser = null,
            [Summary("isPersonalMessage", "Отправить сразу в личку (true) или пытаться в голосовой (false)")]
            bool isPersonalMessage = false)
        {
            // 1. Проверка текста
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogInformation("[TALK] Пустой текст от {User} ({Id})", Context.User.Username, Context.User.Id);
                await RespondAsync("Текст пустой.", ephemeral: true);
                return;
            }

            if (text.Length > _maxTextLength)
            {
                _logger.LogInformation("[TALK] Слишком длинный текст ({Len}) от {User} ({Id})",
                    text.Length, Context.User.Username, Context.User.Id);

                await RespondAsync(
                    $"Текст слишком длинный (максимум {_maxTextLength} символов, а в сообщении {text.Length} символов).",
                    ephemeral: true
                );
                return;
            }

            // 2. Формируем текст по шаблону
            string formatted = _templates.FormatTalk(text);

            // 3. Если команда вызвана в ЛИЧКЕ
            if (Context.Guild == null)
            {
                _logger.LogInformation("[TALK] Команда вызвана в DM. User={User} ({Id}), Text=\"{Text}\"",
                    Context.User.Username, Context.User.Id, text);

                await _voiceQueue.EnqueueDirectMessageAsync(
                    Context.User,   // sender
                    formatted,      // text
                    true,           // forceDM
                    Context.User    // recipient
                );

                await RespondAsync(
                    "Команда вызвана в личных сообщениях. Озвучка будет отправлена вам в DM.",
                    ephemeral: true
                );
                return;
            }

            // 4. Определяем вызывающего
            var caller = Context.User as SocketGuildUser;
            if (caller == null)
            {
                _logger.LogWarning("[TALK] Не удалось привести Context.User к SocketGuildUser. UserId={Id}", Context.User.Id);
                await RespondAsync("Не удалось определить пользователя.", ephemeral: true);
                return;
            }

            // 5. Определяем целевого пользователя
            var target = targetUser ?? caller;

            _logger.LogInformation(
                "[TALK] Вызов /talk. Caller={CallerName}({CallerId}), Target={TargetName}({TargetId}), isPersonalMessage={PM}, Text=\"{Text}\"",
                caller.DisplayName, caller.Id,
                target.DisplayName, target.Id,
                isPersonalMessage,
                text
            );

            // === РЕЖИМ 1: ВСЕГДА В ЛИЧКУ ===
            if (isPersonalMessage)
            {
                _logger.LogInformation("[TALK] Режим: личные сообщения. Target={TargetName}({TargetId})",
                    target.DisplayName, target.Id);

                try
                {
                    await _voiceQueue.EnqueueDirectMessageAsync(
                        caller,         // sender
                        formatted,      // text
                        true,           // forceDM
                        target          // recipient
                    );

                    _logger.LogInformation("[TALK] Озвучка отправлена пользователю {Target}", target.DisplayName);

                    await RespondAsync(
                        $"Озвучка отправлена пользователю **{target.DisplayName}** в личные сообщения.",
                        ephemeral: true
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[TALK] Ошибка отправки DM пользователю {TargetName}({TargetId})",
                        target.DisplayName, target.Id);

                    await RespondAsync(
                        "Не удалось отправить сообщение в личку (возможно, закрыты ЛС).",
                        ephemeral: true
                    );
                }

                return;
            }

            // === РЕЖИМ 2: ПЫТАТЬСЯ В ГОЛОСОВОЙ ===
            if (target.VoiceChannel != null)
            {
                _logger.LogInformation("[TALK] Target в голосовом канале {Channel} (Guild {GuildId})",
                    target.VoiceChannel.Name, target.Guild.Id);

                await _voiceQueue.EnqueueDirectMessageAsync(
                    caller,
                    formatted,
                    false,
                    target
                );

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

            // === Target НЕ в голосовом канале → fallback в DM ===
            _logger.LogInformation("[TALK] Target не в голосовом канале. Пытаемся отправить DM. Target={TargetName}({TargetId})",
                target.DisplayName, target.Id);

            try
            {
                await _voiceQueue.EnqueueDirectMessageAsync(
                    caller,
                    formatted,
                    false,
                    target
                );

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
