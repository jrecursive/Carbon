#!/usr/bin/env bash
set -euo pipefail
root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
generated="${CARBON_HOOKGEN_GENERATED:-$root/release/.tmp/staging-hookgen/generated}"
managed="${CARBON_STAGING_MANAGED_PUBLICIZED:-$root/rust/linux/RustDedicated_Data/Managed}"
raw_managed="${CARBON_STAGING_MANAGED_PATH:-/home/johnm/rust-staging-autoupdate/server/RustDedicated_Data/Managed}"
while (($#)); do
  case "$1" in
    --generated-dir) generated="${2:?Missing generated path}"; shift 2 ;;
    --managed) managed="${2:?Missing managed path}"; shift 2 ;;
    --raw-managed) raw_managed="${2:?Missing raw path}"; shift 2 ;;
    --help|-h) echo 'Usage: ./test-staging-construction-hooks.sh [--generated-dir PATH] [--managed PATH] [--raw-managed PATH]'; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done
for tool in dotnet python3; do command -v "$tool" >/dev/null || { echo "Missing tool: $tool" >&2; exit 1; }; done
for file in "$generated/Structure.cs" "$generated/__GeneratorRuntime.cs" "$managed/Assembly-CSharp.dll" "$raw_managed/Assembly-CSharp.dll"; do
  [[ -f "$file" ]] || { echo "Missing required input: $file" >&2; exit 1; }
done
scratch="$(mktemp -d /tmp/carbon-construction-semantics.XXXXXX)"
trap 'rm -rf -- "$scratch"' EXIT
python3 - "$generated" "$scratch" <<'PY'
from pathlib import Path
import shutil,sys
source,output=map(Path,sys.argv[1:])
text=(source/'Structure.cs').read_text()
marker=text.index('"OnConstructionPlace"')
start=text.index('public static IEnumerable<CodeInstruction> Transpiler(',marker)
brace=text.index('{',start);depth=1;end=brace+1
while depth:
 if text[end]=='{':depth+=1
 elif text[end]=='}':depth-=1
 end+=1
(output/'Generated.cs').write_text('using System.Reflection;using System.Reflection.Emit;using HarmonyLib;using Carbon.Hooks;\ninternal static class ConstructionGeneratedHook {\n'+text[start:end]+'\n}\n')
(output/'generated').mkdir()
shutil.copyfile(source/'__GeneratorRuntime.cs',output/'generated/__GeneratorRuntime.cs')
PY
project="$root/src/Carbon.Tools/Carbon.StagingHookVerifier.ConstructionSemanticTests/ConstructionSemanticTests.csproj"
NUGET_HTTP_CACHE_PATH="$scratch/nuget-cache" dotnet run --project "$project" -c Release \
  -p:GeneratedHookSources="$scratch" -p:RustManaged="$managed" -p:TreatWarningsAsErrors=true -- "$managed"
dotnet run --no-build --project "$project" -c Release \
  -p:GeneratedHookSources="$scratch" -p:RustManaged="$managed" -- "$raw_managed"
