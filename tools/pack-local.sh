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

# Evict any previously-restored copy of these packages first.
#
# NuGet caches a package by id+version and will not look at the feed again once it has one
# extracted globally. Re-packing the same version number therefore has no effect on a
# consumer that already restored it — the build silently keeps using the older assembly, and
# you debug a bug you already fixed. Bumping the version on every local iteration is the
# alternative, and is worse.
GLOBAL_PACKAGES="${NUGET_PACKAGES:-$HOME/.nuget/packages}"
for pkg in tiki.shared tiki.grpc.contracts.compliance tiki.grpc.contracts.identity \
           tiki.grpc.contracts.integration tiki.grpc.contracts.transaction tiki.grpc.contracts.wallet; do
  rm -rf "${GLOBAL_PACKAGES:?}/${pkg}"
done

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
