# Troubleshooting

Most answers are in Panel → **Logs**: yellow lines are warnings, red lines are errors.

## Sound and music

**A song is found but nothing plays.** Look for the reason in the log. Usually YouTube is blocking: Settings → YouTube →
*Update yt-dlp*; if that does not help, upload YouTube cookies ([how](#administration)). No audio is sent while the bot
is still connecting.

**"Sign in to confirm you're not a bot".** YouTube blocks the server's IP address. Upload YouTube cookies.

**ffmpeg could not play the song.** The log line after it shows yt-dlp's reason (age limit, region lock, removed video,
live stream that ended…).

**Some songs are much louder than others.** Turn on *Volume levelling* in the Music plugin; it applies from the next song.

**A radio station doesn't play.** Use a direct stream address (`.mp3`, `.aac`, Icecast/Shoutcast) or an `.m3u` / `.pls` /
`.m3u8` link, not a web page. Test it in VLC first.

**No sound on Linux / "libopus".** `sudo apt install libopus0 ffmpeg` (Debian/Ubuntu). `install.sh` and the Docker image
include them.

## Connection

**The bot can't connect.** Check `host:port` (default port 9987) and the server password. On the first connection the
server may ask for an identity security level; the bot computes it (10–60 seconds). The log shows the reason. The
[connection status](#panel) dialog shows ping and packet loss once it is connected.

**The bot appears as "Frix1".** Another client with the same nickname is online (an old bot, or a reconnect before the old
session timed out). The bot gets its name back on the next connection.

**The bot drops and doesn't come back.** Turn on the *Auto reconnect* plugin. It retries after timeouts, server restarts and
kicks (if allowed), never after a ban.

## Panel

**The panel is not reachable from outside.** Open TCP 58913 in the firewall (`ufw allow 58913/tcp`, Windows Firewall) and in
your cloud provider's network rules.

**I forgot the admin password.** Another admin can reset it in Panel → Users. If there is no other admin, stop Frix, delete
`frix.db` and start again — plugin settings, texts, radios and playlists are lost (restore them from a backup); the bots
are kept.

**A dropdown or a page looks old after an update.** Reload the page with Ctrl+F5.

## Plugins

**A command says "this feature is off".** Turn on the plugin and the feature for this bot (Panel → Plugins).

**The AFK mover moves nobody.** The AFK channel must exist (pick it from the list) and the bot's move power must be higher
than the users'. Server admins can't be moved — add their group to *Excluded groups*. Checks run every 30 seconds.

**The nickname filter catches the wrong people.** Words match anywhere in the nickname, case-insensitive. Avoid very short
words, add exempt groups and start with *poke* before switching to *kick*.

**The channel name doesn't change.** The bot needs `b_channel_modify_name` on that channel. Names are cut at 40 characters.
Pick the channel from the list (by ID), because its name changes.

**Activity ranks don't count / `!top` is empty.** Turn ranks on, on one bot only. Time is added once a minute. Ranks are
written as hours → server group in the rank editor.

**The temporary channel isn't created.** The bot needs `b_channel_create_temporary` and move power. A user gets at most one
channel every 30 seconds.

**The welcome message is sent to everyone when the bot starts.** It isn't: joins in the first 15 seconds after connecting
are ignored, and a user isn't greeted again before the repeat time.

**The avatar doesn't change.** The server limits the avatar file size (`i_client_max_avatar_filesize`); the log says
*Avatar could not be uploaded* with the reason.

**Commands of a disabled plugin still work.** Update to the latest version; since 2.16.3 TS3AudioBot's own commands
(`!play`, `!next`, …) follow the plugin switches too.

## TeamSpeak permissions

The permissions each feature needs are listed under the feature in [Plugins](#plugins). The quickest setup is a server
group for the bot with the moves, pokes, kicks and group assignments your features use, and a higher
`i_client_move_power` / `i_group_member_add_power` than the groups it should manage.
