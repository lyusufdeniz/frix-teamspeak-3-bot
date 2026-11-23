# Administration

## Users and roles

Panel → **Users**. Create accounts for everyone who should use the panel and give them a role: *admin* (everything),
*DJ* (music, radios, playlists) or *viewer* (look only). Every user can change their own password from the account menu.
An admin can reset other users' passwords.

Panel users are separate from TeamSpeak users. Who may use chat commands in TeamSpeak is decided by the plugins (e.g. DJ
mode, staff groups of the registration bot) and by `rights.toml` for TS3AudioBot's own commands.

## Several bots

Panel → **Bots → New bot** adds another TeamSpeak client, for example a music bot and a moderation bot. Each bot has its
own plugins. Some features should run on **one** bot per server only — *Activity ranks* (time would be counted twice) and
*Server activity*.

## Backup and restore

Panel → **Settings → Backup**.

- **Create a backup**: choose the parts — plugin settings, bot texts, commands, radios, playlists and favourites, general
  settings, avatar images and (optional, can be large) statistics — and download one JSON file.
- **Restore from backup**: choose a backup file; Frix shows what is inside and which version made it, you pick the parts,
  and their current content is replaced.

Panel users and passwords, the bots' connection settings and the library's music files are not part of a backup. To move
everything to another machine, copy the whole Frix folder (or the Docker volume) while Frix is stopped.

## Updates

Panel → **Settings → Updates → Check for updates** shows the installed and the latest version with the release notes.
*Update now* downloads the package with a progress bar and installs it on the restart. Updates always come from the
official repository. Docker users can also pull the new image; Pterodactyl users can reinstall the server.

## YouTube

YouTube changes often and sometimes blocks servers.

- **Update yt-dlp** (Settings → YouTube) fixes most "no sound" and "video unavailable" problems.
- **YouTube cookies**: if the log says *Sign in to confirm you're not a bot*, export `cookies.txt` for youtube.com with the
  *Get cookies.txt LOCALLY* browser extension while logged in (preferably with a secondary Google account) and upload it in
  Settings → YouTube.

## Files and folders

Everything is kept in the Frix folder (Docker: `/data`):

| Path | Content |
|---|---|
| `frix.db` | Panel users, plugin settings, texts, commands, radios, playlists, statistics |
| `bots/<name>/bot.toml` | Connection settings of each bot |
| `ts3audiobot.toml`, `rights.toml` | Core settings and TS3AudioBot command rights |
| `music-library/` | Uploaded music files |
| `scripts/` | Scripts (`.js`) |
| `avatars/` | Own avatar images |
| `logs/` | Log files |
| `ytdlp/` | yt-dlp and its config |
| `youtube_cookies.txt` | Uploaded YouTube cookies |
| `KURULUM_KODU.txt` | One-time setup code (deleted after setup) |

## Ports and environment

| Setting | Default | Notes |
|---|---|---|
| Panel port | `58913` | `ts3audiobot.toml` → `[web] port`, or the `FRIX_WEB_PORT` environment variable (Docker, Pterodactyl) |
| Listening addresses | all | `ts3audiobot.toml` → `[web] hosts` |
| TeamSpeak | — | The bot connects out to your server's voice port (default `9987/udp`) |

## Security

- Passwords are stored as PBKDF2-SHA256 hashes; sessions use HttpOnly, SameSite=Strict cookies and a CSRF check.
- After 5 wrong logins an address is locked for 10 minutes.
- The first-time setup from another computer needs the one-time setup code.
- The panel itself speaks plain HTTP: use a reverse proxy with HTTPS when it is reachable from the internet
  ([example](#installation)).
