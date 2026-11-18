// Frix: TeamSpeak sohbet komutlarının özelleştirilmesi.
//  - Yerleşik komutların adı değiştirilebilir (örn. !ara -> !cal), istenirse eski ad kapatılır.
//  - Özel komutlar: text (sabit cevap, örn. !discord) ya da alias (başka komuta yönlendirme, örn. !sarki -> !ara).
// Yeni adlar bot cevaplarında, yardım metninde ve avatarda otomatik kullanılır.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TS3AudioBot.Frix;

public sealed record CustomCommand(long Id, string Trigger, string Kind, string Value, bool Enabled, string? Description);
public sealed record BuiltinCommand(string Name, string Usage, string? Alias, bool KeepOld);

public static class FrixCommands
{
	private static readonly ConcurrentDictionary<string, CustomCommand> Cache = new(StringComparer.OrdinalIgnoreCase);
	private static readonly ConcurrentDictionary<string, string> AliasToBuiltin = new(StringComparer.OrdinalIgnoreCase);
	private static readonly ConcurrentDictionary<string, string> BuiltinToAlias = new(StringComparer.OrdinalIgnoreCase);
	private static readonly ConcurrentDictionary<string, byte> DisabledOld = new(StringComparer.OrdinalIgnoreCase);
	private static readonly Regex CommandToken = new(@"!([A-Za-z0-9_-]+)", RegexOptions.Compiled);
	public const string TextCommand = "ozelkomut";

	/// <summary>Panelden yeniden adlandırılabilen yerleşik komutlar.</summary>
	public static readonly (string name, string usage)[] Builtins =
	[
		("ara", "!ara <şarkı/link>"), ("link", "!link <link>"), ("radyo", "!radyo [isim]"),
		("gec", "!gec"), ("skip", "!skip"), ("dur", "!dur"), ("duraklat", "!duraklat"), ("devam", "!devam"),
		("bitir", "!bitir"), ("kapat", "!kapat"), ("ses", "!ses [0-100]"), ("calan", "!calan"),
		("siradaki", "!siradaki"), ("temizle", "!temizle"), ("yardim", "!yardim"),
		("kayit", "!kayit <isim>"), ("liste", "!liste [kaydet|cal|ekle|goster|sil] <ad>"), ("soz", "!soz [şarkı]"),
		("anket", "!anket soru | a | b"), ("oy", "!oy <no>"), ("hava", "!hava <şehir>"), ("doviz", "!doviz <kod> [miktar]"),
		("dolar", "!dolar [miktar]"), ("euro", "!euro [miktar]"), ("zar", "!zar [2d6]"), ("yazitura", "!yazitura"),
		("sec", "!sec a | b | c"), ("istatistik", "!istatistik"), ("top", "!top"), ("sure", "!sure"),
	];

	/// <summary>
	/// Yerleşik komutun çalışması için açık olması gereken eklentiler (herhangi biri yeterli).
	/// "id:ayar" = eklentinin o ayarı da açık olmalı. Listede olmayan komutlar her zaman açıktır.
	/// </summary>
	public static readonly Dictionary<string, string[]> Requirements = new(StringComparer.OrdinalIgnoreCase)
	{
		["ara"] = ["music"], ["link"] = ["music"], ["liste"] = ["music"], ["istatistik"] = ["music"],
		["radyo"] = ["radio"],
		["gec"] = ["music", "radio"], ["skip"] = ["music", "radio"], ["dur"] = ["music", "radio"], ["duraklat"] = ["music", "radio"],
		["devam"] = ["music", "radio"], ["bitir"] = ["music", "radio"], ["kapat"] = ["music", "radio"], ["ses"] = ["music", "radio"],
		["calan"] = ["music", "radio"], ["siradaki"] = ["music", "radio"], ["temizle"] = ["music", "radio"],
		["kayit"] = ["registration"], ["top"] = ["ranks"], ["sure"] = ["ranks"],
		["soz"] = ["fun:lyrics"], ["anket"] = ["fun:poll"], ["oy"] = ["fun:poll"], ["hava"] = ["fun:weather"],
		["doviz"] = ["fun:currency"], ["dolar"] = ["fun:currency"], ["euro"] = ["fun:currency"],
		["zar"] = ["fun:dice"], ["yazitura"] = ["fun:dice"], ["sec"] = ["fun:dice"],
	};

