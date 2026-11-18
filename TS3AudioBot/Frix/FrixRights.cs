// Frix: güncellemelerde kullanıcının rights.toml dosyası korunur. Yeni sohbet komutlarının
// herkes tarafından kullanılabilmesi için eksik "cmd.*" yetkileri en üstteki "+" listesine eklenir.

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TS3AudioBot.Frix;

public static class FrixRights
{
	private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

	public static void EnsureCommandRights(string path)
	{
		try
		{
			if (!File.Exists(path)) return;
			var text = File.ReadAllText(path);
			var firstRule = text.IndexOf("[[rule]]", StringComparison.Ordinal);
			var head = firstRule < 0 ? text : text[..firstRule];
			var m = Regex.Match(head, @"^""\+""\s*=\s*\[", RegexOptions.Multiline);
			if (!m.Success) return;

			var wanted = FrixCommands.Builtins.Select(b => "cmd." + b.name).Append("cmd." + FrixCommands.TextCommand).Distinct();
			var missing = wanted.Where(r => !Regex.IsMatch(text, "\"" + Regex.Escape(r) + "\"")).ToList();
			if (missing.Count == 0) return;

			var nl = text.Contains("\r\n") ? "\r\n" : "\n";
			var insert = nl + "\t# Frix (otomatik eklendi)" + nl + string.Concat(missing.Select(r => $"\t\"{r}\",{nl}"));
			text = text.Insert(m.Index + m.Length, insert);
			File.WriteAllText(path, text);
			Log.Info("rights.toml: yeni komut yetkileri eklendi: {0}", string.Join(", ", missing));
		}
		catch (Exception ex)
		{
			Log.Warn(ex, "rights.toml güncellenemedi");
		}
	}
}
