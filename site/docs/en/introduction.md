# Introduction

Frix is a self-hosted bot for **TeamSpeak 3 and TeamSpeak 6** servers. It joins your server like a normal client and
streams audio directly with the Opus codec, so it needs no TeamSpeak client, no VLC and no virtual audio cable — it runs
headless on a Windows or Linux server, in Docker or on Pterodactyl.

Everything is managed from a **web panel** in the browser: bots, music, radios, plugins, texts, users and updates.

## What it can do

| Area | Highlights |
|---|---|
| Music | YouTube search and links, live streams (YouTube, Twitch), SoundCloud, internet radio, your own uploaded files, playlists, favourites, autoplay, volume levelling, vote skip, DJ mode |
| Moderation | AFK mover, nickname filter, channel guard, anti-flood |
| Server tools | Registration bot, temporary channels, live channel names (online count, record, time), support notifier |
| Community | Welcome messages, rotating and scheduled announcements, activity ranks, polls, fun commands |
| Automation | Scheduled playback, JavaScript scripts, custom commands |
| Panel | Dashboard, connection status, statistics, users and roles, backup & restore, one-click updates, 9 languages |

## How it is organised

- **Bots** — one Frix install can run several bots. Each bot is its own TeamSpeak client with its own server address,
  channel and nickname (e.g. a music bot and a moderation bot).
- **Plugins** — 10 plugins, switched on per bot. A plugin groups related features; each feature has its own switch,
  settings and commands. See [Plugins](#plugins).
- **Commands** — chat commands in TeamSpeak (`!play`, `!radio`, …). A command only works while its plugin is on.
  See [Commands](#commands).
- **Panel users** — people who can open the web panel, with the roles *admin*, *DJ* and *viewer*. They are separate from
  TeamSpeak users.

## Requirements

- A TeamSpeak 3 or TeamSpeak 6 server the bot can connect to (address and, if set, the server password).
- **Windows** 10 / 11 / Server (x64), or **Linux** x64 / arm64 (Debian, Ubuntu, Fedora, Arch, Alpine), or **Docker**.
- About 150 MB of RAM per install plus a little per bot. ffmpeg, yt-dlp and Opus are included or installed automatically.
- One free TCP port for the panel (default **58913**).

## Next steps

1. [Install Frix](#installation) and open the panel.
2. Add a bot with your server address.
3. Turn on the [plugins](#plugins) you need.
4. Type `!help` in TeamSpeak.
