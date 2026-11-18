// Frix tray uygulaması
// - TS3AudioBot'u konsol penceresi olmadan arka planda çalıştırır
// - Bot kapanır/çökerse otomatik yeniden başlatır (panel her zaman açık kalır)
// - Sistem tepsisinde simge: Paneli aç, Yeniden başlat, Windows ile başlat, Çıkış

using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace FrixTray;

internal static class Program
{
	[STAThread]
	private static void Main()
	{
		using var mutex = new Mutex(true, @"Local\FrixTray", out var isNew);
		if (!isNew)
		{
			// Zaten çalışıyor: sadece paneli aç
			TrayContext.OpenPanel(TrayContext.ReadPort(AppContext.BaseDirectory));
			return;
		}

		ApplicationConfiguration.Initialize();
		Application.Run(new TrayContext());
	}
}

internal sealed class TrayContext : ApplicationContext
{
	private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
	private const string RunName = "Frix";

	private readonly string baseDir = AppContext.BaseDirectory;
	private readonly string botExe;
	private readonly NotifyIcon tray;
	private readonly ToolStripMenuItem statusItem;
	private readonly ToolStripMenuItem autostartItem;
	private readonly System.Windows.Forms.Timer restartTimer = new();
	private readonly Control uiInvoker = new();
	private readonly List<DateTime> crashTimes = [];
	private Process? bot;
	private bool exiting;
	private bool manualRestart;

	public TrayContext()
	{
		botExe = Path.Combine(baseDir, "TS3AudioBot.exe");
		uiInvoker.CreateControl();

		statusItem = new ToolStripMenuItem("Başlatılıyor…") { Enabled = false };
		autostartItem = new ToolStripMenuItem("Windows açılışında başlat", null, (_, _) => ToggleAutostart())
		{
			Checked = IsAutostartEnabled(),
		};

		var menu = new ContextMenuStrip();
		menu.Items.Add(statusItem);
		menu.Items.Add(new ToolStripSeparator());
		menu.Items.Add(new ToolStripMenuItem("Web paneli aç", null, (_, _) => OpenPanel(ReadPort(baseDir))) { Font = new Font(menu.Font, FontStyle.Bold) });
		menu.Items.Add(new ToolStripMenuItem("Botu yeniden başlat", null, (_, _) => RestartBot()));
		menu.Items.Add(new ToolStripMenuItem("Bot klasörünü aç", null, (_, _) => Process.Start(new ProcessStartInfo(baseDir) { UseShellExecute = true })));
		menu.Items.Add(new ToolStripSeparator());
		menu.Items.Add(autostartItem);
		menu.Items.Add(new ToolStripSeparator());
		menu.Items.Add(new ToolStripMenuItem("Çıkış (botu kapat)", null, (_, _) => ExitAll()));

		tray = new NotifyIcon
		{
			Icon = LoadIcon(),
			Text = "Frix",
			ContextMenuStrip = menu,
			Visible = true,
		};
		tray.DoubleClick += (_, _) => OpenPanel(ReadPort(baseDir));

		restartTimer.Tick += (_, _) =>
		{
			restartTimer.Stop();
			StartBot();
		};

		// İlk çalıştırmada Windows ile başlatmayı aç
		var marker = Path.Combine(baseDir, ".tray_ilk_kurulum");
		if (!File.Exists(marker))
		{
			try
			{
				SetAutostart(true);
				autostartItem.Checked = true;
				File.WriteAllText(marker, DateTime.Now.ToString("O"));
			}
			catch { /* yetki yoksa önemli değil */ }
		}

		KillOrphanBots();
		StartBot();

		var port = ReadPort(baseDir);
		var firstRun = !File.Exists(Path.Combine(baseDir, "frix.db")) || File.Exists(Path.Combine(baseDir, "KURULUM_KODU.txt"));
		tray.ShowBalloonTip(5000, "Frix çalışıyor",
			firstRun
				? $"İlk kurulum: http://localhost:{port} adresini açıp yönetici hesabını oluştur."
				: $"Panel: http://localhost:{port}\nSimgeye çift tıklayarak paneli açabilirsin.",
			ToolTipIcon.Info);
	}

