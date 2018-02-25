﻿using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

// https://dev.mixer.com/reference/constellation/index.html

namespace Fritz.StreamTools.Services.Mixer
{
	public interface IMixerLive : IDisposable
	{
		event EventHandler<EventEventArgs> LiveEvent;
		Task ConnectAndJoinAsync(int channelId);
	}

	internal class MixerLive : IMixerLive
	{
		const string WS_URL = "wss://constellation.mixer.com";

		readonly IConfiguration _config;
		readonly ILoggerFactory _loggerFactory;
		readonly IMixerFactory _factory;
		readonly CancellationToken _shutdown;
		readonly ILogger _logger;
		IJsonRpcWebSocket _channel;

		public MixerLive(IConfiguration config, ILoggerFactory loggerFactory, IMixerFactory factory, CancellationToken shutdown)
		{
			_config = config ?? throw new ArgumentNullException(nameof(config));
			_loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
			_factory = factory ?? throw new ArgumentNullException(nameof(factory));
			_shutdown = shutdown;
			_logger = loggerFactory.CreateLogger("MixerLive");
		}

		/// <summary>
		/// Raised each time a chat message is received
		/// </summary>
		public event EventHandler<EventEventArgs> LiveEvent;

		/// <summary>
		/// Connect to the live event server, and join our channel
		/// </summary>
		/// <param name="channelId">Out channelId</param>
		/// <returns></returns>
		public async Task ConnectAndJoinAsync(int channelId)
		{
			// Include token on connect if available
			var token = _config["StreamServices:Mixer:Token"];
			if (string.IsNullOrWhiteSpace(token)) token = null;

			_channel = _factory.CreateJsonRpcWebSocket(_logger, isChat: false);

			// Connect to the chat endpoint
			while (!await _channel.TryConnectAsync(() => WS_URL, token, () =>	{
				// Join the channel and request live updates
				return _channel.SendAsync("livesubscribe", $"channel:{channelId}:update");
			}));

			_channel.EventReceived += Chat_EventReceived;
		}

		/// <summary>
		/// Called when we receive a new live event from server
		/// </summary>
		private void Chat_EventReceived(object sender, EventEventArgs e)
		{
			if(e.Event == "live")
			{
				Debug.Assert(e.Data["payload"] != null);
				LiveEvent?.Invoke(this, new EventEventArgs { Event = e.Event, Data = e.Data["payload"] });
			}
		}

		public void Dispose()
		{
			_channel.Dispose();
			GC.SuppressFinalize(this);
		}
	}
}
