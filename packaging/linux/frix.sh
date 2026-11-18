#!/usr/bin/env bash
# Frix başlatıcı (Linux)
# - Bot kapanırsa / çökerse yeniden başlatır
# - Panelden indirilen güncellemeyi (_update/) kurar; kullanıcı verilerine dokunmaz
set -u
cd "$(dirname "$(readlink -f "$0")")" || exit 1
chmod +x TS3AudioBot ytdlp/yt-dlp ytdlp/deno 2>/dev/null

apply_update() {
	echo "[frix] Güncelleme kuruluyor..."
	(cd _update && find . -type f) | while IFS= read -r f; do
		case "$f" in
			./bots/*|./logs/*|./ts3audiobot.toml|./rights.toml|./frix.db*|./youtube_cookies.txt|./ytdlp/yt-dlp.conf)
				[ -e "$f" ] && continue ;;
		esac
		mkdir -p "$(dirname "$f")"
		cp -f "_update/$f" "$f"
	done
	rm -rf _update
	chmod +x TS3AudioBot frix.sh ytdlp/yt-dlp ytdlp/deno 2>/dev/null
	echo "[frix] Güncelleme tamamlandı."
}

stop=0
trap 'stop=1; [ -n "${pid:-}" ] && kill -TERM "$pid" 2>/dev/null' TERM INT

fails=0
while [ $stop -eq 0 ]; do
	[ -d _update ] && [ -f _update/TS3AudioBot ] && apply_update
	started=$(date +%s)
	./TS3AudioBot --non-interactive --stats-disabled &
	pid=$!
	wait "$pid"
	code=$?
	pid=""
	[ $stop -ne 0 ] && break

	case $code in
		3) echo "[frix] Panelden yeniden başlatma istendi."; sleep 1; fails=0; continue ;;
		4) echo "[frix] Güncelleme için yeniden başlatılıyor."; sleep 1; fails=0; continue ;;
	esac

	if [ $(( $(date +%s) - started )) -lt 60 ]; then fails=$((fails + 1)); else fails=0; fi
	delay=3; [ $fails -ge 4 ] && delay=60
	echo "[frix] Bot kapandı (kod $code), $delay sn sonra yeniden başlatılıyor..."
	sleep $delay
done