	private void StartBot()
	{
		if (exiting)
			return;
		if (!File.Exists(botExe))
		{
			SetStatus("TS3AudioBot.exe bulunamadı!");
			MessageBox.Show($"TS3AudioBot.exe bulunamadı:\n{botExe}", "Frix", MessageBoxButtons.OK, MessageBoxIcon.Error);
			return;
		}

		try
		{
			var psi = new ProcessStartInfo(botExe, "--non-interactive --stats-disabled")
			{
				WorkingDirectory = baseDir,
				UseShellExecute = false,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden,
			};
			bot = new Process { StartInfo = psi, EnableRaisingEvents = true, SynchronizingObject = uiInvoker };
			bot.Exited += OnBotExited;
			bot.Start();
			SetStatus("Bot çalışıyor");
		}
		catch (Exception ex)
		{
			SetStatus("Bot başlatılamadı");
			tray.ShowBalloonTip(5000, "Frix", "Bot başlatılamadı: " + ex.Message, ToolTipIcon.Error);
			ScheduleRestart(TimeSpan.FromSeconds(15));
		}
	}

	private void OnBotExited(object? sender, EventArgs e) => HandleBotExit();

	private void HandleBotExit()
	{
		if (exiting)
			return;

		int code = -1;
		try { code = bot?.ExitCode ?? -1; } catch { }
		bot?.Dispose();
		bot = null;

		if (code == 4 && Directory.Exists(Path.Combine(baseDir, "_update")))
		{
			ApplyUpdate();
			return;
		}

		if (manualRestart || code == 3)
		{
			// Panelden veya menüden istenen yeniden başlatma
			manualRestart = false;
			SetStatus("Yeniden başlatılıyor…");
			ScheduleRestart(TimeSpan.FromSeconds(1));
			return;
		}

		var now = DateTime.Now;
		crashTimes.Add(now);
		crashTimes.RemoveAll(t => now - t > TimeSpan.FromMinutes(2));
		var delay = crashTimes.Count >= 4 ? TimeSpan.FromSeconds(60) : TimeSpan.FromSeconds(3);
		SetStatus($"Bot kapandı (kod {code}), {delay.TotalSeconds:0} sn sonra yeniden açılıyor…");
		if (crashTimes.Count == 4)
			tray.ShowBalloonTip(5000, "Frix", "Bot art arda çöktü. Web paneldeki Loglar sekmesine bak.", ToolTipIcon.Warning);
		ScheduleRestart(delay);
	}

