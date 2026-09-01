#!/usr/bin/env bash
# Packs one named Tiki.Grpc.Contracts.<service> project into a .nupkg under
# artifacts/grpc-contracts/, independent of Tiki.Shared's own pack/release cadence.
#
# Usage: tools/pack-grpc-contract.sh <service>
#   <service> one of: identity, wallet, transaction, compliance, integration (case-insensitive)
set -euo pipefail

usage() {
  echo "Usage: $(basename "$0") <service>" >&2
  echo "  <service> one of: identity, wallet, transaction, compliance, integration" >&2
  exit 1
}

[ $# -eq 1 ] || usage

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

case "$(echo "$1" | tr '[:upper:]' '[:lower:]')" in
  identity)    SERVICE="Identity" ;;
  wallet)      SERVICE="Wallet" ;;
  transaction) SERVICE="Transaction" ;;
  compliance)  SERVICE="Compliance" ;;
  integration) SERVICE="Integration" ;;
  *) echo "error: unknown service '$1'" >&2; usage ;;
esac

PROJECT_DIR="$REPO_ROOT/src/Grpc.Contracts/Tiki.Grpc.Contracts.$SERVICE"
CSPROJ="$PROJECT_DIR/Tiki.Grpc.Contracts.$SERVICE.csproj"
OUTPUT_DIR="$REPO_ROOT/artifacts/grpc-contracts"

[ -f "$CSPROJ" ] || { echo "error: no such project: $CSPROJ" >&2; exit 1; }

mkdir -p "$OUTPUT_DIR"

echo "Packing Tiki.Grpc.Contracts.$SERVICE -> $OUTPUT_DIR" >&2
dotnet pack "$CSPROJ" --configuration Release --output "$OUTPUT_DIR"

echo "$OUTPUT_DIR"