	/// <summary>Yerleşik komut bu botta kullanılabilir mi (bağlı eklenti açık mı)?</summary>
	public static bool IsAvailable(string builtin, string? bot)
		=> bot is null || !Requirements.TryGetValue(builtin, out var reqs) || reqs.Any(r => FrixPluginHost.Active(bot, r));

	/// <summary>Verilen botlardan en az birinde kullanılabilir mi? Bot yoksa true.</summary>
	public static bool IsAvailableAny(string builtin, IReadOnlyCollection<string> bots)
		=> bots.Count == 0 || bots.Any(b => IsAvailable(builtin, b));

	/// <summary>Metindeki satırlardan, komutu bu botta kapalı olanları çıkarır (yardım metni için).</summary>
	public static string FilterLines(string text, Func<string, bool> available)
		=> string.Join('\n', text.Split('\n').Where(line =>
		{
			var tokens = CommandToken.Matches(line).Select(m => m.Groups[1].Value).Where(IsBuiltin).ToArray();
			return tokens.Length == 0 || tokens.Any(available);
		}));

	/// <summary>Kurulumda seçilen dile göre ana komutların adları. Türkçe adlar da çalışmaya devam eder.</summary>
	public static readonly Dictionary<string, Dictionary<string, string>> LanguageDefaults = new()
	{
		["en"] = new() { ["ara"] = "play", ["radyo"] = "radio", ["gec"] = "next", ["dur"] = "pause", ["devam"] = "resume", ["bitir"] = "stop", ["ses"] = "vol", ["calan"] = "np", ["siradaki"] = "queue", ["temizle"] = "clear", ["yardim"] = "help",
			["kayit"] = "register", ["liste"] = "playlist", ["soz"] = "lyrics", ["anket"] = "poll", ["oy"] = "vote", ["hava"] = "weather",
			["doviz"] = "fx", ["zar"] = "dice", ["yazitura"] = "coin", ["sec"] = "pick", ["istatistik"] = "stats", ["sure"] = "time",
			["duraklat"] = "hold", ["kapat"] = "end", ["dolar"] = "usd", ["euro"] = "eur" },
		["de"] = new() { ["ara"] = "spielen", ["radyo"] = "radio", ["gec"] = "weiter", ["dur"] = "pause", ["devam"] = "fortsetzen", ["bitir"] = "stopp", ["ses"] = "lautstärke", ["calan"] = "jetzt", ["siradaki"] = "liste", ["temizle"] = "leeren", ["yardim"] = "hilfe" },
		["es"] = new() { ["ara"] = "poner", ["radyo"] = "radio", ["gec"] = "siguiente", ["dur"] = "pausa", ["devam"] = "seguir", ["bitir"] = "parar", ["ses"] = "volumen", ["calan"] = "sonando", ["siradaki"] = "cola", ["temizle"] = "vaciar", ["yardim"] = "ayuda" },
		["fr"] = new() { ["ara"] = "jouer", ["radyo"] = "radio", ["gec"] = "suivant", ["dur"] = "pause", ["devam"] = "reprendre", ["bitir"] = "stop", ["ses"] = "volume", ["calan"] = "encours", ["siradaki"] = "file", ["temizle"] = "vider", ["yardim"] = "aide" },
		["ru"] = new() { ["ara"] = "играть", ["radyo"] = "радио", ["gec"] = "далее", ["dur"] = "пауза", ["devam"] = "продолжить", ["bitir"] = "стоп", ["ses"] = "громкость", ["calan"] = "сейчас", ["siradaki"] = "очередь", ["temizle"] = "очистить", ["yardim"] = "помощь" },
		["pt"] = new() { ["ara"] = "tocar", ["radyo"] = "radio", ["gec"] = "proxima", ["dur"] = "pausar", ["devam"] = "continuar", ["bitir"] = "parar", ["ses"] = "volume", ["calan"] = "tocando", ["siradaki"] = "fila", ["temizle"] = "limpar", ["yardim"] = "ajuda" },
		["pl"] = new() { ["ara"] = "graj", ["radyo"] = "radio", ["gec"] = "dalej", ["dur"] = "pauza", ["devam"] = "wznów", ["bitir"] = "stop", ["ses"] = "głośność", ["calan"] = "teraz", ["siradaki"] = "kolejka", ["temizle"] = "wyczyść", ["yardim"] = "pomoc" },
		["it"] = new() { ["ara"] = "suona", ["radyo"] = "radio", ["gec"] = "prossima", ["dur"] = "pausa", ["devam"] = "riprendi", ["bitir"] = "stop", ["ses"] = "volume", ["calan"] = "ora", ["siradaki"] = "coda", ["temizle"] = "svuota", ["yardim"] = "aiuto" },
	};

