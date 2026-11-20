#!/usr/bin/env bash
# Frix Docker giriş betiği
# - Pterodactyl: STARTUP değişkeni varsa /home/container içinde onu çalıştırır.
# - Docker: uygulamayı /data'ya kopyalar (imaj sürümü değiştiyse), kullanıcı verilerine dokunmaz ve başlatır.
set -euo pipefail

if [ -n "${STARTUP:-}" ] && [ -d /home/container ]; then
	cd /home/container
	# Pterodactyl {{DEGISKEN}} biçimini ${DEGISKEN} yapar
	MODIFIED_STARTUP=$(echo "${STARTUP}" | sed -e 's/{{/${/g' -e 's/}}/}/g')
	echo ":/home/container$ ${MODIFIED_STARTUP}"
	eval "${MODIFIED_STARTUP}"
	exit $?
fi

DATA=/data
mkdir -p "$DATA"
if [ "$(cat "$DATA/.frix-image" 2>/dev/null || true)" != "${FRIX_VERSION:-dev}" ]; then
	echo "[frix] Uygulama dosyaları güncelleniyor (${FRIX_VERSION:-dev})"
	(cd /opt/frix && find . -type f) | while IFS= read -r f; do
		case "$f" in
			./ts3audiobot.toml|./rights.toml|./ytdlp/yt-dlp.conf)
				[ -e "$DATA/$f" ] && continue ;;
		esac
		mkdir -p "$DATA/$(dirname "$f")"
		cp -f "/opt/frix/$f" "$DATA/$f"
	done
	chmod +x "$DATA/TS3AudioBot" "$DATA/frix.sh" "$DATA/ytdlp/yt-dlp" "$DATA/ytdlp/deno" 2>/dev/null || true
	echo "${FRIX_VERSION:-dev}" > "$DATA/.frix-image"
fi

cd "$DATA"
exec ./frix.sh
