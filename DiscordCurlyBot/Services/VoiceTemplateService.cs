using DiscordCurlyBot.Interfaces;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordCurlyBot.Services
{
    internal class VoiceTemplateService : IVoiceTemplate
    {
        private readonly IConfiguration _config;

        private readonly string _single;
        private readonly string _singleWithActivity;
        private readonly string _group;
        private readonly string _talk;

        public VoiceTemplateService(IConfiguration config)
        {
            _config = config;

            _single = _config["VoiceTemplates:SingleUser"]
                      ?? "Приветствуем {User}";

            _singleWithActivity = _config["VoiceTemplates:SingleUserWithActivity"]
                                  ?? "Приветствуем {User}, который сейчас играет в {Activity}";

            _group = _config["VoiceTemplates:GroupUsers"]
                     ?? "Приветствуем компанию: {Users}";

            _talk = _config["VoiceTemplates:TalkCommand"]
                    ?? "{Text}";
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
    }
}
