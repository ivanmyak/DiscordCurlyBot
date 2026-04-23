using Discord;
using Discord.Audio;
using DiscordCurlyBot.Interfaces;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace DiscordCurlyBot.Services
{
    internal class VoiceService : IVoice
    {
        private readonly HttpClient _httpClient;
        private readonly string _ttsEndpoint;

        public VoiceService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _ttsEndpoint = config["TTS_ENDPOINT"] ?? throw new Exception("TTS_ENDPOINT not found!");
        }

        public async Task<double> SpeakAsync(IVoiceChannel channel, string text, CancellationToken ct)
        {
            // передаем данные через POST JSON
            var payload = new { text = text };
            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_ttsEndpoint}/GetVoice", content, ct);
            response.EnsureSuccessStatusCode();

            using var audioStream = await response.Content.ReadAsStreamAsync(ct);

            // Читаем первые 44 байта (WAV заголовок)
            byte[] header = new byte[44];
            await audioStream.ReadAsync(header, 0, 44, ct);
            double duration = CalculateWavDuration(header);

            using var audioClient = await channel.ConnectAsync();
            using var ffmpeg = CreateStream();
            using var discordStream = audioClient.CreatePCMStream(AudioApplication.Mixed);

            try
            {
                // Качаем данные из Piper в FFmpeg
                var pumpTask = audioStream.CopyToAsync(ffmpeg.StandardInput.BaseStream, ct)
                    .ContinueWith(_ => ffmpeg.StandardInput.BaseStream.Close(), ct);

                // Читаем из FFmpeg в Discord
                await ffmpeg.StandardOutput.BaseStream.CopyToAsync(discordStream, ct);
            }
            finally
            {
                await discordStream.FlushAsync(ct);
                await audioClient.StopAsync();
            }

            return duration;
        }

        private double CalculateWavDuration(byte[] header)
        {
            int sampleRate = BitConverter.ToInt32(header, 24);
            int dataSize = BitConverter.ToInt32(header, 40);
            // Формула для 16-bit Mono/Stereo (2 байта на сэмпл * кол-во каналов)
            return (double)dataSize / (sampleRate * 2 * 2);
        }

        private Process CreateStream() => Process.Start(new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = "-hide_banner -loglevel panic -i pipe:0 -ac 2 -f s16le -ar 48000 pipe:1",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true
        })!;
    }
}
