// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TS3AudioBot.Config;
using TS3AudioBot.Helper;
using TSLib.Audio;
using TSLib.Helper;
using TSLib.Scheduler;

namespace TS3AudioBot.Audio;

public sealed partial class FfmpegProducer : IPlayerSource, IDisposable
{
	private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
	private readonly Id id;

	[GeneratedRegex(@"^\s*Duration: (\d+):(\d\d):(\d\d).(\d\d)", RegexOptions.IgnoreCase)]
	private static partial Regex FindDurationMatch { get; }
	[GeneratedRegex(@"((\w+)='(.*?)';\s*)+", RegexOptions.IgnoreCase | RegexOptions.ECMAScript)]
	private static partial Regex IcyMetadataMacher { get; }

	private const string PreLinkConf = "-hide_banner -nostats -threads 1 -i \"";
	/// <summary>Frix: ffmpeg ses filtresi (örn. loudnorm). Bir sonraki şarkıdan itibaren uygulanır.</summary>
	public string? AudioFilter { get; set; }
	private string Af => string.IsNullOrWhiteSpace(AudioFilter) ? "" : " -af \"" + AudioFilter.Replace("\"", "") + "\"";
	private string PostLinkConf => "\"" + Af + " -ac 2 -ar 48000 -f s16le -acodec pcm_s16le pipe:1";
	private string LinkConfIcy => "-hide_banner -nostats -threads 1 -i pipe:0" + Af + " -ac 2 -ar 48000 -f s16le -acodec pcm_s16le pipe:1";
	private static readonly TimeSpan retryOnDropBeforeEnd = TimeSpan.FromSeconds(10);

	private readonly ConfToolsFfmpeg config;

	public event EventHandler? OnSongEnd;
	public event Action<string>? OnFfmpegFailed;
	public event EventHandler<SongInfoChanged>? OnSongUpdated;

	private readonly DedicatedTaskScheduler scheduler;
	private FfmpegInstance? ffmpegInstance;
	public SampleInfo SampleInfo { get; } = SampleInfo.OpusMusic;

	public FfmpegProducer(ConfToolsFfmpeg config, DedicatedTaskScheduler scheduler, Id id)
	{
		this.config = config;
		this.scheduler = scheduler;
		this.id = id;
	}

	public Task AudioStart(string url, TimeSpan? startOff = null)
	{
		StartFfmpegProcess(url, startOff ?? TimeSpan.Zero);
		return Task.CompletedTask;
	}

	public async Task AudioStartIcy(string url)
	{
		if (!(await StartFfmpegProcessIcy(url)).Get(out _, out var error))
		{
			Log.Warn("Failed to start icy stream: {0}", error);
		}
	}

	public void AudioStop()
	{
		StopFfmpegProcess();
	}

	public TimeSpan? Length => GetCurrentSongLength();

	public TimeSpan? Position => ffmpegInstance?.AudioTimer.SongPosition;

	public Task Seek(TimeSpan position) { SetPosition(position); return Task.CompletedTask; }

	public int Read(Span<byte> data, out Meta? meta)
	{
		meta = default;
		int read;

		var instance = ffmpegInstance;

		if (instance is null)
			return 0;

		try
		{
			read = instance.FfmpegProcess.StandardOutput.BaseStream.Read(data);
		}
		catch (Exception ex)
		{
			read = 0;
			Log.Debug(ex, "Can't read ffmpeg");
		}

		if (read == 0)
		{
			AssertNotMainScheduler();

			var (ret, triggerEndSafe) = instance.IsIcyStream
				? OnReadEmptyIcy(instance)
				: OnReadEmpty(instance);
			if (ret)
				return 0;

			if (instance.FfmpegProcess.HasExitedSafe())
			{
				Log.Trace("Ffmpeg has exited");
				try
				{
					if (instance.Feeder is { } fd && !instance.FeederGotData)
					{
						// yt-dlp gave ffmpeg nothing: its own error is the real reason ("Invalid data" means nothing)
						try { fd.WaitForExit(3000); } catch { }
						if (instance.FeederFailed)
							goto skipFfmpegError;
					}
					if (instance.FfmpegProcess.ExitCode != 0 && !instance.FeederFailed)
					{
						string lines;
						lock (instance.LastErrorLines)
							lines = string.Join(" | ", instance.LastErrorLines);
						if (instance.Feeder != null && !instance.FeederGotData && instance.FeederStderr is { } fe)
						{
							string err;
							lock (fe) err = fe.ToString();
							if (err.Trim().Length > 0) lines = ResourceFactories.Youtube.YtdlpStream.ExplainError(err);
							instance.FeederFailed = true;
						}
						Log.Warn("ffmpeg şarkıyı çalamadı (çıkış kodu {0}): {1}", instance.FfmpegProcess.ExitCode, lines);
						OnFfmpegFailed?.Invoke(lines);
					}
				}
				catch (Exception ex) { Log.Debug(ex, "ffmpeg exit code"); }
				skipFfmpegError:
				AudioStop();
				triggerEndSafe = true;
			}

			if (triggerEndSafe)
			{
				OnSongEnd?.Invoke(this, EventArgs.Empty);
				return 0;
			}
		}

		instance.HasTriedToReconnect = false;
		instance.AudioTimer.PushBytes(read);
		return read;
	}

