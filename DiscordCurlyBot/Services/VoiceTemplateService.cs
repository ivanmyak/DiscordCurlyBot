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

        private readonly string _talkNotifySender;
        private readonly string _talkNotifyRecipient;

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

            _talkNotifySender = config["VoiceTemplates:TalkNotifySender"]
                            ?? "Вы отправили игроку {Recipient} сообщение:";

            _talkNotifyRecipient = config["VoiceTemplates:TalkNotifyRecipient"]
                                   ?? "Игрок {Sender} сказал вам:";
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

        public string FormatTalkNotifySender(string recipient)
            => _talkNotifySender.Replace("{Recipient}", recipient);

        public string FormatTalkNotifyRecipient(string sender)
            => _talkNotifyRecipient.Replace("{Sender}", sender);
    }
}
