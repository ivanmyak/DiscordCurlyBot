using Discord.WebSocket;
using DiscordCurlyBot.Interfaces;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordCurlyBot.Services
{
    internal class VoiceTemplateService : IVoiceTemplate
    {
        private readonly string _single;
        private readonly string _singleWithActivity;
        private readonly string _group;
        private readonly string _talk;

        private readonly string _notifySender;
        private readonly string _notifyRecipient;
        private readonly string _notifySelf;

        public VoiceTemplateService(IConfiguration config)
        {
            _single = config["VoiceTemplates:SingleUser"]
                      ?? "Приветствуем {User}";

            _singleWithActivity = config["VoiceTemplates:SingleUserWithActivity"]
                                  ?? "Приветствуем {User}, который сейчас играет в {Activity}";

            _group = config["VoiceTemplates:GroupUsers"]
                     ?? "Приветствуем компанию: {Users}";

            _talk = config["VoiceTemplates:TalkCommand"]
                    ?? "{Text}";

            _notifySender = config["VoiceTemplates:TalkNotifySender"]
                            ?? "Вы отправили игроку {Recipient} сообщение:";

            _notifyRecipient = config["VoiceTemplates:TalkNotifyRecipient"]
                               ?? "Игрок {Sender} сказал вам:";

            _notifySelf = config["VoiceTemplates:TalkNotifySelf"]
                          ?? "Вы отправили сообщение самому себе:";
        }

        public string FormatSingle(string user)
            => _single.Replace("{User}", user);

        public string FormatSingleWithActivity(string user, string activity)
            => _singleWithActivity
                .Replace("{User}", user)
                .Replace("{Activity}", activity);

        public string FormatGroup(IEnumerable<string> users)
            => _group.Replace("{Users}", string.Join(", ", users));

        public string FormatTalk(string text)
            => _talk.Replace("{Text}", text);

        // ============================
        //     НОВЫЕ DM-ШАБЛОНЫ
        // ============================

        public string FormatTalkNotifySelf()
            => _notifySelf;

        public string FormatTalkNotifySender(SocketUser recipient)
        {
            // Кликая по этому mention — открывается профиль
            string mention = $"<@{recipient.Id}>";

            // Но в тексте мы хотим серверный ник
            string display = (recipient as SocketGuildUser)?.DisplayName ?? recipient.Username;

            return _notifySender
                .Replace("{Recipient}", $"{mention} ({display})");
        }

        public string FormatTalkNotifyRecipient(SocketUser sender)
        {
            string mention = $"<@{sender.Id}>";

            string display = (sender as SocketGuildUser)?.DisplayName ?? sender.Username;

            return _notifyRecipient
                .Replace("{Sender}", $"{mention} ({display})");
        }
    }
}
