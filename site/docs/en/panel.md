# Web panel

The panel is a single page in the browser. The left sidebar holds the **bot picker** (the bot the pages work on), the
pages and, at the bottom, a mini player, the version, a GitHub link and the support button. The top bar has the page
title and actions, the light/dark theme switch and your account menu. On phones the sidebar becomes a menu.

## Dashboard

- **Tiles**: bots online, plays today and this week (with the change), online users, connection quality, uptime. Every
  tile has an *open* button that leads to the related page. Clicking **Online now** lists who is online (name, channel,
  away / microphone / sound state); clicking **Connection** opens the [connection status](#panel) dialog.
- **Selected bot / All bots** switch in the top bar: the dashboard shows the bot chosen in the sidebar, or all bots together.
- **System**: server CPU and Frix CPU of the last 3 minutes, RAM, Frix memory and threads.
- **Server activity**: online users of the last 24 hours and a heat map of the busiest hours (needs the *Server activity*
  feature of the Activity plugin; when it is off the card is blurred and tells you how to turn it on).
- **Last 14 days**, **Top songs** and the list of bots.

### Connection status

Per bot, refreshed every 2 seconds: ping (current, average, jitter), packet loss, download / upload rate and the last
minute's traffic, a ping history chart, connected time, server version and platform, number of clients, the bot's
nickname and channel with codec, audio bitrate and packet counts.

## Bots

Cards for every bot with status, server, channel, audio quality, the playing song and the enabled plugins. Start, stop,
restart, edit or delete a bot here. The **settings** of a bot are the fields described in
[Adding a bot](#installation); after the bot has connected once, the channel is picked from a list.

## Player

The selected bot's player: search (YouTube results with thumbnails), add by link, play now or queue, the queue with
reorder and remove, seek, volume, pause, next, repeat and shuffle. "Adding song" progress is shown while a song loads.

## Playlists

Saved playlists: open, play, remove songs. Playlists are also made in TeamSpeak with `!playlist save <name>` and from the
library.

## Radio

Internet radio stations: name, stream address and genre. Add them one by one or **import** an `.m3u` / `.m3u8` list
(genres are read from `group-title`). A station plays on the selected bot with *Play* or in TeamSpeak with `!radio <name>`.
Use direct stream addresses (`.mp3`, `.aac`, Icecast/Shoutcast, `.m3u`, `.pls`, `.m3u8`), not web pages.

## Library

Your own music files: upload (several at once, up to 200 MB each — mp3, ogg, opus, wav, flac, m4a, aac, webm, wma), search,
play, queue, add to a playlist, rename and delete. Files are stored in the `music-library` folder. In TeamSpeak:
`!file <name>`.

## Plugins

The 10 plugins of the selected bot, in three groups (Core, Community, Extras). The switch turns a plugin on or off;
clicking it opens its dialog with one card per feature. Each card has its own switch, its settings, rarely used settings
under *More settings* and the feature's commands, which you can rename. The **General commands** tile holds the commands
that belong to no plugin. Details: [Plugins](#plugins).

Sub-pages in the sidebar: **Custom commands**, **Bot texts** and **Scripts** — see [Texts & commands](#customization) and
[Scripts](#scripts).

## Users

Panel accounts and their roles:

| Role | Can do |
|---|---|
| Admin | Everything: bots, plugins, users, settings, updates |
| DJ | Add songs and radios, control playback, manage playlists |
| Viewer | Look only |

## Settings

One page with collapsible sections: **General** (language, theme), **YouTube** (update yt-dlp, upload cookies),
**Updates**, **Backup** (backup and restore) and **System** (version, operating system, restart).

## Logs and statistics

**Logs**: the live log, earlier log files and the audit log (who changed what in the panel). Clearing logs also deletes
the log files. **Stats**: most played songs, top requesters and most active users for 7 days, 30 days, a year or all time,
per bot or for all bots.
