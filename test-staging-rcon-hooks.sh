#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
GENERATED="${CARBON_HOOKGEN_GENERATED:-$ROOT/release/.tmp/staging-hookgen/generated}"
MANAGED="${CARBON_STAGING_MANAGED_PUBLICIZED:-$ROOT/rust/linux/RustDedicated_Data/Managed}"
RAW_MANAGED="${CARBON_STAGING_MANAGED_PATH:-/home/johnm/rust-staging-autoupdate/server/RustDedicated_Data/Managed}"
while (($#)); do
  case "$1" in
    --generated-dir) GENERATED="${2:?Missing generated path}"; shift 2 ;;
    --managed) MANAGED="${2:?Missing managed path}"; shift 2 ;;
    --raw-managed) RAW_MANAGED="${2:?Missing raw managed path}"; shift 2 ;;
    --help|-h) echo 'Usage: ./test-staging-rcon-hooks.sh [--generated-dir PATH] [--managed PATH] [--raw-managed PATH]'; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done
for tool in dotnet python3; do command -v "$tool" >/dev/null || { echo "Missing tool: $tool" >&2; exit 1; }; done
for file in "$GENERATED/Server.cs" "$GENERATED/_Patches.cs" "$MANAGED/Facepunch.Rcon.dll" "$RAW_MANAGED/Facepunch.Rcon.dll"; do
  [[ -f "$file" ]] || { echo "Missing required input: $file" >&2; exit 1; }
done
WORK="$(mktemp -d /tmp/carbon-rcon-semantics.XXXXXX)"
trap 'rm -rf -- "$WORK"' EXIT
python3 - "$GENERATED" "$WORK" <<'PY'
from pathlib import Path
import sys
source,output=map(Path,sys.argv[1:])
for filename,hook,kind in [('Server.cs','OnRconConnection [web]','GeneratedBase'),('_Patches.cs','OnRconConnection [web, patch]','GeneratedPatch')]:
 text=(source/filename).read_text();marker=text.index('"'+hook+'"')
 start=text.index('public static IEnumerable<CodeInstruction> Transpiler(',marker)
 brace=text.index('{',start);depth=1;end=brace+1
 while depth:
  if text[end]=='{':depth+=1
  elif text[end]=='}':depth-=1
  end+=1
 (output/(kind+'.cs')).write_text('using System.Reflection; using System.Reflection.Emit; using HarmonyLib;\ninternal static class '+kind+' {\n'+text[start:end]+'\n}\n')
PY
PROJECT="$ROOT/src/Carbon.Tools/Carbon.StagingHookVerifier.RconSemanticTests/Carbon.StagingHookVerifier.RconSemanticTests.csproj"
NUGET_HTTP_CACHE_PATH="$WORK/nuget-cache" dotnet run --project "$PROJECT" -c Release -p:GeneratedHookSources="$WORK" -- "$MANAGED"
dotnet run --no-build --project "$PROJECT" -c Release -p:GeneratedHookSources="$WORK" -- "$RAW_MANAGED"
