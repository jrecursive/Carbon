#!/usr/bin/env bash
set -euo pipefail
root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
server="${CARBON_STAGING_ROOT:-/home/johnm/rust-staging-autoupdate/server}"
managed="${CARBON_STAGING_MANAGED_PUBLICIZED:-$root/rust/linux/RustDedicated_Data/Managed}"
raw_managed="${CARBON_STAGING_MANAGED_PATH:-$server/RustDedicated_Data/Managed}"
carbon="${CARBON_STAGING_CARBON_MANAGED_PATH:-$server/carbon/managed}"
hooks=""
output=""
while (($#)); do
  case "$1" in
    --hooks-dir) hooks="${2:?Missing hooks directory}"; shift 2 ;;
    --carbon-managed) carbon="${2:?Missing Carbon managed directory}"; shift 2 ;;
    --managed) managed="${2:?Missing publicized managed directory}"; shift 2 ;;
    --raw-managed) raw_managed="${2:?Missing raw managed directory}"; shift 2 ;;
    --output) output="${2:?Missing new verification JSON path}"; shift 2 ;;
    --help|-h) echo 'Usage: ./test-staging-collectible-hooks.sh --hooks-dir PATH [--carbon-managed PATH] [--managed PATH] [--raw-managed PATH] [--output NEW_JSON]'; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done
[[ -n "$hooks" ]] || { echo 'Select the exact candidate --hooks-dir.' >&2; exit 2; }
command -v dotnet >/dev/null || { echo 'Missing tool: dotnet' >&2; exit 2; }
command -v python3 >/dev/null || { echo 'Missing tool: python3' >&2; exit 2; }
command -v tee >/dev/null || { echo 'Missing tool: tee' >&2; exit 2; }
for file in "$hooks/Carbon.Hooks.Oxide.dll" "$carbon/Carbon.Common.dll" "$carbon/Carbon.SDK.dll" "$managed/Assembly-CSharp.dll" "$raw_managed/Assembly-CSharp.dll"; do
  [[ -f "$file" ]] || { echo "Missing required input: $file" >&2; exit 2; }
done
project="$root/src/Carbon.Tools/Carbon.StagingHookVerifier.CollectiblePickupTests/CollectiblePickupTests.csproj"
[[ -f "$project" ]] || { echo "Missing collectible regression project: $project" >&2; exit 2; }
logs="$(python3 - "$output" <<'PY'
import pathlib,sys,tempfile
destination=pathlib.Path(sys.argv[1]) if sys.argv[1] else None
if destination is not None:
    assert destination.parent.is_dir() and not destination.exists() and not destination.is_symlink(), 'Verification must use a new JSON path in an existing directory'
print(tempfile.mkdtemp(prefix='collectible-verification-',dir=destination.parent if destination else '/tmp'))
PY
)"
if [[ -z "$output" ]]; then trap 'rm -rf -- "$logs"' EXIT; fi
dotnet run --project "$project" -c Release -- "$managed" "$carbon" "$hooks/Carbon.Hooks.Oxide.dll" 2>&1 | tee "$logs/publicized.log"
dotnet run --no-build --project "$project" -c Release -- "$raw_managed" "$carbon" "$hooks/Carbon.Hooks.Oxide.dll" 2>&1 | tee "$logs/raw.log"
if [[ -n "$output" ]]; then
  python3 - "$root" "$output" "$logs" "$managed" "$raw_managed" "$hooks/Carbon.Hooks.Oxide.dll" <<'PY'
import hashlib,json,pathlib,re,sys
root,destination,logs,publicized,raw,candidate=map(pathlib.Path,sys.argv[1:])
def sha(path):
    digest=hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda:stream.read(1024*1024),b''): digest.update(chunk)
    return digest.hexdigest()
evidence=[]
for name in ('publicized','raw'):
    path=(logs/(name+'.log')).resolve()
    match=re.search(r'Collectible pickup checks passed=(\d+);',path.read_text())
    assert match and int(match.group(1)) >= 12, 'Focused semantic/install checks are incomplete'
    evidence.append({'phase':name,'path':str(path),'sha256':sha(path),'checks':int(match.group(1))})
sources=['test-staging-collectible-hooks.sh',
    'src/Carbon.Tools/Carbon.StagingHookVerifier/CollectiblePickupSemantics.cs',
    'src/Carbon.Tools/Carbon.StagingHookVerifier/CollectiblePickupVerification.cs',
    'src/Carbon.Tools/Carbon.StagingHookVerifier.CollectiblePickupTests/Program.cs',
    'src/Carbon.Tools/Carbon.StagingHookVerifier.CollectiblePickupTests/CollectiblePickupTests.csproj']
value={'schemaVersion':1,'kind':'carbon-collectible-hook-verification','result':'PASS',
    'candidateSha256':sha(candidate),'nativeAssemblySha256':sha(raw/'Assembly-CSharp.dll'),
    'publicizedAssemblySha256':sha(publicized/'Assembly-CSharp.dll'),
    'sources':{str((root/path).resolve()):sha(root/path) for path in sources},'evidence':evidence}
with destination.open('x') as stream: json.dump(value,stream,indent=2); stream.write('\n')
print('Verification receipt: '+str(destination))
PY
fi