	/// <summary>
	/// _update/ klasöründeki yeni sürümü kurar. Çalışan tray kendi dosyalarını kilitlediği için
	/// işi ayrı bir betik yapar: tray kapanınca kopyalar ve tray'i yeniden başlatır.
	/// Kullanıcı verileri (ayarlar, veritabanı, botlar, çerezler, loglar) korunur.
	/// </summary>
	private void ApplyUpdate()
	{
		SetStatus("Güncelleme kuruluyor…");
		tray.ShowBalloonTip(4000, "Frix", "Güncelleme kuruluyor, birkaç saniye içinde yeniden açılacak.", ToolTipIcon.Info);
		var script = Path.Combine(Path.GetTempPath(), "frix_update.cmd");
		var dir = baseDir.TrimEnd('\\');
		File.WriteAllText(script, string.Join("\r\n",
			"@echo off",
			"chcp 65001 >nul",
			":wait",
			"tasklist /FI \"IMAGENAME eq Frix.exe\" | find /I \"Frix.exe\" >nul && (timeout /t 1 >nul & goto wait)",
			"tasklist /FI \"IMAGENAME eq TS3AudioBot.exe\" | find /I \"TS3AudioBot.exe\" >nul && (timeout /t 1 >nul & goto wait)",
			$"robocopy \"{dir}\\_update\" \"{dir}\" /E /R:3 /W:1 /NFL /NDL /NJH /NJS " +
				"/XF ts3audiobot.toml rights.toml frix.db frix.db-wal frix.db-shm youtube_cookies.txt yt-dlp.conf .tray_ilk_kurulum " +
				"/XD bots logs",
			$"rmdir /s /q \"{dir}\\_update\"",
			$"start \"\" \"{dir}\\Frix.exe\"",
			"del \"%~f0\""));
		Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"")
		{
			UseShellExecute = false,
			CreateNoWindow = true,
			WindowStyle = ProcessWindowStyle.Hidden,
		});
		exiting = true;
		restartTimer.Stop();
		tray.Visible = false;
		tray.Dispose();
		ExitThread();
	}

	private void ScheduleRestart(TimeSpan delay)
	{
		restartTimer.Stop();
		restartTimer.Interval = (int)Math.Max(100, delay.TotalMilliseconds);
		restartTimer.Start();
	}

	private void RestartBot()
	{
		if (bot is { HasExited: false })
		{
			manualRestart = true;
			SetStatus("Yeniden başlatılıyor…");
			try { bot.Kill(true); } catch { }
		}
		else
		{
			restartTimer.Stop();
			StartBot();
		}
	}

	private void ExitAll()
	{
		var answer = MessageBox.Show("Bot tamamen kapatılsın mı?\nWeb panel de kapanır.", "Frix",
			MessageBoxButtons.YesNo, MessageBoxIcon.Question);
		if (answer != DialogResult.Yes)
			return;

		exiting = true;
		restartTimer.Stop();
		try { if (bot is { HasExited: false }) bot.Kill(true); } catch { }
		tray.Visible = false;
		tray.Dispose();
		ExitThread();
	}

	private void SetStatus(string text)
	{
		statusItem.Text = text;
		var tip = "Frix – " + text;
		tray.Text = tip.Length > 63 ? tip[..63] : tip;
	}

	/// <summary>Bu klasörden kalmış eski bot süreçlerini kapatır (port çakışmasın diye).</summary>
	private void KillOrphanBots()
	{
		foreach (var p in Process.GetProcessesByName("TS3AudioBot"))
		{
			try
			{
				var path = p.MainModule?.FileName;
				if (path != null && string.Equals(Path.GetFullPath(path), Path.GetFullPath(botExe), StringComparison.OrdinalIgnoreCase))
				{
					p.Kill(true);
					p.WaitForExit(5000);
				}
			}
			catch { /* erişim yok */ }
			finally { p.Dispose(); }
		}
	}

	private static Icon LoadIcon()
	{
		try
		{
			using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("FrixTray.frix.ico");
			if (s != null)
				return new Icon(s);
		}
		catch { }
		return SystemIcons.Application;
	}

	// ---- Windows ile başlat (oturum açılınca) ----
	private bool IsAutostartEnabled()
	{
		using var key = Registry.CurrentUser.OpenSubKey(RunKey);
		var value = key?.GetValue(RunName) as string;
		return value != null && value.Contains(Path.Combine(baseDir, "Frix.exe"), StringComparison.OrdinalIgnoreCase);
	}

	private void SetAutostart(bool enable)
	{
		using var key = Registry.CurrentUser.CreateSubKey(RunKey);
		if (enable)
			key.SetValue(RunName, "\"" + Path.Combine(baseDir, "Frix.exe") + "\"");
		else
			key.DeleteValue(RunName, false);
	}

	private void ToggleAutostart()
	{
		try
		{
			SetAutostart(!autostartItem.Checked);
			autostartItem.Checked = IsAutostartEnabled();
		}
		catch (Exception ex)
		{
			MessageBox.Show("Ayar değiştirilemedi: " + ex.Message, "Frix", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	// ---- Yardımcılar ----
	public static int ReadPort(string baseDir)
	{
		try
		{
			var text = File.ReadAllText(Path.Combine(baseDir, "ts3audiobot.toml"));
			var web = text.IndexOf("[web]", StringComparison.Ordinal);
			if (web >= 0)
			{
				var m = Regex.Match(text[web..], @"^\s*port\s*=\s*(\d+)", RegexOptions.Multiline);
				if (m.Success && int.TryParse(m.Groups[1].Value, out var port))
					return port;
			}
		}
		catch { }
		return 58913;
	}

	public static void OpenPanel(int port)
	{
		try { Process.Start(new ProcessStartInfo($"http://localhost:{port}/") { UseShellExecute = true }); }
		catch { }
	}
}
