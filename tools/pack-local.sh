#!/usr/bin/env bash
# Packs Tiki.Shared and every gRPC contract package into the repo-root local feed, so a
# sibling service repo can consume them as *packages* while they are still unreleased.
#
#   ./tools/pack-local.sh
#
# This exists to keep the one rule in README.md honest. A service that project-references
# ../tiki-shared-lib/src/... compiles against whatever is on disk, which means it never
# discovers that it depends on an unreleased API — and its Docker build needs the sibling
# repo mounted, which is why tiki-identity-api's compose file had to set `context: ..`.
# Packing locally keeps the dependency a versioned package everywhere.
set -euo pipefail

cd "$(dirname "$0")/.."

FEED="${TIKI_LOCAL_FEED:-$(cd .. && pwd)/.local-packages}"
mkdir -p "$FEED"

echo "Packing into $FEED"

dotnet pack src/Tiki.Shared/Tiki.Shared.csproj --configuration Release --output "$FEED" --nologo -v q

for proj in src/Grpc.Contracts/*/*.csproj; do
  dotnet pack "$proj" --configuration Release --output "$FEED" --nologo -v q
done

echo
echo "Local feed contents:"
ls -1 "$FEED"/*.nupkg | sed 's|.*/|  |'
echo
echo "Consume it by adding this source to a service's nuget.config:"
echo "  <add key=\"tiki-local\" value=\"$FEED\" />"
