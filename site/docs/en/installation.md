# Installation

Download the package for your system from the [latest release](https://github.com/lyusufdeniz/frix-teamspeak-bot/releases/latest),
or use Docker / Pterodactyl. All packages contain the bot, the web panel, ffmpeg (Windows) and yt-dlp.

## Windows

1. Download `Frix-win-x64.zip` and extract it, for example to `C:\Frix`.
2. Run **`Frix.exe`**. The Frix fox icon appears in the system tray; there is no console window.
3. Open **http://localhost:58913** and create the admin account.

The tray icon opens the panel, restarts the bot, toggles *Start with Windows* (on by default) and quits. If the bot
crashes it is restarted automatically. `konsol-modu.bat` starts the bot with a console window, useful for debugging.

> For a server that reboots without anyone logging in, turn on Windows auto-login (`netplwiz`) so the tray app starts.

## Linux

```bash
wget https://github.com/lyusufdeniz/frix-teamspeak-bot/releases/latest/download/Frix-linux-x64.zip
unzip Frix-linux-x64.zip && cd Frix-linux-x64
sudo bash install.sh
```

Use `Frix-linux-arm64.zip` on ARM servers (Raspberry Pi 4/5, Ampere…). `install.sh`:

- installs `ffmpeg` and `libopus` (apt, dnf, pacman or apk),
- creates the `frix` system user and copies the bot to `/opt/frix` (another folder: `sudo bash install.sh /path`),
- enables and starts the `frix` systemd service,
- prints the panel address and the one-time setup code.

```bash
systemctl status frix        # state
journalctl -u frix -f        # logs (also in the panel)
sudo systemctl restart frix  # restart
sudo ufw allow 58913/tcp     # open the panel port if you use a firewall
```

Running `install.sh` again from a newer package updates Frix and keeps your data.

## Docker

```bash
docker run -d --name frix --restart unless-stopped \
  -p 58913:58913 -v frix-data:/data \
  ghcr.io/lyusufdeniz/frix:latest
```

- All data (database, bots, library, scripts, avatars) lives in the `/data` volume.
- Images are published for `amd64` and `arm64`, tagged with the version (`2.16.7`) and `latest`.
- `FRIX_WEB_PORT` changes the panel port inside the container; change the `-p` mapping to match.
- A ready [`docker-compose.yml`](https://github.com/lyusufdeniz/frix-teamspeak-bot/blob/main/docker-compose.yml) is in the repository.
- Update: `docker pull ghcr.io/lyusufdeniz/frix:latest` and recreate the container. The data stays in the volume.

## Pterodactyl

1. Download [`egg-frix.json`](https://github.com/lyusufdeniz/frix-teamspeak-bot/blob/main/packaging/pterodactyl/egg-frix.json).
2. Pterodactyl → **Admin → Nests → Import Egg**.
3. Create a server with the *Frix TeamSpeak Bot* egg. The panel runs on the server's **primary port**.

The *Frix release* variable selects the version (`latest` or a tag such as `v2.16.7`). Reinstalling the server updates
Frix and keeps the data.

## First start

Open the panel in the browser:

- **On the same machine** (`localhost`): the setup wizard opens directly.
- **From another computer**: the wizard asks for an 8-character **setup code**. It is printed in the log and saved in
  `KURULUM_KODU.txt` in the Frix folder (Linux: `/opt/frix`, Docker: `docker logs frix`). The file is deleted after setup.

The wizard asks for the language and the admin username and password. The language decides the panel, the bot's replies
and the command names.

## Adding a bot

Panel → **Bots → New bot**.

| Field | What to enter |
|---|---|
| Name | Short internal name (letters, digits, `-`, `_`), e.g. `music` |
| Nickname | Name shown in TeamSpeak (3–30 characters) |
| Server address | `host:port`, e.g. `ts.example.com:9987` or `localhost:9987` |
| Channel | The channel to join. After the first connection the channels appear as a list |
| Server password | Only if the server has one |
| Audio quality | 32 / 48 / 64 / 96 kbps — 64 is recommended |
| Channel commander | Shows the bot as channel commander (needs the permission) |
| Start automatically | Starts the bot together with Frix |

On the first connection the server may ask for an identity security level; the bot computes it by itself (this can take
10–60 seconds). Then turn on the [plugins](#plugins) you need for this bot.

## Remote access and HTTPS

The panel listens on TCP **58913** on all interfaces. Open it in your firewall and your cloud provider's network rules to
reach it from outside. The connection is plain HTTP; for access over the internet put it behind a reverse proxy with
HTTPS, for example Caddy:

```
frix.example.com {
  reverse_proxy 127.0.0.1:58913
}
```

## Updating

Panel → **Settings → Updates → Check for updates**. The new version is downloaded with a progress bar and installed on the
next restart (Windows tray app, Linux service and Docker entrypoint all apply it). Your data is never overwritten.
