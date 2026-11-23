# Frix TeamSpeak bot — Docker imajı
#   docker run -d -p 58913:58913 -v frix-data:/data ghcr.io/lyusufdeniz/frix:latest
# Tüm veriler (veritabanı, botlar, kütüphane, scriptler) /data altında tutulur.
# Aynı imaj Pterodactyl'de de çalışır (STARTUP değişkeni varsa /home/container kullanılır).

# ---------------------------------------------------------------- derleme (her mimari için host'ta)
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY . .
RUN RID=$([ "$TARGETARCH" = "arm64" ] && echo linux-arm64 || echo linux-x64) \
 && dotnet publish TS3AudioBot/TS3AudioBot.csproj -c Release -r $RID --self-contained true \
      -p:DebugType=None -p:DebugSymbols=false -o /out --nologo -v q \
 && mkdir -p /out/WebInterface && cp -r FrixPanel/. /out/WebInterface/ \
 && cp packaging/linux/frix.sh /out/ && chmod +x /out/frix.sh /out/TS3AudioBot \
 && mkdir -p /out/ytdlp && cp packaging/yt-dlp.conf /out/ytdlp/ \
 && cp README.md README.tr.md LICENSE /out/ 2>/dev/null || true

# ---------------------------------------------------------------- yt-dlp ve deno
FROM --platform=$BUILDPLATFORM debian:bookworm-slim AS tools
ARG TARGETARCH
RUN apt-get update -qq && apt-get install -y -qq curl unzip ca-certificates >/dev/null
WORKDIR /t
RUN if [ "$TARGETARCH" = "arm64" ]; then Y=yt-dlp_linux_aarch64.zip; D=deno-aarch64-unknown-linux-gnu.zip; else Y=yt-dlp_linux.zip; D=deno-x86_64-unknown-linux-gnu.zip; fi \
 && curl -fsSL -o y.zip "https://github.com/yt-dlp/yt-dlp/releases/latest/download/$Y" && unzip -qo y.zip -d ytdlp \
 && curl -fsSL -o d.zip "https://github.com/denoland/deno/releases/latest/download/$D" && unzip -qo d.zip -d ytdlp \
 && for f in ytdlp/yt-dlp_linux ytdlp/yt-dlp_linux_aarch64; do [ -f "$f" ] && mv "$f" ytdlp/yt-dlp; done; \
    chmod +x ytdlp/yt-dlp ytdlp/deno

# ---------------------------------------------------------------- çalışma
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0
ARG VERSION=dev
ENV FRIX_VERSION=$VERSION \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LANG=C.UTF-8
RUN apt-get update -qq \
 && DEBIAN_FRONTEND=noninteractive apt-get install -y -qq --no-install-recommends ffmpeg libopus0 ca-certificates curl unzip tini >/dev/null \
 && rm -rf /var/lib/apt/lists/* \
 && useradd -m -d /home/container -s /bin/bash container \
 && mkdir -p /data && chown container:container /data
COPY --from=build /out /opt/frix
COPY --from=tools /t/ytdlp/yt-dlp /t/ytdlp/deno /opt/frix/ytdlp/
COPY packaging/docker/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh /opt/frix/ytdlp/yt-dlp /opt/frix/ytdlp/deno
USER container
EXPOSE 58913
VOLUME /data
ENTRYPOINT ["/usr/bin/tini", "--", "/entrypoint.sh"]
