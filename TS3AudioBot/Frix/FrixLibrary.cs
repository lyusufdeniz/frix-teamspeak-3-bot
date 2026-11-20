// Frix: panelden yüklenen kendi müzik dosyaların (kütüphane).
// Dosyalar uygulama klasöründeki music-library/ altında rastgele adla saklanır; başlık veritabanında tutulur.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TS3AudioBot.ResourceFactories;

namespace TS3AudioBot.Frix;

public sealed record LibraryItem(long Id, string Title, string File, long Size, string Added, string? AddedBy)
{
	public string FullPath => Path.Combine(FrixLibrary.Dir, File);
	public AudioResource Resource() => new(FullPath, Title, "media");
}

public static class FrixLibrary
{
	public static string Dir => Path.Combine(AppContext.BaseDirectory, "music-library");
	public const long MaxFileSize = 200L * 1024 * 1024;
	public static readonly string[] Extensions = [".mp3", ".ogg", ".opus", ".wav", ".flac", ".m4a", ".aac", ".webm", ".wma"];

	public static void EnsureTable()
		=> FrixDb.Exec(@"CREATE TABLE IF NOT EXISTS library (
			id INTEGER PRIMARY KEY AUTOINCREMENT, title TEXT NOT NULL, file TEXT NOT NULL UNIQUE, size INTEGER NOT NULL DEFAULT 0,
			added TEXT NOT NULL, added_by TEXT)");

	private static LibraryItem Map(Dictionary<string, object?> r)
		=> new((long)r["id"]!, (string)r["title"]!, (string)r["file"]!, (long)r["size"]!, (string)r["added"]!, r["added_by"] as string);

	public static List<LibraryItem> List()
		=> FrixDb.Query("SELECT * FROM library ORDER BY title COLLATE NOCASE").Select(Map).ToList();

	public static LibraryItem? Get(long id)
		=> FrixDb.Query("SELECT * FROM library WHERE id=$i", ("$i", id)).Select(Map).FirstOrDefault();

	/// <summary>Başlığa göre arama: tam eşleşme, sonra başlayan, sonra içeren.</summary>
	public static LibraryItem? Find(string query)
	{
		var q = query.Trim();
		if (q.Length == 0) return null;
		var all = List();
		return all.FirstOrDefault(x => x.Title.Equals(q, StringComparison.OrdinalIgnoreCase))
			?? all.FirstOrDefault(x => x.Title.StartsWith(q, StringComparison.OrdinalIgnoreCase))
			?? all.FirstOrDefault(x => x.Title.Contains(q, StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>Akıştaki dosyayı kütüphaneye ekler. Uzantı ve boyut kontrol edilir.</summary>
	public static async Task<LibraryItem> Add(Stream data, string originalName, string? by)
	{
		var ext = Path.GetExtension(originalName).ToLowerInvariant();
		if (!Extensions.Contains(ext)) throw new InvalidDataException("bad_audio_type");
		var title = Path.GetFileNameWithoutExtension(originalName).Replace('_', ' ').Trim();
		if (title.Length == 0) title = "?";
		if (title.Length > 120) title = title[..120];
		Directory.CreateDirectory(Dir);
		var file = Guid.NewGuid().ToString("N") + ext;
		var path = Path.Combine(Dir, file);
		long size = 0;
		try
		{
			await using (var f = File.Create(path))
			{
				var buf = new byte[81920];
				int n;
				while ((n = await data.ReadAsync(buf)) > 0)
				{
					size += n;
					if (size > MaxFileSize) throw new InvalidDataException("file_too_big");
					await f.WriteAsync(buf.AsMemory(0, n));
				}
			}
			if (size == 0) throw new InvalidDataException("empty_file");
		}
		catch
		{
			try { File.Delete(path); } catch { }
			throw;
		}
		FrixDb.Exec("INSERT INTO library(title,file,size,added,added_by) VALUES($t,$f,$s,$a,$b)",
			("$t", title), ("$f", file), ("$s", size), ("$a", DateTime.UtcNow.ToString("O")), ("$b", by));
		return FrixDb.Query("SELECT * FROM library WHERE file=$f", ("$f", file)).Select(Map).First();
	}

	public static void Rename(long id, string title)
	{
		title = title.Trim();
		if (title.Length is < 1 or > 120) throw new ArgumentException("bad_title");
		var item = Get(id) ?? throw new FileNotFoundException();
		FrixDb.Exec("UPDATE library SET title=$t WHERE id=$i", ("$t", title), ("$i", id));
		// çalma listelerindeki adı da güncelle
		FrixDb.Exec("UPDATE playlist_items SET title=$t WHERE type='media' AND resource_id=$r", ("$t", title), ("$r", item.FullPath));
	}

	public static void Delete(long id)
	{
		var item = Get(id) ?? throw new FileNotFoundException();
		FrixDb.Exec("DELETE FROM library WHERE id=$i", ("$i", id));
		FrixDb.Exec("DELETE FROM playlist_items WHERE type='media' AND resource_id=$r", ("$r", item.FullPath));
		try { File.Delete(item.FullPath); } catch { /* dosya zaten yoksa sorun değil */ }
	}

	public static long TotalSize() => (long)(FrixDb.Scalar("SELECT COALESCE(SUM(size),0) FROM library") ?? 0L);
}
