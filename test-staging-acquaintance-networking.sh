#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
GENERATED="${CARBON_HOOKGEN_GENERATED:-$ROOT/release/.tmp/staging-hookgen/generated}"
MANAGED="${CARBON_STAGING_MANAGED_PUBLICIZED:-$ROOT/rust/linux/RustDedicated_Data/Managed}"
RAW_MANAGED="${CARBON_STAGING_MANAGED_PATH:-/home/johnm/rust-staging-autoupdate/server/RustDedicated_Data/Managed}"
while (($#)); do
  case "$1" in
    --generated-dir) GENERATED="${2:?Missing generated directory}"; shift 2 ;;
    --managed) MANAGED="${2:?Missing managed directory}"; shift 2 ;;
    --raw-managed) RAW_MANAGED="${2:?Missing raw managed directory}"; shift 2 ;;
    --help|-h) echo 'Usage: ./test-staging-acquaintance-networking.sh [--generated-dir PATH] [--managed PATH] [--raw-managed PATH]'; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done
for tool in dotnet python3; do command -v "$tool" >/dev/null || { echo "Missing tool: $tool" >&2; exit 2; }; done
for path in "$GENERATED/_Patches.cs" "$GENERATED/__GeneratorRuntime.cs" "$MANAGED/Assembly-CSharp.dll" "$RAW_MANAGED/Assembly-CSharp.dll"; do
  [[ -f "$path" ]] || { echo "Missing required input: $path" >&2; exit 2; }
done
WORK="$(mktemp -d /tmp/carbon-acquaintance-networking.XXXXXX)"
trap 'rm -rf -- "$WORK"' EXIT
python3 - "$GENERATED" "$WORK" <<'PY'
from pathlib import Path
import sys
source, output = map(Path, sys.argv[1:])
text = (source / '_Patches.cs').read_text()
marker = text.index('"LimitNetworkingAcquaintances [patch]"')
start = text.index('public static IEnumerable<CodeInstruction> Transpiler(', marker)
brace = text.index('{', start)
depth, end = 1, brace + 1
while depth:
    if text[end] == '{': depth += 1
    elif text[end] == '}': depth -= 1
    end += 1
(output / 'GeneratedAcquaintancePatch.cs').write_text(
    'using System.Reflection; using System.Reflection.Emit; using HarmonyLib; using Carbon.Hooks;\n'
    'internal static class GeneratedAcquaintancePatch {\n' + text[start:end] + '\n}\n')
(output / '__GeneratorRuntime.cs').write_bytes((source / '__GeneratorRuntime.cs').read_bytes())
PY
PROJECT="$ROOT/src/Carbon.Tools/Carbon.StagingHookVerifier.AcquaintanceNetworkingTests/Carbon.StagingHookVerifier.AcquaintanceNetworkingTests.csproj"
NUGET_HTTP_CACHE_PATH="$WORK/nuget-cache" dotnet run --project "$PROJECT" -c Release -p:GeneratedHookSources="$WORK" -- "$MANAGED"
dotnet run --no-build --project "$PROJECT" -c Release -p:GeneratedHookSources="$WORK" -- "$RAW_MANAGED"
