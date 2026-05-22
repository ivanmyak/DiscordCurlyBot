using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordCurlyBot.Classes
{
    internal class VoiceQueueItem
    {
        public IVoiceChannel? Channel { get; set; }
        public string Text { get; set; } = null!;

        // Если задача пришла из лички
        public SocketUser? DmUser { get; set; } 

        // По умолчанию - голос, потом личка. Если True - сразу и только в личку
        public bool ForceDM { get; set; } = false;

        // Для логирования/диагностики
        public ulong? GuildId => Channel?.GuildId;

        // Пользователь-отправитель
        public SocketUser? Sender { get; set; }

        // Пользователь-назначение
        public SocketUser? Recipient { get; set; }
    }
}
