// Frix veritabanı (SQLite): panel kullanıcıları, oturumlar, radyolar,
// eklenti ayarları, özel komutlar, metin/çeviri düzenlemeleri ve genel ayarlar.

using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;

namespace TS3AudioBot.Frix;

public static class FrixDb
{
	private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
	private static readonly object Sync = new();
	private static string? connectionString;

	public static string DbPath => Path.Combine(AppContext.BaseDirectory, "frix.db");

	public static void Initialize()
	{
		lock (Sync)
		{
			if (connectionString != null)
				return;
			// Eski sürümden (furina.db) taşı
			try
			{
				var legacy = Path.Combine(AppContext.BaseDirectory, "furina.db");
				if (!File.Exists(DbPath) && File.Exists(legacy))
					foreach (var suffix in new[] { "", "-wal", "-shm" })
						if (File.Exists(legacy + suffix))
							File.Move(legacy + suffix, DbPath + suffix);
			}
			catch (Exception ex) { Log.Warn(ex, "Eski veritabanı taşınamadı"); }
			connectionString = new SqliteConnectionStringBuilder
			{
				DataSource = DbPath,
				Mode = SqliteOpenMode.ReadWriteCreate,
				Cache = SqliteCacheMode.Shared,
			}.ToString();

			using var c = Open();
			Exec(c, "PRAGMA journal_mode=WAL;");
			Exec(c, @"
CREATE TABLE IF NOT EXISTS meta (key TEXT PRIMARY KEY, value TEXT);
CREATE TABLE IF NOT EXISTS users (
	id INTEGER PRIMARY KEY AUTOINCREMENT,
	name TEXT NOT NULL UNIQUE COLLATE NOCASE,
	salt TEXT NOT NULL, hash TEXT NOT NULL, iterations INTEGER NOT NULL,
	role TEXT NOT NULL DEFAULT 'dj',
	created TEXT NOT NULL, last_login TEXT);
CREATE TABLE IF NOT EXISTS sessions (
	key TEXT PRIMARY KEY, user_id INTEGER NOT NULL, expires TEXT NOT NULL,
	created TEXT NOT NULL, ip TEXT, agent TEXT,
	FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE);
CREATE TABLE IF NOT EXISTS radios (
	id INTEGER PRIMARY KEY AUTOINCREMENT,
	name TEXT NOT NULL UNIQUE COLLATE NOCASE, url TEXT NOT NULL, genre TEXT, created TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS texts (lang TEXT NOT NULL, key TEXT NOT NULL, value TEXT NOT NULL, PRIMARY KEY(lang, key));
CREATE TABLE IF NOT EXISTS plugins (bot TEXT NOT NULL, id TEXT NOT NULL, enabled INTEGER NOT NULL DEFAULT 0, config TEXT, PRIMARY KEY(bot, id));
CREATE TABLE IF NOT EXISTS commands (
	id INTEGER PRIMARY KEY AUTOINCREMENT,
	trigger TEXT NOT NULL UNIQUE COLLATE NOCASE, kind TEXT NOT NULL, value TEXT NOT NULL,
	enabled INTEGER NOT NULL DEFAULT 1, description TEXT);
CREATE TABLE IF NOT EXISTS builtin_names (name TEXT PRIMARY KEY, alias TEXT NOT NULL UNIQUE COLLATE NOCASE, keep_old INTEGER NOT NULL DEFAULT 1);
CREATE TABLE IF NOT EXISTS audit (
	id INTEGER PRIMARY KEY AUTOINCREMENT, time TEXT NOT NULL, user TEXT, ip TEXT, action TEXT NOT NULL, detail TEXT);
");
			Log.Info("Veritabanı hazır: {0}", DbPath);
		}
	}

	public static SqliteConnection Open()
	{
		var c = new SqliteConnection(connectionString ?? throw new InvalidOperationException("FrixDb başlatılmadı"));
		c.Open();
		using var cmd = c.CreateCommand();
		cmd.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";
		cmd.ExecuteNonQuery();
		return c;
	}

	public static int Exec(SqliteConnection c, string sql, params (string, object?)[] args)
	{
		using var cmd = c.CreateCommand();
		cmd.CommandText = sql;
		foreach (var (k, v) in args)
			cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
		return cmd.ExecuteNonQuery();
	}

	public static int Exec(string sql, params (string, object?)[] args)
	{
		using var c = Open();
		return Exec(c, sql, args);
	}

	public static object? Scalar(string sql, params (string, object?)[] args)
	{
		using var c = Open();
		using var cmd = c.CreateCommand();
		cmd.CommandText = sql;
		foreach (var (k, v) in args)
			cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
		var r = cmd.ExecuteScalar();
		return r is DBNull ? null : r;
	}

	public static List<Dictionary<string, object?>> Query(string sql, params (string, object?)[] args)
	{
		using var c = Open();
		using var cmd = c.CreateCommand();
		cmd.CommandText = sql;
		foreach (var (k, v) in args)
			cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
		using var r = cmd.ExecuteReader();
		var list = new List<Dictionary<string, object?>>();
		while (r.Read())
		{
			var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
			for (int i = 0; i < r.FieldCount; i++)
				row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
			list.Add(row);
		}
		return list;
	}

	public static string? GetMeta(string key) => Scalar("SELECT value FROM meta WHERE key=$k", ("$k", key)) as string;

	public static void SetMeta(string key, string? value)
	{
		if (value is null)
			Exec("DELETE FROM meta WHERE key=$k", ("$k", key));
		else
			Exec("INSERT INTO meta(key,value) VALUES($k,$v) ON CONFLICT(key) DO UPDATE SET value=excluded.value", ("$k", key), ("$v", value));
	}

	public static void Audit(string? user, string? ip, string action, string? detail = null)
	{
		try
		{
			Exec("INSERT INTO audit(time,user,ip,action,detail) VALUES($t,$u,$i,$a,$d)",
				("$t", DateTime.UtcNow.ToString("O")), ("$u", user), ("$i", ip), ("$a", action), ("$d", detail));
			Exec("DELETE FROM audit WHERE id <= (SELECT MAX(id) - 20000 FROM audit)");
		}
		catch (Exception ex)
		{
			Log.Debug(ex, "Audit yazılamadı");
		}
	}
}