	/// <summary>Komut adlarını bir dilin varsayılanlarına ayarlar (tr = orijinal adlar). Çakışan adlar atlanır.</summary>
	/// <summary>
	/// Komut adlarını bir dilin varsayılanlarına ayarlar. Varsayılan dil İngilizcedir:
	/// bir dilde karşılığı olmayan komutlar İngilizce adı alır. "tr" = orijinal Türkçe adlar.
	/// Türkçe adlar her durumda çalışmaya devam eder. Çakışan adlar atlanır.
	/// </summary>
	public static string[] ApplyLanguageDefaults(string lang)
	{
		FrixDb.Exec("DELETE FROM builtin_names");
		Load();
		var skipped = new List<string>();
		if (lang != "tr")
		{
			var map = new Dictionary<string, string>(LanguageDefaults["en"]);
			if (lang != "en" && LanguageDefaults.TryGetValue(lang, out var own))
				foreach (var (k, v) in own) map[k] = v;
			foreach (var (builtin, alias) in map)
			{
				try { SetBuiltinName(builtin, alias, keepOld: true); }
				catch (ArgumentException) { skipped.Add(alias); }
			}
		}
		FrixDb.SetMeta("cmd_names_lang", lang);
		Load();
		return [.. skipped];
	}

	/// <summary>İlk açılışta (dil hiç seçilmemişse) komut adlarını İngilizce yapar.</summary>
	public static void EnsureDefaultNames()
	{
		if (FrixDb.GetMeta("cmd_names_lang") is null)
			ApplyLanguageDefaults("en");
	}

	private static bool IsBuiltin(string name) => Builtins.Any(b => b.name.Equals(name, StringComparison.OrdinalIgnoreCase));

	public static void Load()
	{
		Cache.Clear();
		foreach (var c in List().Where(c => c.Enabled))
			Cache[c.Trigger] = c;

		AliasToBuiltin.Clear();
		BuiltinToAlias.Clear();
		DisabledOld.Clear();
		foreach (var row in FrixDb.Query("SELECT name, alias, keep_old FROM builtin_names"))
		{
			var name = (string)row["name"]!;
			if (row["alias"] is string alias && alias.Length > 0)
			{
				AliasToBuiltin[alias] = name;
				BuiltinToAlias[name] = alias;
				if ((long)row["keep_old"]! == 0)
					DisabledOld[name] = 0;
			}
		}
	}

	public static CustomCommand[] List()
		=> FrixDb.Query("SELECT id, trigger, kind, value, enabled, description FROM commands ORDER BY trigger COLLATE NOCASE")
			.Select(r => new CustomCommand((long)r["id"]!, (string)r["trigger"]!, (string)r["kind"]!, (string)r["value"]!,
				(long)r["enabled"]! != 0, r["description"] as string))
			.ToArray();

	public static BuiltinCommand[] BuiltinList()
		=> Builtins.Select(b => new BuiltinCommand(b.name, b.usage,
			BuiltinToAlias.GetValueOrDefault(b.name), !DisabledOld.ContainsKey(b.name))).ToArray();

	/// <summary>Yerleşik komutun TeamSpeak'te görünen adı (yeniden adlandırıldıysa yeni adı).</summary>
	public static string Display(string builtin) => BuiltinToAlias.GetValueOrDefault(builtin) ?? builtin;

	/// <summary>Metindeki !komut adlarını yeni adlarla değiştirir (cevaplar, yardım, avatar).</summary>
	public static string ApplyNames(string text)
		=> BuiltinToAlias.IsEmpty ? text : CommandToken.Replace(text, m => BuiltinToAlias.TryGetValue(m.Groups[1].Value, out var a) ? "!" + a : m.Value);

