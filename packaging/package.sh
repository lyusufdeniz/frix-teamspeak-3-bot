#!/usr/bin/env bash
# Frix sürüm paketi oluşturur.
#   ./packaging/package.sh win-x64      -> dist/Frix-win-x64.zip
#   ./packaging/package.sh linux-x64    -> dist/Frix-linux-x64.zip
# Gerekenler: .NET 10 SDK, curl, unzip, zip (ya da 7z / PowerShell)
set -euo pipefail

RID="${1:?Kullanım: package.sh <win-x64|linux-x64|linux-arm64>}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OS="${RID%%-*}"
ARCH="${RID##*-}"
NAME="Frix-${RID}"
OUT="$ROOT/dist/$NAME"
TMP="$ROOT/dist/_tmp_$RID"

rm -rf "$OUT" "$TMP"
mkdir -p "$OUT" "$TMP"

echo "==> Bot derleniyor ($RID)"
dotnet publish "$ROOT/TS3AudioBot/TS3AudioBot.csproj" -c Release -r "$RID" --self-contained true \
	-p:DebugType=None -p:DebugSymbols=false -o "$OUT" --nologo -v q

if [ "$OS" = "win" ]; then
	echo "==> Tray uygulaması derleniyor"
	dotnet publish "$ROOT/FrixTray/FrixTray.csproj" -c Release -r "$RID" --self-contained true \
		-p:DebugType=None -p:DebugSymbols=false -o "$OUT" --nologo -v q
	cp "$ROOT/packaging/windows/konsol-modu.bat" "$OUT/"
fi

echo "==> Web panel"
rm -rf "$OUT/WebInterface"
mkdir -p "$OUT/WebInterface"
cp -r "$ROOT/FrixPanel/." "$OUT/WebInterface/"

echo "==> yt-dlp ve deno indiriliyor"
mkdir -p "$OUT/ytdlp"
if [ "$OS" = "win" ]; then
	YTDLP_ASSET="yt-dlp_win.zip"
	DENO_ASSET="deno-x86_64-pc-windows-msvc.zip"
elif [ "$ARCH" = "arm64" ]; then
	YTDLP_ASSET="yt-dlp_linux_aarch64.zip"
	DENO_ASSET="deno-aarch64-unknown-linux-gnu.zip"
else
	YTDLP_ASSET="yt-dlp_linux.zip"
	DENO_ASSET="deno-x86_64-unknown-linux-gnu.zip"
fi
curl -fsSL -o "$TMP/ytdlp.zip" "https://github.com/yt-dlp/yt-dlp/releases/latest/download/$YTDLP_ASSET"
unzip -qo "$TMP/ytdlp.zip" -d "$OUT/ytdlp"
# Bazı arşivler tek dosya (yt-dlp_linux) içerir
[ -f "$OUT/ytdlp/yt-dlp_linux" ] && mv "$OUT/ytdlp/yt-dlp_linux" "$OUT/ytdlp/yt-dlp"
[ -f "$OUT/ytdlp/yt-dlp_linux_aarch64" ] && mv "$OUT/ytdlp/yt-dlp_linux_aarch64" "$OUT/ytdlp/yt-dlp"
curl -fsSL -o "$TMP/deno.zip" "https://github.com/denoland/deno/releases/latest/download/$DENO_ASSET"
unzip -qo "$TMP/deno.zip" -d "$OUT/ytdlp"
cp "$ROOT/packaging/yt-dlp.conf" "$OUT/ytdlp/yt-dlp.conf"

if [ "$OS" = "win" ]; then
	echo "==> ffmpeg (Windows) indiriliyor"
	curl -fsSL -o "$TMP/ffmpeg.zip" "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip"
	unzip -qo -j "$TMP/ffmpeg.zip" "*/bin/ffmpeg.exe" -d "$OUT"
else
	cp "$ROOT/packaging/linux/frix.sh" "$ROOT/packaging/linux/install.sh" "$ROOT/packaging/linux/frix.service" "$OUT/"
	chmod +x "$OUT/frix.sh" "$OUT/install.sh" "$OUT/TS3AudioBot" "$OUT/ytdlp/yt-dlp" "$OUT/ytdlp/deno" 2>/dev/null || true
fi

echo "==> Belgeler"
cp "$ROOT/README.md" "$ROOT/README.tr.md" "$ROOT/LICENSE" "$OUT/" 2>/dev/null || true
mkdir -p "$OUT/docs" && cp "$ROOT/docs/"*.md "$OUT/docs/" 2>/dev/null || true

# Gereksiz dosyalar
rm -f "$OUT"/*.pdb "$OUT"/createdump* 2>/dev/null || true

echo "==> Arşivleniyor"
mkdir -p "$ROOT/dist"
rm -f "$ROOT/dist/$NAME.zip"
if command -v zip >/dev/null; then
	(cd "$ROOT/dist" && zip -qry "$NAME.zip" "$NAME")
elif [ -x /c/Windows/System32/tar.exe ]; then
	# Windows'un bsdtar'ı ileri eğik çizgili (her platformda açılan) zip üretir
	(cd "$ROOT/dist" && /c/Windows/System32/tar.exe -a -c -f "$NAME.zip" "$NAME")
else
	powershell.exe -NoProfile -Command "Compress-Archive -Path '$(cygpath -w "$OUT")' -DestinationPath '$(cygpath -w "$ROOT/dist/$NAME.zip")' -Force"
fi
rm -rf "$TMP"
echo "✅ $ROOT/dist/$NAME.zip"