	private (bool ret, bool trigger) OnReadEmpty(FfmpegInstance instance)
	{
		if (instance.FfmpegProcess.HasExitedSafe() && !instance.HasTriedToReconnect)
		{
			var expectedStopLength = GetCurrentSongLength();
			Log.Trace("Expected song length {0}", expectedStopLength);
			if (expectedStopLength != TimeSpan.Zero)
			{
				var actualStopPosition = instance.AudioTimer.SongPosition;
				Log.Trace("Actual song position {0}", actualStopPosition);
				if (actualStopPosition + retryOnDropBeforeEnd < expectedStopLength)
				{
					Log.Debug("Connection to song lost, retrying at {0}", actualStopPosition);
					instance.HasTriedToReconnect = true;
					if (SetPosition(actualStopPosition).Get(out var newInstance, out var error))
					{
						newInstance.HasTriedToReconnect = true;
						return (true, false);
					}
					else
					{
						Log.Debug("Retry failed {0}", error);
						return (false, true);
					}
				}
			}
		}
		return (false, false);
	}

	private (bool ret, bool trigger) OnReadEmptyIcy(FfmpegInstance instance)
	{
		AssertNotMainScheduler();

		if (instance.FfmpegProcess.HasExitedSafe() && !instance.HasTriedToReconnect)
		{
			Log.Debug("Connection to stream lost, retrying...");
			instance.HasTriedToReconnect = true;
			var newInstance = StartFfmpegProcessIcy(instance.ReconnectUrl).Result;
			if (newInstance.Ok)
			{
				newInstance.Value.HasTriedToReconnect = true;
				return (true, false);
			}
			else
			{
				Log.Debug("Retry failed {0}", newInstance.Error);
				return (false, true);
			}
		}
		return (false, false);
	}

