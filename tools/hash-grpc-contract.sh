#!/usr/bin/env bash
# Computes a SHA-256 over one gRPC contract's compiled RPC signatures and every field of
# every message/enum it defines — sorted, so declaration order and comments never affect
# the hash. Prints just the hex digest to stdout; paste it into a consuming service's own
# drift test. If the published contract's shape ever changes without a version bump, the
# hash recomputed here will stop matching the one pasted there — that's the point.
#
# This script does not run any drift test itself — that belongs in the consuming service's
# own repo, not here. What this repo checks about its own packages lives in
# tests/Grpc.Contracts.Tests/PackageContentsTests.cs (what actually ships), separately.
#
# Usage: tools/hash-grpc-contract.sh <service>
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
  identity)    SERVICE="Identity";    PROTO_FILE="identity.proto" ;;
  wallet)      SERVICE="Wallet";      PROTO_FILE="wallet.proto" ;;
  transaction) SERVICE="Transaction"; PROTO_FILE="transaction.proto" ;;
  compliance)  SERVICE="Compliance";  PROTO_FILE="compliance.proto" ;;
  integration) SERVICE="Integration"; PROTO_FILE="integration.proto" ;;
  *) echo "error: unknown service '$1'" >&2; usage ;;
esac

PROJECT_DIR="$REPO_ROOT/src/Grpc.Contracts/Tiki.Grpc.Contracts.$SERVICE"
CSPROJ="$PROJECT_DIR/Tiki.Grpc.Contracts.$SERVICE.csproj"
PROTOS_DIR="$PROJECT_DIR/Protos"
PROTO_PATH="$PROTOS_DIR/$PROTO_FILE"

[ -f "$PROTO_PATH" ] || { echo "error: no such proto file: $PROTO_PATH" >&2; exit 1; }

# Use the exact Grpc.Tools version this project itself references, so the hash reflects
# the same protoc build that actually compiled it — not whatever else happens to be
# newest in the local NuGet cache.
NUGET_PACKAGES="${NUGET_PACKAGES:-$HOME/.nuget/packages}"
GRPC_TOOLS_VERSION=$(grep -o 'Grpc\.Tools" Version="[^"]*"' "$CSPROJ" | sed -E 's/.*Version="([^"]*)"/\1/')
GRPC_TOOLS_DIR="$NUGET_PACKAGES/grpc.tools/$GRPC_TOOLS_VERSION"

if [ ! -d "$GRPC_TOOLS_DIR" ]; then
  echo "error: Grpc.Tools $GRPC_TOOLS_VERSION not found under $NUGET_PACKAGES — run 'dotnet restore' on $CSPROJ first." >&2
  exit 1
fi

case "$(uname -s)-$(uname -m)" in
  Darwin-x86_64) PLATFORM="macosx_x64" ;;
  Darwin-arm64)  PLATFORM="macosx_x64" ;; # Grpc.Tools ships no native arm64 mac binary; the x64 one runs fine under Rosetta.
  Linux-x86_64)  PLATFORM="linux_x64" ;;
  Linux-aarch64) PLATFORM="linux_arm64" ;;
  *) echo "error: unsupported platform $(uname -s)-$(uname -m)" >&2; exit 1 ;;
esac

PROTOC="$GRPC_TOOLS_DIR/tools/$PLATFORM/protoc"
WELL_KNOWN_INCLUDE="$GRPC_TOOLS_DIR/build/native/include"

[ -x "$PROTOC" ] || { echo "error: protoc not found at $PROTOC" >&2; exit 1; }

WORK_DIR=$(mktemp -d)
trap 'rm -rf "$WORK_DIR"' EXIT

DESCRIPTOR="$WORK_DIR/descriptor.bin"
"$PROTOC" --proto_path="$PROTOS_DIR" --descriptor_set_out="$DESCRIPTOR" "$PROTO_PATH"

echo "Hashing RPC signatures and message/enum fields from $PROTO_FILE ..." >&2

# Materialized to a file, not read via `python3 - <<PY`: python3 reading its own program
# from stdin ('-') would consume the heredoc as the *script*, leaving nothing in stdin for
# the script's own sys.stdin.read() to see — the piped protoc output would be silently lost.
CANONICALIZE="$WORK_DIR/canonicalize.py"
cat > "$CANONICALIZE" <<'PY'
import hashlib
import sys


def parse(text):
    facts = []
    ctx_stack = []

    for raw_line in text.splitlines():
        line = raw_line.strip()
        if not line:
            continue

        if line.endswith("{"):
            ctx_stack.append({"kind": line[:-1].strip(), "data": {}})
            continue

        if line == "}":
            closed = ctx_stack.pop()
            kind, data = closed["kind"], closed["data"]
            owner = ".".join(
                c["data"].get("name", "")
                for c in ctx_stack
                if c["kind"] in ("message_type", "enum_type")
            )

            if kind == "field":
                facts.append(
                    "field|{}|{}|{}|{}|{}|{}".format(
                        owner,
                        data.get("name", ""),
                        data.get("number", ""),
                        data.get("type", ""),
                        data.get("type_name", ""),
                        data.get("label", ""),
                    )
                )
            elif kind == "value":
                facts.append(
                    "enumvalue|{}|{}|{}".format(owner, data.get("name", ""), data.get("number", ""))
                )
            elif kind == "method" and ctx_stack and ctx_stack[-1]["kind"] == "service":
                service_name = ctx_stack[-1]["data"].get("name", "")
                facts.append(
                    "rpc|{}|{}|{}|{}".format(
                        service_name,
                        data.get("name", ""),
                        data.get("input_type", ""),
                        data.get("output_type", ""),
                    )
                )
            continue

        if ":" in line:
            key, _, value = line.partition(":")
            key = key.strip()
            value = value.strip().strip('"')
            if ctx_stack:
                ctx_stack[-1]["data"][key] = value

    return facts


text = sys.stdin.read()
facts = sorted(set(parse(text)))
canonical = "\n".join(facts)
print(hashlib.sha256(canonical.encode("utf-8")).hexdigest())
PY

"$PROTOC" -I "$WELL_KNOWN_INCLUDE" --decode=google.protobuf.FileDescriptorSet google/protobuf/descriptor.proto \
  < "$DESCRIPTOR" \
  | python3 "$CANONICALIZE"
