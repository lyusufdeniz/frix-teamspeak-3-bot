# Frix integration tests (Windows): builds Frix as a Docker image, starts it next to an empty TeamSpeak
# server, runs the tests and removes everything again. Needs Docker Desktop and the .NET 10 SDK.
#   .\tests\integration\run.ps1                       all tests
#   .\tests\integration\run.ps1 "!dice (fun plugin)"  only the named tests
#   .\tests\integration\run.ps1 -Keep                 leave the containers running afterwards
param([switch]$Keep, [Parameter(ValueFromRemainingArguments = $true)][string[]]$Tests)
Set-Location $PSScriptRoot

docker compose up -d --build
if ($LASTEXITCODE -ne 0) { docker compose logs; exit 1 }
dotnet run --project Frix.IntegrationTests -c Release -- @Tests
$code = $LASTEXITCODE
if ($code -ne 0) {
	Write-Host "---- frix log (last 120 lines)"
	docker compose logs --tail 120 frix
}
if (-not $Keep) { docker compose down -v }
exit $code
