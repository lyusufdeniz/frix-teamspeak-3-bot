# Frix integration tests with Frix running natively on Windows (Frix.exe build, not Docker).
# The TeamSpeak server still runs in Docker (Docker Desktop), or pass -TsServer host:port for one you already run.
#   .\tests\integration\run-windows.ps1                       all tests
#   .\tests\integration\run-windows.ps1 "!dice (fun plugin)"  only the named tests
#   .\tests\integration\run-windows.ps1 -Keep                 leave Frix and the TS server running afterwards
# ffmpeg: set FRIX_FFMPEG to an ffmpeg.exe, otherwise it is downloaded (same build as the release package).
param(
	[switch]$Keep,
	[string]$TsServer = "",
	[string]$QueryServer = "127.0.0.1:20011",
	[Parameter(ValueFromRemainingArguments = $true)][string[]]$Tests
)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
$root = Resolve-Path "..\.."
$out = Join-Path $env:TEMP "frix-it-windows"
$port = 18914

$startedTs = -not $TsServer
if ($startedTs) {
	docker compose up -d ts3
	if ($LASTEXITCODE -ne 0) { exit 1 }
	$TsServer = "127.0.0.1:19987"
}

Write-Host "==> Publishing Frix for win-x64 to $out"
if (Test-Path $out) { Remove-Item -Recurse -Force $out }
dotnet publish "$root\TS3AudioBot\TS3AudioBot.csproj" -c Release -r win-x64 --self-contained true -o $out --nologo -v q
if ($LASTEXITCODE -ne 0) { exit 1 }
Copy-Item -Recurse "$root\FrixPanel" "$out\WebInterface"
New-Item -ItemType Directory -Force "$out\ytdlp" | Out-Null
Copy-Item "$root\packaging\yt-dlp.conf" "$out\ytdlp\yt-dlp.conf"
if ($env:FRIX_FFMPEG) {
	Copy-Item $env:FRIX_FFMPEG "$out\ffmpeg.exe"
} else {
	Write-Host "==> Downloading ffmpeg"
	$zip = Join-Path $env:TEMP "frix-it-ffmpeg.zip"
	if (-not (Test-Path $zip)) {
		Invoke-WebRequest -UseBasicParsing -OutFile $zip "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip"
	}
	Add-Type -AssemblyName System.IO.Compression.FileSystem
	$z = [IO.Compression.ZipFile]::OpenRead($zip)
	try { $e = $z.Entries | Where-Object { $_.FullName -like "*/bin/ffmpeg.exe" } | Select-Object -First 1
		[IO.Compression.ZipFileExtensions]::ExtractToFile($e, "$out\ffmpeg.exe", $true) } finally { $z.Dispose() }
}

Write-Host "==> Starting Frix on panel port $port"
$env:FRIX_WEB_PORT = "$port"
$env:FRIX_TRACE_PLUGINS = "1"
$frix = Start-Process -FilePath "$out\TS3AudioBot.exe" -ArgumentList "--non-interactive", "--stats-disabled" -WorkingDirectory $out -PassThru -WindowStyle Hidden

$env:FRIX_PANEL = "http://127.0.0.1:$port"
$env:FRIX_TS = $TsServer
$env:FRIX_QUERY = $QueryServer
$env:FRIX_BOT_ADDRESS = $TsServer
$env:FRIX_HOST_FROM_BOT = "127.0.0.1"
$env:FRIX_STREAM_PORT = "18981"
$env:FRIX_SETUP_CMD = "type $out\KURULUM_KODU.txt"
$ErrorActionPreference = "Continue"
# own output folder: a Docker run of the tests may use the default one at the same time
$runner = Join-Path $env:TEMP "frix-it-runner"
dotnet build Frix.IntegrationTests -c Release -o $runner --nologo -v q
dotnet "$runner\Frix.IntegrationTests.dll" @Tests
$code = $LASTEXITCODE

if (-not $Keep) {
	if (-not $frix.HasExited) { Stop-Process -Id $frix.Id -Force }
	if ($startedTs) { docker compose down -v }
}
exit $code
