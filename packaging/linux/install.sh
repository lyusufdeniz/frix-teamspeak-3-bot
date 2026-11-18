#!/usr/bin/env bash
# Frix Linux kurulumu
#   sudo ./install.sh            -> /opt/frix altına kurar, systemd servisi açar
#   sudo ./install.sh /baska/dizin
set -euo pipefail

if [ "$(id -u)" -ne 0 ]; then
	echo "Lütfen root olarak çalıştırın: sudo ./install.sh" >&2
	exit 1
fi

SRC="$(cd "$(dirname "$(readlink -f "$0")")" && pwd)"
DEST="${1:-/opt/frix}"
SVC_USER="frix"

echo "==> Gerekli paketler kuruluyor (ffmpeg, libopus, ca-certificates)"
if command -v apt-get >/dev/null; then
	apt-get update -qq
	DEBIAN_FRONTEND=noninteractive apt-get install -y -qq ffmpeg libopus0 ca-certificates libicu-dev >/dev/null || \
		DEBIAN_FRONTEND=noninteractive apt-get install -y -qq ffmpeg libopus0 ca-certificates
elif command -v dnf >/dev/null; then
	dnf install -y -q ffmpeg opus ca-certificates libicu || {
		echo "ffmpeg için RPM Fusion deposu gerekebilir: https://rpmfusion.org/Configuration" >&2; }
elif command -v pacman >/dev/null; then
	pacman -Sy --noconfirm --needed ffmpeg opus ca-certificates icu
elif command -v apk >/dev/null; then
	apk add --no-cache ffmpeg opus ca-certificates icu-libs libstdc++ gcompat
else
	echo "Paket yöneticisi bulunamadı. ffmpeg ve libopus'u elle kurun." >&2
fi

echo "==> Kullanıcı: $SVC_USER"
id "$SVC_USER" >/dev/null 2>&1 || useradd --system --home "$DEST" --shell /usr/sbin/nologin "$SVC_USER"

echo "==> Dosyalar kopyalanıyor: $DEST"
mkdir -p "$DEST"
if [ "$SRC" != "$DEST" ]; then
	# Güncellemede kullanıcı verileri (ayarlar, veritabanı, botlar, çerezler) korunur
	(cd "$SRC" && find . -type f ! -name install.sh) | while IFS= read -r f; do
		case "$f" in
			./bots/*|./logs/*|./ts3audiobot.toml|./rights.toml|./frix.db*|./youtube_cookies.txt|./ytdlp/yt-dlp.conf)
				[ -e "$DEST/$f" ] && continue ;;
		esac
		mkdir -p "$DEST/$(dirname "$f")"
		cp -f "$SRC/$f" "$DEST/$f"
	done
fi
chmod +x "$DEST/TS3AudioBot" "$DEST/frix.sh" "$DEST/ytdlp/yt-dlp" "$DEST/ytdlp/deno" 2>/dev/null || true
chown -R "$SVC_USER:$SVC_USER" "$DEST"

echo "==> systemd servisi"
sed "s#/opt/frix#$DEST#g; s#User=frix#User=$SVC_USER#; s#Group=frix#Group=$SVC_USER#" \
	"$SRC/frix.service" > /etc/systemd/system/frix.service
systemctl daemon-reload
systemctl enable --now frix.service

sleep 6
PORT=$(grep -A20 '^\[web\]' "$DEST/ts3audiobot.toml" 2>/dev/null | grep -m1 '^port' | grep -o '[0-9]\+' || echo 58913)
IP=$(hostname -I 2>/dev/null | awk '{print $1}')
echo
echo "✅ Frix kuruldu ve çalışıyor."
echo "   Web panel: http://${IP:-SUNUCU-IP}:${PORT:-58913}"
if [ -f "$DEST/KURULUM_KODU.txt" ]; then
	echo "   İlk kurulum kodu: $(cat "$DEST/KURULUM_KODU.txt")"
fi
echo "   Durum:   systemctl status frix"
echo "   Loglar:  journalctl -u frix -f   (ya da web paneldeki Loglar sayfası)"
echo "   Güvenlik duvarı açıksa: sudo ufw allow ${PORT:-58913}/tcp"
