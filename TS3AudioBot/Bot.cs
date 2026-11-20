// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TS3AudioBot.Algorithm;
using TS3AudioBot.Audio;
using TS3AudioBot.CommandSystem;
using TS3AudioBot.CommandSystem.Text;
using TS3AudioBot.Config;
using TS3AudioBot.Dependency;
using TS3AudioBot.Environment;
using TS3AudioBot.Helper;
using TS3AudioBot.History;
using TS3AudioBot.Localization;
using TS3AudioBot.Playlists;
using TS3AudioBot.Plugins;
using TS3AudioBot.ResourceFactories;
using TS3AudioBot.Sessions;
using TSLib;
using TSLib.Full;
using TSLib.Helper;
using TSLib.Messages;
using TSLib.Scheduler;

namespace TS3AudioBot;

/// <summary>Core class managing all bots and utility modules.</summary>
public sealed class Bot
{
	private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

	private readonly ConfBot config;
	private readonly TickWorker idleTickWorker;
	private CancellationTokenSource? avatarRequest;

	private bool isClosed;
	internal BotInjector Injector { get; }
	internal DedicatedTaskScheduler Scheduler { get; }

	public Id Id { get; }

	/// <summary>This is the template name. Can be null.</summary>
	public string? Name => config.Name;

	public bool QuizMode { get; set; }

	private readonly ResolveContext resourceResolver;
	private readonly Ts3Client ts3client;
	private Frix.FrixPluginHost? plugins;
	private System.Threading.Timer? pluginTimer;
	private readonly TsFullClient ts3FullClient;
	private readonly SessionManager sessionManager;
	private readonly PlayManager playManager;
	private readonly IVoiceTarget targetManager;
	private readonly Player player;
	private readonly Stats stats;
	private readonly LocalizationManager localization;

