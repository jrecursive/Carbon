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
    --help|-h) echo 'Usage: ./test-staging-limit-networking.sh [--generated-dir PATH] [--managed PATH] [--raw-managed PATH]'; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done
for tool in dotnet python3; do command -v "$tool" >/dev/null || { echo "Missing tool: $tool" >&2; exit 1; }; done
for file in "$GENERATED/Player.cs" "$GENERATED/Weapon.cs" "$GENERATED/_Patches.cs" "$GENERATED/__GeneratorRuntime.cs" "$MANAGED/Assembly-CSharp.dll" "$RAW_MANAGED/Assembly-CSharp.dll"; do
  [[ -f "$file" ]] || { echo "Missing required input: $file" >&2; exit 1; }
done
WORK="$(mktemp -d /tmp/carbon-limit-networking.XXXXXX)"
trap 'rm -rf -- "$WORK"' EXIT
python3 - "$GENERATED" "$WORK" <<'PY'
from pathlib import Path
import sys
source,output=map(Path,sys.argv[1:])
for filename,hook,kind in [('Weapon.cs','OnImpactEffectCreate','GeneratedImpactBase'),('Weapon.cs','OnWeaponFired','GeneratedProjectileBase'),('Player.cs','IOnBasePlayerAttacked','GeneratedHeadshotBase'),('_Patches.cs','LimitNetworkingNoEffect [patch 1]','GeneratedImpactPatch'),('_Patches.cs','LimitNetworkingNoEffect [patch 2]','GeneratedProjectilePatch'),('_Patches.cs','LimitNetworkingNoEffect [patch 3]','GeneratedHeadshotPatch')]:
 text=(source/filename).read_text();marker=text.index('"'+hook+'"');start=text.index('public static IEnumerable<CodeInstruction> Transpiler(',marker);brace=text.index('{',start);depth=1;end=brace+1
 while depth:
  if text[end]=='{':depth+=1
  elif text[end]=='}':depth-=1
  end+=1
 (output/(kind+'.cs')).write_text('using System.Reflection; using System.Reflection.Emit; using HarmonyLib; using Carbon.Hooks;\ninternal static class '+kind+' {\n'+text[start:end]+'\n}\n')
(output/'__GeneratorRuntime.cs').write_bytes((source/'__GeneratorRuntime.cs').read_bytes())
PY
PROJECT="$ROOT/src/Carbon.Tools/Carbon.StagingHookVerifier.LimitNetworkingTests/Carbon.StagingHookVerifier.LimitNetworkingTests.csproj"
NUGET_HTTP_CACHE_PATH="$WORK/nuget-cache" dotnet run --project "$PROJECT" -c Release -p:GeneratedHookSources="$WORK" -- "$MANAGED"
dotnet run --no-build --project "$PROJECT" -c Release -p:GeneratedHookSources="$WORK" -- "$RAW_MANAGED"
