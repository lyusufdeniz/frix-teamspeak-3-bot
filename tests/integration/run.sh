#!/usr/bin/env bash
# Frix integration tests: builds Frix as a Docker image, starts it next to an empty TeamSpeak server,
# runs the tests against both and removes everything again.
#   tests/integration/run.sh              all tests
#   tests/integration/run.sh "!dice (fun plugin)"   only the named tests
#   KEEP=1 tests/integration/run.sh       leave the containers running afterwards
set -uo pipefail
cd "$(dirname "$0")"

docker compose up -d --build || { docker compose logs; exit 1; }
dotnet run --project Frix.IntegrationTests -c Release -- "$@"
code=$?
if [ $code -ne 0 ]; then
	echo "---- frix log (last 120 lines)"
	docker compose logs --tail 120 frix
fi
[ "${KEEP:-0}" = "1" ] || docker compose down -v
exit $code
