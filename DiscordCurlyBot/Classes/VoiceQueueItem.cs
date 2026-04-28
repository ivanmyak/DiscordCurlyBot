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
        public SocketUser DmUser { get; set; } = null!;

        // Для логирования/диагностики
        public ulong? GuildId => Channel?.GuildId;
    }
}
