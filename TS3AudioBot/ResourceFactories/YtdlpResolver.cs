// Frix: YouTube dışındaki siteler (SoundCloud, Twitch, Bandcamp, Vimeo, canlı yayınlar ...)
// için genel çözücü. Sesi yt-dlp üzerinden doğrudan akıtır.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TS3AudioBot.ResourceFactories.Youtube;

namespace TS3AudioBot.ResourceFactories;

public sealed class YtdlpResolver : IResourceResolver
{
	public string ResolverFor => "ytdlp";

	private static readonly string[] DirectAudioExt =
		[".mp3", ".aac", ".aacp", ".ogg", ".oga", ".opus", ".flac", ".wav", ".m4a", ".m3u", ".m3u8", ".pls", ".xspf", ".wma"];

	/// <summary>Doğrudan ses dosyası ya da radyo yayını gibi görünen linkler ffmpeg ile doğrudan açılır.</summary>
	public static bool LooksLikeDirectMedia(string uri)
	{
		if (!Uri.TryCreate(uri, UriKind.Absolute, out var u))
			return true;
		var ext = Path.GetExtension(u.AbsolutePath).ToLowerInvariant();
		if (DirectAudioExt.Contains(ext))
			return true;
		// Icecast/Shoutcast radyoları genelde 8000, 8080, 9000 gibi portlarda yayın yapar
		if (!u.IsDefaultPort)
			return true;
		var path = u.AbsolutePath.ToLowerInvariant();
		return path.Contains("/stream") || path.Contains("/live") && !u.Host.Contains("youtube") || path.EndsWith("/;");
	}

	public MatchCertainty MatchResource(ResolveContext _, string uri)
	{
		if (!uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			return MatchCertainty.Never;
		if (FrixChat.YoutubeId(uri) != null)
			return MatchCertainty.Never;
		return LooksLikeDirectMedia(uri) ? MatchCertainty.Never : MatchCertainty.Always;
	}

	public Task<PlayResource> GetResource(ResolveContext ctx, string uri, CancellationToken cancellationToken)
		=> GetResourceById(ctx, new AudioResource(uri, null, ResolverFor), cancellationToken);

	public Task<PlayResource> GetResourceById(ResolveContext _, AudioResource resource, CancellationToken cancellationToken)
	{
		resource.ResourceTitle ??= ShortTitle(resource.ResourceId);
		return Task.FromResult(new PlayResource(YtdlpStream.MakeUri(resource.ResourceId), resource,
			songInfo: new SongInfo { Title = resource.ResourceTitle }));
	}

	public string RestoreLink(ResolveContext _, AudioResource resource) => resource.ResourceId;

	private static string ShortTitle(string uri)
	{
		if (Uri.TryCreate(uri, UriKind.Absolute, out var u))
			return u.Host + u.AbsolutePath.TrimEnd('/');
		return uri;
	}

	public void Dispose() { }
}
