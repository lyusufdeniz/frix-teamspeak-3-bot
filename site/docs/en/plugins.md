# Plugins

Plugins are switched on **per bot**: Panel → **Plugins** → pick the bot in the sidebar → click a plugin. Plugins with several features show each feature as a card with its own switch, settings and commands. Channel fields offer the bot's channels as a list once the bot has connected; you can also type a channel name or ID (`5` or `/5`). Group fields take comma-separated server group IDs (TeamSpeak → *Permissions → Server Groups*). Message fields support BBCode and open an editor with a live preview.

## Music

Playing songs and music features: commands, volume levelling, autoplay, favourites, vote skip, DJ mode, pause when alone, the playing song as the bot's name / description.

### Music

*on by default*

Playing songs from YouTube/links: !play, !link, !playlist, !stats and controls like !next, !pause, !vol. Turning it off disables these commands (controls keep working while Radio is on).

| Setting | Default | Notes |
|---|---|---|
| Play now (don't queue) | off | When on, !play and !link stop the current song and play the new one right away. The rest of the queue is kept. |
| Announce the playing song in the channel | on | Posts a 'Now playing' message to the channel when each song starts (text: Bot texts → nowplaying). |

**TeamSpeak permission:** `b_client_use_channel_commander` (only if channel commander is on in the bot settings)

### Volume levelling

*on by default*

Brings songs recorded too loud or too quiet to the same level. Changes apply from the next song.

| Setting | Default | Notes |
|---|---|---|
| Target level | normal | quiet / normal / loud — quiet: softer, normal: recommended, loud: louder |

### Autoplay

*off by default*

When the queue ends the bot keeps going with songs similar to the last one. It does not continue after !stop.

| Setting | Default | Notes |
|---|---|---|
| Only when someone is in the channel | on |  |
| Announce in the channel | on |  |
| Don't repeat the last N songs | `30` |  |

### Favourites

*on by default*

!fav adds the playing song to your favourites, !favs queues your favourites. !favs list shows them, !fav del <no> removes one.

| Setting | Default | Notes |
|---|---|---|
| Max favourites per user | `200` |  |
| !favs queues in random order | off |  |

### Vote skip

*off by default*

!next doesn't skip immediately; the song is skipped once a share of the listeners in the bot's channel voted.

| Setting | Default | Notes |
|---|---|---|
| Required vote share (%) | `50` |  |
| The requester can skip without a vote | on |  |
| Group IDs that skip without a vote | empty |  |

### DJ mode & queue limit

*off by default*

Only lets certain groups add songs and limits how many songs each user can queue.

| Setting | Default | Notes |
|---|---|---|
| DJ group IDs (empty = everyone can add) | empty |  |
| Max queued songs per user (0 = unlimited) | `3` |  |
| Skip/pause/volume/stop for DJs only too | off |  |

### Pause when alone

*off by default*

Pauses music when nobody is left in the bot's channel and resumes when someone joins.

| Setting | Default | Notes |
|---|---|---|
| Resume when someone joins | on |  |
| Stop completely after minutes (0 = never) | `0` |  |

### Song → bot name/description

*off by default*

Shows the playing song as the bot's TeamSpeak nickname and/or description. Restores them when playback stops.

| Setting | Default | Notes |
|---|---|---|
| Change the nickname | on |  |
| Nickname format | `♪ {title}` | Use {title}, {artist}, {song}, {user}, {bot}. TeamSpeak nicknames are max 30 characters; longer ones are cut. |
| Change the description | on |  |
| Description format | `🎵 {title} · {user}` | Use {title}, {artist}, {song}, {user}, {bot}. {artist}: the artist in "Artist - Song" titles; {song}: the song name. |
| Description when idle | empty | Leave empty to clear the description. |

**TeamSpeak permission:** `b_client_modify_own_description` (for the description)

## Radio

Radios added in the panel (!radio) and a radio when the queue is idle.

### Radio

*on by default*

Plays the radios added in the panel with !radio <name>. Turning it off disables !radio.

| Setting | Default | Notes |
|---|---|---|
| Play now (don't queue) | on | When on, the radio starts right away and the current song stops. When off, the radio is added to the end of the queue. |
| Announce the radio in the channel | on | Posts a message to the channel when a radio starts (text: Bot texts → nowplaying.radio). |

### Idle radio

*off by default*

Plays a chosen radio station when the queue is empty for a while.

| Setting | Default | Notes |
|---|---|---|
| Radio station | empty |  |
| Idle minutes before playing | `5` |  |

## Avatar

The bot's avatar for idle, music and radio separately (command card, song cover or your own image).

| Setting | Default | Notes |
|---|---|---|
| When idle | command card | command card / own image / none |
| Idle image | — |  |
| While music plays | song cover | song cover / own image / idle avatar |
| Music image | — |  |
| While a radio plays | idle avatar | idle avatar / own image |
| Radio image | — |  |

**TeamSpeak permission:** `i_client_max_avatar_filesize` high enough for the image (the command card is about 30 KB)

## Auto reconnect

When the bot drops it reconnects by itself and rejoins the same channel.

| Setting | Default | Notes |
|---|---|---|
| First retry (seconds) | `3` | Later retries back off up to 2 minutes. |
| Max retries (0 = unlimited) | `0` |  |
| Rejoin the same channel | on | If off, the bot joins its default channel from the bot settings. |
| Also reconnect after being kicked from the server | on |  |
| Retry when the server shuts down or restarts | on |  |

## Welcome & announcements

Welcome messages, rotating announcements/ads and scheduled messages.

### Welcome message

*off by default*

Greets users who connect to the server or join the bot's channel.

| Setting | Default | Notes |
|---|---|---|
| When | server | server / channel / both — server: on connect · channel: on joining the bot's channel |
| Delivery | private message | private message / poke / channel |
| Message | `[b][color=#00FF00]👋 Hoş geldin {user}! {serve…` | Variables: {user} {server} {channel} {time} {date} |
| Repeat for same user (minutes) | `60` |  |

**TeamSpeak permission:** `i_client_poke_power` (only for the *poke* delivery)

### Announcements

*off by default*

Rotating announcement/ad messages at an interval, plus scheduled messages on specific days and times.

| Setting | Default | Notes |
|---|---|---|
| Rotating messages (one per line) | `[b][color=#00FF00]🎵 Müzik botunu kullanmak iç…` | Leave empty to send only scheduled messages. |
| Interval (seconds) | `1800` | At least 30 seconds. E.g. 1800 = 30 minutes. |
| Order | in order | in order / random |
| Target | channel | channel / server |
| Scheduled messages (one per line: [days] HH:MM \| message) | empty | E.g. 21:00 \| 🎮 Event starts! — sat,sun 12:00 \| ☀️ Have a nice weekend! Days: mon tue wed thu fri sat sun; empty = every day. |

**TeamSpeak permission:** `b_client_server_textmessage_send` (only for messages to the whole server)

## Scheduled playback

Starts a radio or playlist, stops or sets the volume at set days and times.

| Setting | Default | Notes |
|---|---|---|
| Plan | plan editor | Actions: radio <name>, playlist <name>, stop, volume <0-100>. Days: mon tue wed thu fri sat sun; empty = every day. |

## Moderation

AFK mover, nickname filter, channel guard and anti-flood.

### AFK mover

*off by default*

Moves idle, away or speaker-muted users to an AFK channel and brings them back when they return.

| Setting | Default | Notes |
|---|---|---|
| AFK channel | `AFK` |  |
| Idle time (minutes, 0 = off) | `15` |  |
| Move users set to away | on |  |
| Move users with speakers muted | off |  |
| Move back when active again | on |  |
| Excluded server group IDs (comma separated) | empty | E.g. 6, 9 (admins are not moved) |
| Private message to moved user (empty = none) | `💤 AFK olduğun için AFK kanalına taşındın.` |  |

**TeamSpeak permission:** `i_client_move_power` higher than the users' `i_client_needed_move_power`

### Nickname filter

*off by default*

Warns or kicks users whose nickname contains a forbidden word.

| Setting | Default | Notes |
|---|---|---|
| Forbidden words (one per line) | empty |  |
| Action | poke | poke / kick |
| Message | `⚠️ Takma adın sunucu kurallarına uymuyor, lüt…` |  |
| Excluded server group IDs | empty |  |

**TeamSpeak permission:** `i_client_poke_power`, and `i_client_kick_from_server_power` for *kick*

### Channel guard

*off by default*

Moves or kicks users without an allowed group out of protected channels.

| Setting | Default | Notes |
|---|---|---|
| Protected channels | empty |  |
| Allowed group IDs (comma separated) | empty |  |
| Action | kick | kick / move |
| Move to channel (for move) | empty |  |
| Message to the user | `⛔ Bu kanala girme yetkin yok.` |  |

**TeamSpeak permission:** `i_client_move_power` (move) or `i_client_kick_from_channel_power` (kick)

### Anti-flood

*off by default*

Warns users who send too many messages in a short time and kicks repeat offenders (covers chats the bot can see).

| Setting | Default | Notes |
|---|---|---|
| Max messages | `5` |  |
| Within seconds | `8` |  |
| Warnings before kick | `2` |  |
| Final action | channel | channel / server — channel: kick from channel · server: kick from server |
| Warning (poke) | `⚠️ Lütfen flood yapma! ({count}/{max})` |  |
| Kick reason | `Flood` |  |
| Excluded group IDs | empty |  |

**TeamSpeak permission:** `i_client_poke_power`, `i_client_kick_from_channel_power` / `i_client_kick_from_server_power`

## Server tools

Registration, temporary channel creator, channel name info and support notifier.

### Registration

*off by default*

Notifies staff about unregistered users in the registration channel; staff type !register <name> to grant the member group.

| Setting | Default | Notes |
|---|---|---|
| Registration channel | `Kayıt` |  |
| Member group ID to grant | empty |  |
| Unregistered group ID to remove (empty = none) | empty |  |
| Staff group IDs allowed to register (comma separated) | empty |  |
| Channel to move to after registration (empty = don't move) | empty |  |
| Poke to staff | `📝 {user} kayıt bekliyor → !kayit {user}` |  |
| Message to the waiting user | `👋 Hoş geldin {user}! Yetkililer bilgilendiril…` |  |
| Message after registration | `✅ Kaydın tamamlandı, aramıza hoş geldin {user}!` |  |
| Cooldown per user (minutes) | `3` |  |

**TeamSpeak permission:** `i_group_member_add_power`, `i_group_member_remove_power`, `i_client_move_power`, `i_client_poke_power`

### Temporary channel creator

*off by default*

Creates a temporary channel named after users who join the creator channel and moves them in; TeamSpeak deletes it when empty.

| Setting | Default | Notes |
|---|---|---|
| Creator channel | `➕ Kanal oluştur` |  |
| Parent channel (empty = creator's parent) | empty |  |
| Channel name template | `🔊 {user} kanalı` | Variables: {user} {n} |
| Max clients (0 = unlimited) | `0` |  |
| Cooldown per user (seconds) | `30` |  |

**TeamSpeak permission:** `b_channel_create_temporary`, `i_client_move_power`

### Channel name info

*off by default*

Keeps a channel's name updated with the online count and time (bot needs channel edit permission).

| Setting | Default | Notes |
|---|---|---|
| Channel | empty |  |
| Name template | `🎧 Çevrimiçi: {online} \| {time}` | Variables: {online} {record} {max} {time} {date} — max 40 characters. {record}: the most users ever online at once. |
| Update interval (seconds) | `60` | At least 10 seconds. |

**TeamSpeak permission:** `b_channel_modify_name` on that channel

### Support notifier

*off by default*

Pokes online staff when someone joins the support channel.

| Setting | Default | Notes |
|---|---|---|
| Support channel | `Destek` |  |
| Staff server group IDs (comma separated) | empty |  |
| Message to staff | `🆘 {user} destek kanalında bekliyor.` |  |
| Message to the user | `👋 Yetkililere haber verildi ({count} kişi çev…` |  |
| Message if no staff online | `😔 Şu an çevrimiçi yetkili yok, lütfen daha so…` |  |
| Cooldown per user (minutes) | `5` |  |

**TeamSpeak permission:** `i_client_poke_power`

## Activity

Records who is online (dashboard chart) and gives ranks by time spent on the server.

### Server activity

*on by default*

Records how many people are online; the dashboard shows the last 24 hours and the busiest hours.

| Setting | Default | Notes |
|---|---|---|
| Keep data for (days) | `90` |  |

### Activity ranks

*off by default*

Counts time spent on the server and grants server groups at set hours. See !time and !top. Enable on one bot only.

| Setting | Default | Notes |
|---|---|---|
| Ranks | rank editor | E.g. 10=12 → group 12 at 10 hours |
| Remove the previous rank when promoted | on |  |
| Group IDs that never get ranks | empty |  |
| Private message on rank up | `🎖️ Tebrikler {user}! {hours} saate ulaştın ve…` |  |

**TeamSpeak permission:** `i_group_member_add_power` (and `i_group_member_remove_power` to remove the previous rank)

## Fun & tools

!dice, !coin, !pick, !poll, !weather, !fx and !lyrics commands.

| Setting | Default | Notes |
|---|---|---|
| !dice, !coin, !pick | on |  |
| !poll and !vote | on |  |
| Poll duration (minutes) | `2` |  |
| !weather | on |  |
| !fx, !usd, !eur | on |  |
| Currency to convert into | `TRY` |  |
| !lyrics (lyrics) | on |  |