	private R<FfmpegInstance, string> SetPosition(TimeSpan value)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);

		var instance = ffmpegInstance;
		if (instance is null)
			return "No instance running";
		if (instance.IsIcyStream)
			return "Cannot seek icy stream";
		var lastLink = instance.ReconnectUrl;
		if (lastLink is null)
			return "No current url active";
		var knownLength = instance.ParsedSongLength;
		var result = StartFfmpegProcess(lastLink, value);
		if (result.Ok && result.Value.ParsedSongLength is null)
			result.Value.ParsedSongLength = knownLength;
		return result;
	}

	private R<FfmpegInstance, string> StartFfmpegProcess(string url, TimeSpan? offsetOpt)
	{
		if (ResourceFactories.Youtube.YtdlpStream.IsYtdlpUri(url))
			return StartYtdlpPipe(url, offsetOpt ?? TimeSpan.Zero);

		StopFfmpegProcess();
		Log.Trace("Start request {0}", url);

		string arguments;
		var offset = offsetOpt ?? TimeSpan.Zero;
		if (offset > TimeSpan.Zero)
		{
			var seek = string.Format(CultureInfo.InvariantCulture, @"-ss {0:hh\:mm\:ss\.fff}", offset);
			arguments = string.Concat(seek, " ", PreLinkConf, url, PostLinkConf, " ", seek);
		}
		else
		{
			arguments = string.Concat(PreLinkConf, url, PostLinkConf);
		}

		var newInstance = new FfmpegInstance(
			url,
			new PreciseAudioTimer(SampleInfo)
			{
				SongPositionOffset = offset,
			});

		return StartFfmpegProcessInternal(newInstance, arguments);
	}

	private string PipeConf => "-hide_banner -nostats -threads 1 -i pipe:0" + Af + " -ac 2 -ar 48000 -f s16le -acodec pcm_s16le pipe:1";

	/// <summary>Frix: yt-dlp çıktısını ffmpeg'e borudan verir (indirmesiz, canlı yayın destekli).</summary>
	private R<FfmpegInstance, string> StartYtdlpPipe(string uri, TimeSpan offset)
	{
		StopFfmpegProcess();
		var target = ResourceFactories.Youtube.YtdlpStream.TargetOf(uri);
		var metaFile = Path.Combine(Path.GetTempPath(), $"frix_meta_{id}_{Guid.NewGuid():N}.txt");

		var arguments = offset > TimeSpan.Zero
			? string.Format(CultureInfo.InvariantCulture, @"-ss {0:hh\:mm\:ss\.fff} ", offset) + PipeConf
			: PipeConf;

		var instance = new FfmpegInstance(uri, new PreciseAudioTimer(SampleInfo) { SongPositionOffset = offset })
		{
			OnMetaUpdated = e => OnSongUpdated?.Invoke(this, e),
			MetaFile = metaFile,
		};

		var started = StartFfmpegProcessInternal(instance, arguments);
		if (!started.Ok)
			return started;

		Process feeder;
		try
		{
			feeder = ResourceFactories.Youtube.YtdlpStream.Start(target, metaFile);
		}
		catch (Exception ex)
		{
			Log.Error(ex, "yt-dlp başlatılamadı");
			instance.Close();
			OnFfmpegFailed?.Invoke("yt-dlp başlatılamadı: " + ex.Message);
			return "yt-dlp başlatılamadı: " + ex.Message;
		}
		instance.Feeder = feeder;

		var stderr = new StringBuilder();
		instance.FeederStderr = stderr;
		feeder.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (stderr) stderr.AppendLine(e.Data); };
		feeder.BeginErrorReadLine();

		new Thread(() =>
		{
			var buffer = new byte[64 * 1024];
			var metaSent = false;
			try
			{
				var input = feeder.StandardOutput.BaseStream;
				var output = instance.FfmpegProcess.StandardInput.BaseStream;
				int n;
				while (!instance.Closed && (n = input.Read(buffer, 0, buffer.Length)) > 0)
				{
					output.Write(buffer, 0, n);
					instance.FeederGotData = true;
					if (!metaSent)
					{
						metaSent = true;
						var meta = ResourceFactories.Youtube.YtdlpStream.ReadMeta(metaFile);
						if (meta != null)
						{
							if (meta.Duration.HasValue && !meta.IsLive)
								instance.ParsedSongLength = meta.Duration;
							if (!string.IsNullOrEmpty(meta.Title))
								instance.OnMetaUpdated?.Invoke(new SongInfoChanged { Title = meta.Title, Length = meta.IsLive ? null : meta.Duration });
						}
					}
				}
			}
			catch (Exception ex) when (instance.Closed || ex is IOException or ObjectDisposedException)
			{
				Log.Trace(ex, "yt-dlp borusu kapandı");
			}
			catch (Exception ex)
			{
				Log.Debug(ex, "yt-dlp borusu hatası");
			}
			finally
			{
				try { instance.FfmpegProcess.StandardInput.Close(); } catch { }
			}

			try
			{
				if (!feeder.WaitForExit(5000)) return;
				if (!instance.Closed && feeder.ExitCode != 0)
				{
					string err;
					lock (stderr) err = stderr.ToString();
					var reason = ResourceFactories.Youtube.YtdlpStream.ExplainError(err);
					Log.Warn("yt-dlp akışı başarısız ({0}): {1}", feeder.ExitCode, reason);
					instance.FeederFailed = true;
					if (!metaSent)
						OnFfmpegFailed?.Invoke(reason);
				}
			}
			catch { }
		})
		{
			Name = $"YtdlpPipe[{id}]",
			IsBackground = true,
		}.Start();

		return instance;
	}

	private async Task<R<FfmpegInstance, string>> StartFfmpegProcessIcy(string url)
	{
		StopFfmpegProcess();
		Log.Trace("Start icy-stream request {0}", url);

		try
		{
			var response = await WebWrapper
				.Request(url)
				.WithHeader("Icy-MetaData", "1")
				.UnsafeResponse();

			if (!int.TryParse(response.Headers.GetSingle("icy-metaint"), out var metaint))
			{
				response.Dispose();
				return "Invalid icy stream tags";
			}

			var stream = await response.Content.ReadAsStreamAsync();
			var newInstance = new FfmpegInstance(
				url,
				new PreciseAudioTimer(SampleInfo),
				stream,
				metaint)
			{
				OnMetaUpdated = e => OnSongUpdated?.Invoke(this, e)
			};

			new Thread(() => newInstance.ReadStreamLoop(id))
			{
				Name = $"IcyStreamReader[{id}]",
			}.Start();

			return StartFfmpegProcessInternal(newInstance, LinkConfIcy);
		}
		catch (Exception ex)
		{
			var error = $"Unable to create icy-stream ({ex.Message})";
			Log.Warn(ex, error);
			return error;
		}
	}

	private R<FfmpegInstance, string> StartFfmpegProcessInternal(FfmpegInstance instance, string arguments)
	{
		try
		{
			instance.FfmpegProcess.StartInfo = new ProcessStartInfo
			{
				FileName = Frix.FrixTools.ResolveFfmpeg(config.Path.Value),
				Arguments = arguments,
				RedirectStandardOutput = true,
				RedirectStandardInput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			};
			instance.FfmpegProcess.EnableRaisingEvents = true;

			Log.Debug("Starting ffmpeg with {0}", arguments);
			instance.FfmpegProcess.ErrorDataReceived += instance.FfmpegProcess_ErrorDataReceived;
			instance.FfmpegProcess.Start();
			instance.FfmpegProcess.BeginErrorReadLine();

			instance.AudioTimer.Start();

			var oldInstance = Interlocked.Exchange(ref ffmpegInstance, instance);
			oldInstance?.Close();

			return instance;
		}
		catch (Exception ex)
		{
			var error = ex is Win32Exception
				? $"Ffmpeg could not be found ({ex.Message})"
				: $"Unable to create stream ({ex.Message})";
			Log.Error(ex, error);
			instance.Close();
			StopFfmpegProcess();
			return error;
		}
	}

	private void StopFfmpegProcess()
	{
		var oldInstance = Interlocked.Exchange(ref ffmpegInstance, null);
		if (oldInstance != null)
		{
			oldInstance.OnMetaUpdated = null;
			oldInstance.Close();
		}
	}

	private TimeSpan? GetCurrentSongLength() => ffmpegInstance?.ParsedSongLength;

	private void AssertNotMainScheduler()
	{
		if (TaskScheduler.Current == scheduler)
			throw new Exception("Cannot read on own scheduler. Throwing to prevent deadlock");
	}

	public void Dispose()
	{
		StopFfmpegProcess();
	}

	private class FfmpegInstance
	{
		public Process FfmpegProcess { get; }
		public bool HasTriedToReconnect { get; set; }
		public string ReconnectUrl { get; }
		public bool IsIcyStream => IcyStream != null;

		public PreciseAudioTimer AudioTimer { get; }
		public TimeSpan? ParsedSongLength { get; set; } = null;

		public Stream? IcyStream { get; }
		public int IcyMetaInt { get; }
		public bool Closed { get; set; }

		public Action<SongInfoChanged>? OnMetaUpdated;
		public Process? Feeder { get; set; }
		public string? MetaFile { get; set; }
		public bool FeederFailed { get; set; }
		/// <summary>Frix: yt-dlp hata çıktısı ve ffmpeg'e hiç veri gidip gitmediği.</summary>
		public System.Text.StringBuilder? FeederStderr { get; set; }
		public bool FeederGotData { get; set; }

		public FfmpegInstance(string url, PreciseAudioTimer timer) : this(url, timer, null!, 0) { }
		public FfmpegInstance(string url, PreciseAudioTimer timer, Stream icyStream, int icyMetaInt)
		{
			FfmpegProcess = new Process();
			ReconnectUrl = url;
			AudioTimer = timer;
			IcyStream = icyStream;
			IcyMetaInt = icyMetaInt;

			HasTriedToReconnect = false;
		}

		public void Close()
		{
			Closed = true;

			try
			{
				if (!FfmpegProcess.HasExitedSafe())
					FfmpegProcess.Kill();
			}
			catch (Exception ex) { Log.Debug(ex, "Failed killing ffmpeg"); }
			try { FfmpegProcess.Dispose(); } catch { }
			try
			{
				if (Feeder != null && !Feeder.HasExitedSafe())
					Feeder.Kill(true);
			}
			catch (Exception ex) { Log.Debug(ex, "Failed killing yt-dlp"); }
			try { Feeder?.Dispose(); } catch { }
			try { if (MetaFile != null && File.Exists(MetaFile)) File.Delete(MetaFile); } catch { }

			IcyStream?.Dispose();
		}

		public readonly System.Collections.Generic.Queue<string> LastErrorLines = new();

		public void FfmpegProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
		{
			if (e.Data is null)
				return;

			if (sender != FfmpegProcess)
				throw new InvalidOperationException("Wrong process associated to event");

			lock (LastErrorLines)
			{
				LastErrorLines.Enqueue(e.Data);
				while (LastErrorLines.Count > 6)
					LastErrorLines.Dequeue();
			}

			if (ParsedSongLength is null)
			{
				var match = FindDurationMatch.Match(e.Data);
				if (!match.Success)
					return;

				int hours = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
				int minutes = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
				int seconds = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
				int millisec = int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture) * 10;
				ParsedSongLength = new TimeSpan(0, hours, minutes, seconds, millisec);
			}

			//if (!HasIcyTag && e.Data.AsSpan().TrimStart().StartsWith("icy-".AsSpan()))
			//{
			//	HasIcyTag = true;
			//}
		}

		public void ReadStreamLoop(Id id)
		{
			if (IcyStream is null)
				throw new InvalidOperationException("Instance is not an icy stream");

			Tools.SetLogId(id.ToString());
			const int IcyMaxMeta = 255 * 16;
			const int ReadBufferSize = 4096;

			int errorCount = 0;
			var buffer = new byte[Math.Max(ReadBufferSize, IcyMaxMeta)];
			int readCount = 0;

			while (!Closed)
			{
				try
				{
					while (readCount < IcyMetaInt)
					{
						int read = IcyStream.Read(buffer, 0, Math.Min(ReadBufferSize, IcyMetaInt - readCount));
						if (read == 0)
						{
							Close();
							return;
						}
						readCount += read;
						FfmpegProcess.StandardInput.BaseStream.Write(buffer, 0, read);
						errorCount = 0;
					}
					readCount = 0;

					var metaByte = IcyStream.ReadByte();
					if (metaByte < 0)
					{
						Close();
						return;
					}

					if (metaByte > 0)
					{
						metaByte *= 16;
						while (readCount < metaByte)
						{
							int read = IcyStream.Read(buffer, 0, metaByte - readCount);
							if (read == 0)
							{
								Close();
								return;
							}
							readCount += read;
						}
						readCount = 0;

						var metaString = Tools.Utf8Encoder.GetString(buffer, 0, metaByte).TrimEnd('\0');
						Log.Debug("Meta: {0}", metaString);
						OnMetaUpdated?.Invoke(ParseIcyMeta(metaString));
					}
				}
				catch (Exception ex)
				{
					errorCount++;
					if (errorCount >= 50)
					{
						Log.Error(ex, "Failed too many times trying to access ffmpeg. Closing stream.");
						Close();
						return;
					}

					if (ex is InvalidOperationException)
					{
						Log.Debug(ex, "Waiting for ffmpeg");
						Thread.Sleep(100);
					}
					else
					{
						Log.Debug(ex, "Stream read/write error");
					}
				}
			}
		}

		private static SongInfoChanged ParseIcyMeta(string metaString)
		{
			var songInfo = new SongInfoChanged();
			var match = IcyMetadataMacher.Match(metaString);
			if (match.Success)
			{
				for (int i = 0; i < match.Groups[1].Captures.Count; i++)
				{
					switch (match.Groups[2].Captures[i].Value.ToUpperInvariant())
					{
					case "STREAMTITLE":
						songInfo.Title = match.Groups[3].Captures[i].Value;
						break;
					}
				}
			}
			return songInfo;
		}
	}
}

// Icy: IcyLoop +=> FFmpeg -=> Buffer -=> TimePipe
// Nrm:             FFmpeg -=> Buffer -=> TimePipe