	public static void SetBuiltinName(string name, string? alias, bool keepOld)
	{
		if (!IsBuiltin(name)) throw new ArgumentException("Bilinmeyen komut.");
		name = name.ToLowerInvariant();
		if (string.IsNullOrWhiteSpace(alias) || alias.Trim().TrimStart('!').Equals(name, StringComparison.OrdinalIgnoreCase))
		{
			FrixDb.Exec("DELETE FROM builtin_names WHERE name=$n", ("$n", name));
		}
		else
		{
			alias = NormalizeTrigger(alias);
			if (IsBuiltin(alias))
				throw new ArgumentException($"'!{alias}' zaten yerleşik bir komut.");
			if (AliasToBuiltin.TryGetValue(alias, out var other) && other != name)
				throw new ArgumentException($"'!{alias}' zaten '!{other}' için kullanılıyor.");
			if (Cache.ContainsKey(alias) || List().Any(c => c.Trigger.Equals(alias, StringComparison.OrdinalIgnoreCase)))
				throw new ArgumentException($"'!{alias}' adında özel bir komut var.");
			FrixDb.Exec("INSERT INTO builtin_names(name,alias,keep_old) VALUES($n,$a,$k) ON CONFLICT(name) DO UPDATE SET alias=excluded.alias, keep_old=excluded.keep_old",
				("$n", name), ("$a", alias), ("$k", keepOld ? 1 : 0));
		}
		Load();
	}

	public static string NormalizeTrigger(string trigger)
	{
		trigger = trigger.Trim().TrimStart('!').ToLowerInvariant();
		if (trigger.Length is < 1 or > 32 || !char.IsLetter(trigger[0]) || !trigger.All(c => char.IsLetterOrDigit(c) || c is '_' or '-'))
			throw new ArgumentException("Komut adı harfle başlamalı; harf, rakam, - ve _ içerebilir (1-32 karakter).");
		return trigger;
	}

	public static void Validate(string kind, string value)
	{
		if (kind is not ("text" or "alias"))
			throw new ArgumentException("Geçersiz tür.");
		if (string.IsNullOrWhiteSpace(value) || value.Length > 1000)
			throw new ArgumentException("Değer 1-1000 karakter olmalı.");
		if (kind == "alias" && !value.TrimStart().StartsWith('!'))
			throw new ArgumentException("Yönlendirme '!' ile başlayan bir komut olmalı. Örn: !ara");
	}

	public static void ValidateCustomTrigger(string trigger)
	{
		if (IsBuiltin(trigger) || AliasToBuiltin.ContainsKey(trigger))
			throw new ArgumentException($"'!{trigger}' yerleşik bir komutun adı. Yerleşik komutları 'Yerleşik komutlar' bölümünden yeniden adlandırın.");
	}

	/// <summary>Gelen sohbet mesajını yeniden yazar. null dönerse mesaj yoksayılır (eski adı kapatılmış komut).</summary>
	public static string? Rewrite(string message)
	{
		var space = message.IndexOf(' ');
		var trigger = space < 0 ? message[1..] : message[1..space];
		var rest = space < 0 ? "" : message[(space + 1)..];
		var tail = rest.Length > 0 ? " " + rest : "";

		if (AliasToBuiltin.TryGetValue(trigger, out var builtin))
			return "!" + builtin + tail;
		if (DisabledOld.ContainsKey(trigger))
			return null;
		if (!Cache.TryGetValue(trigger, out var cmd))
			return message;
		return cmd.Kind == "alias"
			? cmd.Value.Trim() + tail
			: $"!{TextCommand} {cmd.Trigger}" + tail;
	}

	public static string? TextFor(string trigger, string user, string args)
	{
		if (!Cache.TryGetValue(trigger, out var cmd) || cmd.Kind != "text")
			return null;
		return FrixPlugin.Fill(cmd.Value, ("user", user), ("args", args),
			("time", DateTime.Now.ToString("HH:mm")), ("date", DateTime.Now.ToString("dd.MM.yyyy")));
	}
}