	public Bot(Id id, ConfBot config, BotInjector injector)
	{
		this.Id = id;
		this.config = config;
		this.Injector = injector;

		var builder = new DependencyBuilder(Injector);
		Injector.HideParentModule<CommandManager>();
		Injector.HideParentModule<DedicatedTaskScheduler>();
		Injector.AddModule(this);
		Injector.AddModule(config);
		Injector.AddModule(Injector);
		Injector.AddModule(config.Playlists);
		Injector.AddModule(config.History);
		Injector.AddModule(Id);
		builder.RequestModule<PlaylistIO>();
		builder.RequestModule<PlaylistManager>();
		builder.RequestModule<DedicatedTaskScheduler>();
		builder.RequestModule<TsFullClient>();
		builder.RequestModule<TsBaseFunctions, TsFullClient>();
		builder.RequestModule<Ts3Client>();
		builder.RequestModule<Player>();
		builder.RequestModule<CustomTargetPipe>();
		builder.RequestModule<IVoiceTarget, CustomTargetPipe>();
		builder.RequestModule<SessionManager>();
		builder.RequestModule<ResolveContext>();
		builder.RequestModule<CommandManager>();
		builder.RequestModule<LocalizationManager>();
		if (config.History.Enabled)
		{
			builder.RequestModule<HistoryManager>();
		}

		builder.RequestModule<PlayManager>();

		if (!builder.Build())
		{
			Log.Error("Missing bot module dependency");
			throw new Exception("Could not load all bot modules");
		}

		Injector.ClearHiddenParentModules();

		resourceResolver = Injector.GetModuleOrThrow<ResolveContext>();
		ts3FullClient = Injector.GetModuleOrThrow<TsFullClient>();
		ts3client = Injector.GetModuleOrThrow<Ts3Client>();
		player = Injector.GetModuleOrThrow<Player>();
		Scheduler = Injector.GetModuleOrThrow<DedicatedTaskScheduler>();
		var customTarget = Injector.GetModuleOrThrow<CustomTargetPipe>();
		player.SetTarget(customTarget);
		Injector.AddModule(ts3FullClient.Book);
		playManager = Injector.GetModuleOrThrow<PlayManager>();
		targetManager = Injector.GetModuleOrThrow<IVoiceTarget>();
		sessionManager = Injector.GetModuleOrThrow<SessionManager>();
		stats = Injector.GetModuleOrThrow<Stats>();
		var commandManager = Injector.GetModuleOrThrow<CommandManager>();
		localization = Injector.GetModuleOrThrow<LocalizationManager>();

		// Registering config changes
		config.Language.Changed += async (s, e) =>
		{
			var langResult = await localization.LoadLanguage(e.NewValue, true);
			if (!langResult.Ok)
				Log.Error("Failed to load language file ({0})", langResult.Error);
		};
		config.Events.IdleDelay.Changed += (s, e) => EnableIdleTickWorker();
		config.Events.OnIdle.Changed += (s, e) => EnableIdleTickWorker();

		idleTickWorker = Scheduler.Invoke(() => Scheduler.CreateTimer(OnIdle, TimeSpan.MaxValue, false)).Result;

		player.OnSongEnd += playManager.SongStoppedEvent;
		player.OnSongUpdated += (s, e) => playManager.Update(e);
		// Update idle status events
		playManager.BeforeResourceStarted += (s, e) =>
		{
			DisableIdleTickWorker();
			return Task.CompletedTask;
		};
		playManager.PlaybackStopped += (s, e) =>
		{
			EnableIdleTickWorker();
			return Task.CompletedTask;
		};
		// Used for custom scripts, like voice_mode, onsongstart
		playManager.BeforeResourceStarted += BeforeResourceStarted;
		playManager.AfterResourceStarted += AfterResourceStarted;
		// Update the own status text to the current song title
		playManager.AfterResourceStarted += (s, e) => UpdateBotStatus();
		playManager.PlaybackStopped += (s, e) => UpdateBotStatus();
		playManager.OnResourceUpdated += (s, e) => UpdateBotStatus();
		// Log our resource in the history
		if (Injector.TryGet<HistoryManager>(out var historyManager))
			playManager.AfterResourceStarted += (s, e) =>
			{
				historyManager.LogAudioResource(new HistorySaveData(e.PlayResource.AudioResource,
					e.EffectiveOwnerUid));
				return Task.CompletedTask;
			};
		// Update our thumbnail
		playManager.AfterResourceStarted += (s, e) => GenerateStatusImage(true, e);
		playManager.PlaybackStopped += (s, e) => GenerateStatusImage(false, null);
		// Stats
		playManager.AfterResourceStarted += (s, e) =>
		{
			stats.TrackSongStart(Id, e.ResourceData.AudioType);
			return Task.CompletedTask;
		};
		playManager.ResourceStopped += (s, e) =>
		{
			stats.TrackSongStop(Id);
			return Task.CompletedTask;
		};
		// Frix: kanala renkli bildirimler
		playManager.AfterResourceStarted += (s, e) =>
		{
			var title = e.ResourceData.ResourceTitle ?? "?";
			var who = FrixChat.NameOf(e.EffectiveOwnerUid);
			var isRadio = e.ResourceData.Get("radio") != null;
			var key = isRadio ? "nowplaying.radio" : "nowplaying";
			// Müzik / Radyo eklentilerindeki "kanala duyur" ayarı kapalıysa mesaj gönderilmez
			if (config.Name is { } an && (isRadio
				? Frix.FrixPluginHost.Enabled<Frix.RadioPlugin>(an) is { Announce: false }
				: Frix.FrixPluginHost.Enabled<Frix.MusicPlugin>(an) is { Announce: false }))
				return Task.CompletedTask;
			return AnnounceChannel(FrixChat.Color(FrixText.Get(key, ("title", title), ("user", who))));
		};
		playManager.AfterResourceStarted += (s, e) =>
		{
			var res = e.ResourceData;
			var owner = e.EffectiveOwnerUid;
			Frix.FrixData.RecordPlay(config.Name, owner.Value, FrixChat.NameOf(owner), res.AudioType, res.ResourceId, res.ResourceTitle);
			var host = plugins;
			if (host != null) _ = Task.Run(() => host.SongStarted());
			return Task.CompletedTask;
		};
		playManager.SongFailed += (item, reason) =>
			AnnounceChannel(FrixChat.Color(FrixText.Get("failed", ("title", item.AudioResource.ResourceTitle ?? "?"), ("reason", reason)), FrixChat.Red));
		player.FfmpegProducer.OnFfmpegFailed += lines =>
		{
			var shortText = lines.Length > 200 ? lines[^200..] : lines;
			_ = Scheduler.InvokeAsync(() => AnnounceChannel(FrixChat.Color(FrixText.Get("stream.error", ("reason", shortText)), FrixChat.Red)));
		};

		// Frix eklentileri
		plugins = new Frix.FrixPluginHost(new Frix.PluginContext
		{
			BotName = config.Name ?? $"bot{id}",
			Client = ts3client,
			Full = ts3FullClient,
			PlayManager = playManager,
			Player = player,
			Playlist = Injector.GetModuleOrThrow<PlaylistManager>(),
			RunOnBot = f => Scheduler.InvokeAsync(f),
			BaseNickname = () => config.Connect.Name.Value,
			RefreshAvatar = () => GenerateStatusImage(playManager.IsPlaying, playManager.CurrentPlayData),
		});
		// Frix: yeniden bağlanma eklentisi (kapalıysa bot düşünce yeniden bağlanmaz)
		ts3client.FrixReconnect = (type, attempt) =>
		{
			if (config.Name is not { } bn || Frix.FrixPluginHost.For(bn) is null) return (false, null);
			var rp = Frix.FrixPluginHost.Enabled<Frix.AutoReconnectPlugin>(bn);
			return (true, rp?.Delay(type, attempt));
		};
		ts3client.FrixSameChannel = () => config.Name is not { } bn2 || Frix.FrixPluginHost.Enabled<Frix.AutoReconnectPlugin>(bn2) is not { SameChannel: false };
		ts3FullClient.OnEachClientEnterView += (s, e) =>
		{
			var host = plugins;
			if (host != null)
				_ = Task.Run(() => host.ClientEntered(e.ClientId, e.Name, e.Uid, e.SourceChannelId, e.TargetChannelId, (int)e.Reason, e.ClientType != ClientType.Full));
		};
		ts3FullClient.OnEachClientMoved += (s, e) =>
		{
			var host = plugins;
			if (host != null)
				_ = Task.Run(() => host.ClientMoved(e.ClientId, e.TargetChannelId));
		};
		ts3client.OnAloneChanged += (s, e) =>
		{
			var host = plugins;
			if (host != null)
				_ = Task.Run(() => host.AloneChanged(e.Alone));
			return Task.CompletedTask;
		};
		playManager.PlaybackStopped += (s, e) =>
		{
			var host = plugins;
			if (host != null)
				_ = Task.Run(() => host.PlaybackStopped());
			return Task.CompletedTask;
		};
		pluginTimer = new System.Threading.Timer(_ =>
		{
			var host = plugins;
			if (host != null)
				_ = Task.Run(() => host.Tick());
		}, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

		// Register callback for all messages happening
		ts3client.OnMessageReceived += OnMessageReceived;
		// Register callback to remove open private sessions, when user disconnects
		ts3FullClient.OnEachClientLeftView += OnClientLeftView;
		ts3client.OnBotConnected += OnBotConnected;
		ts3client.OnBotDisconnected += OnBotDisconnected;
		ts3client.OnBotStoppedReconnecting += OnBotStoppedReconnecting;
		// Alone mode
		ts3client.OnAloneChanged += OnAloneChanged;
		ts3client.OnAloneChanged += (s, e) =>
		{
			customTarget.Alone = e.Alone;
			return Task.CompletedTask;
		};
		// Whisper stall
		ts3client.OnWhisperNoTarget += (s, e) => player.SetStall();

		commandManager.RegisterCollection(MainCommands.Bag);
		// TODO remove after plugin rework
		var pluginManager = Injector.GetModuleOrThrow<PluginManager>();
		foreach (var plugin in pluginManager.Plugins)
			if (plugin.Type == PluginType.CorePlugin || plugin.Type == PluginType.Commands)
				commandManager.RegisterCollection(plugin.CorePlugin.Bag);

		// Add global aliases
		foreach (var alias in config.GetParent().Bot.Commands.Alias.GetAllItems())
			commandManager.RegisterAlias(alias.Key, alias.Value).UnwrapToLog(Log);

		// Restore bot-aliases from the config
		// Frix: eski sürümden kalan Türkçe kısayollar (gec, dur...) artık gerçek komut; çakışanları temizle
		var obsolete = config.Commands.Alias.GetAllItems()
			.Where(a => Frix.FrixCommands.Builtins.Any(b => b.name == a.Key)).Select(a => a.Key).ToList();
		foreach (var key in obsolete)
			config.Commands.Alias.RemoveItem(key);
		if (obsolete.Count > 0)
		{
			config.SaveWhenExists();
			Log.Info("Eski kısayollar kaldırıldı (artık yerleşik komut): {0}", string.Join(", ", obsolete));
		}
		foreach (var alias in config.Commands.Alias.GetAllItems())
			commandManager.RegisterAlias(alias.Key, alias.Value).UnwrapToLog(Log);
	}

	public Task<E<string>> Run()
	{
		Scheduler.VerifyOwnThread();

		Log.Info("Bot \"{0}\" connecting to \"{1}\"", config.Name, config.Connect.Address);
		return Task.FromResult(ts3client.Connect());
	}

	public async Task Stop()
	{
		Scheduler.VerifyOwnThread();

		Injector.GetModule<BotManager>()?.RemoveBot(this);

		if (!isClosed) isClosed = true;
		else return;

		Log.Info("Bot ({0}) disconnecting.", Id);

		DisableIdleTickWorker();
		pluginTimer?.Dispose();
		pluginTimer = null;
		if (plugins != null)
			Frix.FrixPluginHost.Remove(plugins.Ctx.BotName, plugins);
		plugins = null;

		Injector.GetModule<PluginManager>()?.StopPlugins(this);
		Injector.GetModule<PlayManager>()?.Stop();
		Injector.GetModule<Player>()?.Dispose();
		var tsClient = Injector.GetModule<Ts3Client>();
		if (tsClient != null)
			await tsClient.Disconnect();
		Injector.GetModule<DedicatedTaskScheduler>()?.Dispose();
		config.ClearEvents();
	}

	private async Task OnBotConnected(object? sender, EventArgs e)
	{
		EnableIdleTickWorker();
		plugins?.OnConnected();

		var badges = config.Connect.Badges.Value;
		if (!string.IsNullOrEmpty(badges))
			ts3client?.ChangeBadges(badges);

		if (config.Connect.Commander.Value)
		{
			try { await ts3client!.SetChannelCommander(true); }
			catch (Exception ex) { Log.Warn("Channel commander açılamadı (sunucuda 'b_client_use_channel_commander' yetkisi gerekebilir): {0}", ex.Message); }
		}

		var onStart = config.Events.OnConnect.Value;
		if (!string.IsNullOrEmpty(onStart))
		{
			var info = CreateExecInfo();
			await CallScript(info, onStart, false, true);
		}
	}

	private async Task OnBotDisconnected(object? sender, DisconnectEventArgs e)
	{
		DisableIdleTickWorker();
		plugins?.OnDisconnected();

		var onStop = config.Events.OnDisconnect.Value;
		if (!string.IsNullOrEmpty(onStop))
		{
			var info = CreateExecInfo();
			await CallScript(info, onStop, false, true);
		}
	}

	private Task OnBotStoppedReconnecting(object? sender, EventArgs e)
	{
		return Stop();
	}

	private async Task OnMessageReceived(object? sender, TextMessage textMessage)
	{
		if (textMessage?.Message == null)
		{
			Log.Warn("Invalid TextMessage: {@textMessage}", textMessage);
			return;
		}

		Log.Debug("TextMessage: {@textMessage}", textMessage);

		var message = textMessage.Message.Trim();
		if (plugins is { } fhost && textMessage.InvokerId != ts3FullClient.ClientId)
		{
			var tm = textMessage;
			_ = Task.Run(() => fhost.TextMessage(tm.InvokerId, tm.InvokerUid ?? Uid.Null, tm.InvokerName, tm.Message));
		}
		if (!(message.Length >= 2 && message[0] == '!' && char.IsLetter(message[1])))
			return;
		var rewritten = Frix.FrixCommands.Rewrite(message);
		if (rewritten is null)
			return;
		textMessage.Message = rewritten;

		if (!localization.LanguageLoaded)
		{
			var langResult = await localization.LoadLanguage(config.Language, false);
			if (!langResult.Ok)
				Log.Error("Failed to load language file ({0})", langResult.Error);
		}

		localization.ApplyLanguage();

		Log.Info("{InvokerName}: {Message}", textMessage.InvokerName, textMessage.Message);
		FrixChat.RememberName(textMessage.InvokerUid, textMessage.InvokerName);

		ts3client.InvalidateClientBuffer();

		ChannelId? channelId = null;
		ClientDbId? databaseId = null;
		ChannelGroupId? channelGroup = null;
		ServerGroupId[]? serverGroups = null;

		if (ts3FullClient.Book.Clients.TryGetValue(textMessage.InvokerId, out var bookClient))
		{
			channelId = bookClient.Channel;
			databaseId = bookClient.DatabaseId;
			serverGroups = bookClient.ServerGroups.ToArray();
			channelGroup = bookClient.ChannelGroup;
		}
		else if ((await ts3FullClient.ClientInfo(textMessage.InvokerId)).Get(out var infoClient,
			out var infoClientError))
		{
			channelId = infoClient.ChannelId;
			databaseId = infoClient.DatabaseId;
			serverGroups = infoClient.ServerGroups;
			channelGroup = infoClient.ChannelGroup;
		}
		else
		{
			try
			{
				var cachedClient = await ts3client.GetCachedClientById(textMessage.InvokerId);
				channelId = cachedClient.ChannelId;
				databaseId = cachedClient.DatabaseId;
				channelGroup = cachedClient.ChannelGroup;
			}
			catch (AudioBotException cachedClientError)
			{
				Log.Warn(cachedClientError,
					"The bot is missing teamspeak permissions to view the communicating client. " +
					"Some commands or permission checks might not work " +
					"(clientlist:{0}, clientinfo:{1}).",
					cachedClientError.Message, infoClientError.ErrorFormat());
			}
		}

		var invoker = new ClientCall(textMessage.InvokerUid ?? Uid.Anonymous, textMessage.Message,
			clientId: textMessage.InvokerId,
			visibility: textMessage.Target,
			nickName: textMessage.InvokerName,
			channelId: channelId,
			databaseId: databaseId,
			serverGroups: serverGroups,
			channelGroup: channelGroup);

		var session = sessionManager.GetOrCreateSession(textMessage.InvokerId);
		var info = CreateExecInfo(invoker, session);

		// Frix: bağlı eklentisi kapalı yerleşik komutlar
		{
			var msg = textMessage.Message;
			var sp = msg.IndexOf(' ');
			var cmdName = sp < 0 ? msg[1..] : msg[1..sp];
			if (!Frix.FrixCommands.IsAvailable(cmdName, config.Name))
			{
				await info.Write(FrixChat.Colorize(FrixText.Get("plugin.off"))).CatchToLog(Log);
				return;
			}
		}

		// check if the user has an open request
		if (session.ResponseProcessor != null)
		{
			await TryCatchCommand(info, answer: true, async () =>
			{
				var msg = await session.ResponseProcessor(textMessage.Message);
				if (!string.IsNullOrEmpty(msg))
					await info.Write(msg).CatchToLog(Log);
			});
			session.ClearResponse();
			return;
		}

		await CallScript(info, textMessage.Message, answer: true, false);
	}

	private void OnClientLeftView(object? sender, ClientLeftView eventArgs)
	{
		targetManager.WhisperClientUnsubscribe(eventArgs.ClientId);
		sessionManager.RemoveSession(eventArgs.ClientId);
	}

	private async Task BeforeResourceStarted(object? sender, PlayInfoEventArgs e)
	{
		const string DefaultVoiceScript = "!whisper off";
		const string DefaultWhisperScript =
			"!xecute (!whisper subscription) (!unsubscribe temporary) (!subscribe channeltemp (!getmy channel))";

		var mode = config.Audio.SendMode.Value;
		string script;
		if (mode.StartsWith('!'))
			script = mode;
		else if (mode.Equals("voice", StringComparison.OrdinalIgnoreCase))
			script = DefaultVoiceScript;
		else if (mode.Equals("whisper", StringComparison.OrdinalIgnoreCase))
			script = DefaultWhisperScript;
		else
		{
			Log.Error("Invalid voice mode");
			return;
		}

		var info = CreateExecInfo(e.Invoker);
		await CallScript(info, script, false, true);
	}

	private async Task AfterResourceStarted(object? sender, PlayInfoEventArgs e)
	{
		var onSongStart = config.Events.OnSongStart.Value;
		if (!string.IsNullOrEmpty(onSongStart))
		{
			var info = CreateExecInfo();
			await CallScript(info, onSongStart, false, true);
		}
	}

	private async Task AnnounceChannel(string message)
	{
		try
		{
			if (!isClosed && ts3FullClient.Connected)
				await ts3client.SendChannelMessage(message);
		}
		catch (Exception ex)
		{
			Log.Debug(ex, "Kanal mesajı gönderilemedi");
		}
	}

	#region Status: Description, Avatar

	public Task UpdateBotStatus()
	{
		return Scheduler.InvokeAsync(UpdateBotStatusInternal);
	}

	private async Task UpdateBotStatusInternal()
	{
		Scheduler.VerifyOwnThread();

		if (isClosed || !ts3FullClient.Connected)
			return;

		if (!config.SetStatusDescription)
			return;
		// Frix: "şarkı adı → açıklama" eklentisi açıksa açıklamayı o yönetir
		if (config.Name is { } fn && Frix.FrixPluginHost.Enabled<Frix.NowPlayingStatusPlugin>(fn) is { HandlesDescription: true })
			return;

		string? setString;
		if (playManager.IsPlaying)
		{
			setString = QuizMode
				? strings.info_botstatus_quiztime
				: playManager.CurrentPlayData?.ResourceData?.ResourceTitle;
		}
		else
		{
			setString = strings.info_botstatus_sleeping;
		}

		var result = await ts3FullClient.ChangeDescription(setString ?? "");
		result.UnwrapToLog(Log);
	}

	/// <summary>Frix: ayarlar değişince boştaki avatarı yeniler.</summary>
	public Task RefreshIdleAvatar()
		=> Scheduler.InvokeAsync(async () =>
		{
			if (!isClosed && Injector.TryGet<PlayManager>(out var pm) && ts3FullClient.Connected)
				await GenerateStatusImageInternal(pm.IsPlaying, pm.CurrentPlayData);
		});

	public Task GenerateStatusImage(bool playing, PlayInfoEventArgs? startEvent)
	{
		return Scheduler.InvokeAsync(() => GenerateStatusImageInternal(playing, startEvent));
	}

	private async Task GenerateStatusImageInternal(bool playing, PlayInfoEventArgs? startEvent)
	{
		Scheduler.VerifyOwnThread();

		if (!config.GenerateStatusAvatar || isClosed || !ts3FullClient.Connected)
			return;

		static Stream? GetRandomFile(string? basePath, string prefix)
		{
			try
			{
				if (string.IsNullOrEmpty(basePath))
					return null;
				var avatarPath = new DirectoryInfo(Path.Combine(basePath, BotPaths.Avatars));
				if (!avatarPath.Exists)
					return null;
				var avatars = avatarPath.EnumerateFiles(prefix).ToArray();
				if (avatars.Length == 0)
					return null;
				var pickedAvatar = Tools.PickRandom(avatars);
				return pickedAvatar.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
			}
			catch (Exception ex)
			{
				Log.Warn(ex, "Failed to load local avatar");
				return null;
			}
		}

		using var cts = new CancellationTokenSource();
		avatarRequest?.Cancel();
		avatarRequest = cts;

		// Frix: avatar eklentisi kapalıysa avatar kaldırılır; seçeneklere göre kapak / komut kartı
		var avatarPlugin = config.Name is { } an ? Frix.FrixPluginHost.Enabled<Frix.AvatarPlugin>(an) : null;
		if (plugins != null && avatarPlugin is null)
		{
			if (!playing) (await ts3FullClient.DeleteAvatar()).UnwrapToLog(Log);
			if (avatarRequest == cts) avatarRequest = null;
			return;
		}
		// Frix: her durum için ayrı seçim: boşta (kart / resim / yok), müzik (kapak / resim / boştaki), radyo (boştaki / resim)
		var isRadio = startEvent?.ResourceData.Get("radio") != null;
		Stream? setStream = null;
		if (playing && avatarPlugin != null)
		{
			var mode = isRadio ? avatarPlugin.RadioMode : avatarPlugin.MusicMode;
			if (mode == "custom") setStream = Frix.FrixAvatar.CustomStream(config.Name, isRadio ? "radio" : "music");
			if (setStream != null) goto upload;
			if (mode != "cover") playing = false; // "boştaki" seçili ya da resim yüklenmemiş: boştaki avatar
		}
		if (!playing && avatarPlugin != null)
		{
			switch (avatarPlugin.IdleMode)
			{
			case "none": setStream = null; goto upload;
			case "custom": setStream = Frix.FrixAvatar.CustomStream(config.Name, "idle"); break;
			}
			if (setStream != null) goto upload;
		}

		if (playing)
		{
			if (startEvent != null && !QuizMode)
			{
				try
				{
					await resourceResolver.GetThumbnail(startEvent.PlayResource,
						async (thumbStream, ct) =>
							setStream = (await ImageUtil.ResizeImageSave(thumbStream, ct)).Stream, cts.Token);
				}
				catch (AudioBotException ex) { Log.Debug(ex, "Failed to fetch thumbnail image"); }
				catch (OperationCanceledException) { return; }
			}

			setStream ??= GetRandomFile(config.LocalConfigDir, "play*");
			// Frix: nothing with a cover → the idle avatar instead of removing it
			if (setStream == null && avatarPlugin != null)
				setStream = avatarPlugin.IdleMode switch
				{
					"custom" => Frix.FrixAvatar.CustomStream(config.Name, "idle") ?? Frix.FrixAvatar.IdleStream(),
					"none" => null,
					_ => Frix.FrixAvatar.IdleStream(),
				};
			else if (setStream == null && avatarPlugin is null)
				setStream = Frix.FrixAvatar.IdleStream();
		}
		else
		{
			setStream ??= GetRandomFile(config.LocalConfigDir, "sleep*");
			{
				setStream ??= Frix.FrixAvatar.IdleStream();
				setStream ??= Util.GetEmbeddedFile("TS3AudioBot.Assets.SleepingKitty.png");
			}
		}

	upload:
		if (setStream != null)
		{
			try
			{
				using (setStream)
				{
					var result = await ts3FullClient.UploadAvatar(setStream, cts.Token);
					if (!result.Ok)
						Log.Warn("Avatar yüklenemedi: {0} — botun sunucu grubundaki avatar boyut sınırı (i_client_max_avatar_filesize) düşük olabilir; eski avatar yerinde kalır.",
							result.Error.ErrorFormat());
				}
			}
			catch (Exception ex)
			{
				Log.Warn(ex, "Could not change avatar");
			}
		}
		else
		{
			var result = await ts3FullClient.DeleteAvatar();
			result.UnwrapToLog(Log);
		}

		if (avatarRequest == cts) avatarRequest = null;
	}

	#endregion

	#region Script Execution

	private async Task CallScript(ExecutionInformation info, string command, bool answer, bool skipRights)
	{
		Log.Debug("Calling script (skipRights:{0}, answer:{1}): {2}", skipRights, answer, command);
		stats.TrackCommandCall(answer);

		info.AddModule(new CallerInfo(false)
		{
			SkipRightsChecks = skipRights,
			CommandComplexityMax = config.Commands.CommandComplexity,
			IsColor = config.Commands.Color,
		});

		await TryCatchCommand(info, answer, async () =>
		{
			// parse and execute the command
			var res = await CommandManager.Execute(info, command);

			if (!answer)
				return;

			// Write result to user
			var s = res.AsString();
			if (!string.IsNullOrEmpty(s))
				await info.Write(s).CatchToLog(Log);
		});
	}

	private ExecutionInformation CreateExecInfo(InvokerData? invoker = null, UserSession? session = null)
	{
		var info = new ExecutionInformation(Injector);
		if (invoker is ClientCall ci)
			info.AddModule(ci);
		info.AddModule(invoker ?? InvokerData.Anonymous);
		info.AddModule(session ?? new AnonymousSession());
		info.AddModule(Filter.GetFilterByNameOrDefault(config.Commands.Matcher));
		return info;
	}

	private async Task TryCatchCommand(ExecutionInformation info, bool answer, Func<Task> action)
	{
		try
		{
			await action.Invoke();
		}
		catch (AudioBotException ex)
		{
			NLog.LogLevel commandErrorLevel = answer ? NLog.LogLevel.Debug : NLog.LogLevel.Warn;
			Log.Log(commandErrorLevel, ex, "Command Error ({0})", ex.Message);
			if (answer)
			{
				await info.Write(TextMod.Format(config.Commands.Color,
						strings.error_call_error.Mod().Color(Color.Red).Bold(), ex.Message))
					.CatchToLog(Log);
			}
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Unexpected command error: {0}", ex.Message);
			if (answer)
			{
				await info.Write(TextMod.Format(config.Commands.Color,
						strings.error_call_unexpected_error.Mod().Color(Color.Red).Bold(), ex.Message))
					.CatchToLog(Log);
			}
		}
	}

	#endregion

	#region Event: Idle

	private async void OnIdle()
	{
		// DisableIdleTickWorker(); // fire once only ??

		var onIdle = config.Events.OnIdle.Value;
		if (!string.IsNullOrEmpty(onIdle))
		{
			var info = CreateExecInfo();
			await CallScript(info, onIdle, false, true);
		}
	}

	private void EnableIdleTickWorker()
	{
		var idleTime = config.Events.IdleDelay.Value;
		if (idleTime <= TimeSpan.Zero || string.IsNullOrEmpty(config.Events.OnIdle.Value))
		{
			DisableIdleTickWorker();
			return;
		}

		idleTickWorker.Interval = idleTime;
		idleTickWorker.Enable();
	}

	private void DisableIdleTickWorker() => idleTickWorker.Disable();

	#endregion

	#region Event: Alone/Party

	private async Task OnAloneChanged(object? sender, AloneChanged e)
	{
		Scheduler.VerifyOwnThread();

		string script;
		TimeSpan delay;
		if (e.Alone)
		{
			script = config.Events.OnAlone.Value;
			delay = config.Events.AloneDelay.Value;
		}
		else
		{
			script = config.Events.OnParty.Value;
			delay = config.Events.PartyDelay.Value;
		}

		if (string.IsNullOrEmpty(script))
			return;

		if (delay > TimeSpan.Zero) // TODO: Async (Add cancellation token for better consistency)
			await Task.Delay(delay);

		var info = CreateExecInfo();
		await CallScript(info, script, false, true);
	}

	#endregion

	public BotInfo GetInfo() => new()
	{
		Id = Id,
		Name = config.Name,
		Server = ts3FullClient.ConnectionData?.Address,
		Status = ts3FullClient.Connected ? BotStatus.Connected : BotStatus.Connecting,
	};
}

public class BotInfo
{
	public int? Id { get; set; }
	public string? Name { get; set; }
	public string? Server { get; set; }
	public BotStatus Status { get; set; }

	public override string ToString() => $"Id: {Id} Name: {Name} Server: {Server} Status: {Status}"; // LOC: TODO
}

public enum BotStatus
{
	Offline,
	Connecting,
	Connected,
}
