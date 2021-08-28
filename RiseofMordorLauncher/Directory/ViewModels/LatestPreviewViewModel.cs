﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Discord;
using Discord.WebSocket;
using DSharpPlus;

namespace RiseofMordorLauncher
{
    public class LatestPreviewViewModel : BaseViewModel
    {
        private static string   DISCORD_BOT_TOKEN   = "NzQ4OTQwODg4NDk0ODMzNzU0.X0kvjg.7LGEK6yxa64ziaRzGmdjpsO3bgQ";
        private static ulong    PREVIEWS_CHANNEL_ID = 739128378669662228;
        //private static ulong    ROM_SERVER_ID       = 328911806372511744;

        private DiscordSocketClient     _client;
        private ISocketMessageChannel   _previewChannel;
        private string                  _latestPreviewURL;

        public BitmapImage PreviewImage { get; private set; }

        public LatestPreviewViewModel()
        {
            PreviewImage = new BitmapImage();
            MainAsync();
        }

        public async Task MainAsync()
        {
            var discord = new DiscordClient(new DiscordConfiguration()
            {
                Token       = DISCORD_BOT_TOKEN,
                TokenType   = DSharpPlus.TokenType.Bot,
                Intents     = DiscordIntents.All
            });

            await discord.ConnectAsync();

            _client                     = new DiscordSocketClient();
            _client.Ready               += OnDiscordClientConnected;
            _client.LoggedOut           += () =>
            {
                _client.LoginAsync(Discord.TokenType.Bot, DISCORD_BOT_TOKEN);
                return Task.CompletedTask;
            };

            await _client.LoginAsync(Discord.TokenType.Bot, DISCORD_BOT_TOKEN);
            await _client.StartAsync();
            await Task.Delay(-1);
        }

        private Task OnDiscordClientConnected()
        {
            _previewChannel = _client.GetChannel(PREVIEWS_CHANNEL_ID) as ISocketMessageChannel;
            var lastMessageAsync = _previewChannel.GetMessagesAsync(1);
            var lastMsgArr = AsyncEnumerableExtensions.FlattenAsync(lastMessageAsync);
            var lastMsg = lastMsgArr.Result.First();
            var attachment = lastMsg.Attachments.FirstOrDefault();
            _latestPreviewURL = attachment.Url;

            Application.Current.Dispatcher.BeginInvoke(
                new ThreadStart(() =>
                {
                    PreviewImage.BeginInit();
                    PreviewImage.UriSource = new Uri(_latestPreviewURL, UriKind.Absolute);
                    PreviewImage.EndInit();

                    OnPropertyChanged(nameof(PreviewImage));
                })
            );

            return Task.CompletedTask;
        }
    }
}