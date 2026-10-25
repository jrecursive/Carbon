#!/usr/bin/env bash
set -euo pipefail
umask 077

root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
server="${CARBON_STAGING_ROOT:-/home/johnm/rust-staging-autoupdate/server}"
input=''
output=''
apply=false
usage() {
  echo 'Usage: ./repair-collectible-hook.sh --input EXACT_HOOK_DLL [--server-root PATH] [--output NEW_DIRECTORY] [--dry-run|--apply]'
}
while (($#)); do
  case "$1" in
    --input|--server-root|--output)
      (($# >= 2)) || { usage >&2; exit 2; }
      case "$1" in --input) input="$2" ;; --server-root) server="$2" ;; --output) output="$2" ;; esac
      shift 2 ;;
    --apply) apply=true; shift ;;
    --dry-run) apply=false; shift ;;
    --help|-h) usage; exit 0 ;;
    *) usage >&2; exit 2 ;;
  esac
done
for tool in mcs mono python3 realpath; do command -v "$tool" >/dev/null || { echo "Missing tool: $tool" >&2; exit 2; }; done
[[ -n "$input" ]] || { usage >&2; exit 2; }
input="$(realpath -- "$input")"
native="$server/RustDedicated_Data/Managed/Assembly-CSharp.dll"
cecil="$server/carbon/managed/lib/Mono.Cecil.dll"
source="$root/src/Carbon.Tools/Carbon.CollectibleHookRepair/CollectibleHookRepair.cs"
overlay="$root/staging-hookgen-overlays/staging.json"
for path in "$input" "$native" "$cecil" "$source" "$overlay"; do
  [[ -f "$path" && ! -L "$path" ]] || { echo "Missing or redirected input: $path" >&2; exit 2; }
done
python3 - "$input" "$native" "$overlay" <<'PY'
import hashlib,json,pathlib,sys
source,native,overlay=map(pathlib.Path,sys.argv[1:])
assert hashlib.sha256(source.read_bytes()).hexdigest()=='78f7071f45775165b83f71e3825d5cdc71db82bcbfe1d880a30c2017601d7245', 'Exact original hook hash changed'
assert hashlib.sha256(native.read_bytes()).hexdigest()=='30b6d0925bc40a08b0b59c1c3c85559390435792e00838d9e183061994330f82', 'Exact native assembly hash changed'
patches=[p for p in json.loads(overlay.read_text())['patches'] if p['name']=='OnCollectiblePickedup']
assert len(patches)==1 and patches[0]['set']=={'ArgumentString':'this, a0, l8','InjectionIndex':128}, 'Source overlay differs from reviewed correction'
PY
if [[ "$apply" == false ]]; then
  printf 'Dry-run: repair exact original %s; native input and source overlay verified. No assembly or runtime changes.\n' "$input"
  exit 0
fi
[[ -n "$output" ]] || { echo '--output is required with --apply' >&2; exit 2; }
output="$(realpath -m -- "$output")"
[[ ! -e "$output" ]] || { echo 'Output must be a new directory' >&2; exit 2; }
mkdir -p -- "$(dirname -- "$output")"
mkdir -- "$output"
mkdir -p -- "$output/original/hooks" "$output/candidate/hooks" "$output/tools"
cp -- "$input" "$output/original/hooks/Carbon.Hooks.Oxide.dll"
cp -- "$cecil" "$output/tools/Mono.Cecil.dll"
mcs -langversion:latest -warnaserror -r:"$output/tools/Mono.Cecil.dll" \
  -out:"$output/tools/CollectibleHookRepair.exe" "$source"
mono "$output/tools/CollectibleHookRepair.exe" "$output/original/hooks/Carbon.Hooks.Oxide.dll" \
  "$native" "$server/carbon/managed" "$output/candidate/hooks/Carbon.Hooks.Oxide.dll" > "$output/structural-comparison.json"
python3 - "$root" "$output" "$native" "$cecil" <<'PY'
import hashlib,json,pathlib,sys
root,output,native,cecil=map(pathlib.Path,sys.argv[1:])
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
structural=json.loads((output/'structural-comparison.json').read_text())
assert structural['result']=='PASS' and structural['structuralComparison']=='PASS' and structural['deterministic']
paths=['repair-collectible-hook.sh','src/Carbon.Tools/Carbon.CollectibleHookRepair/CollectibleHookRepair.cs']
receipt={
 'schemaVersion':1,'kind':'carbon-collectible-hook-repair','result':'PASS',
 'scope':'Exact-input collectible hook binary repair; not a full platform release qualification.',
 'sourceHook':'original/hooks/Carbon.Hooks.Oxide.dll','candidate':'candidate/hooks/Carbon.Hooks.Oxide.dll',
 'sourceHookSha256':sha(output/'original/hooks/Carbon.Hooks.Oxide.dll'),
 'nativeAssemblySha256':sha(native),'candidateSha256':sha(output/'candidate/hooks/Carbon.Hooks.Oxide.dll'),
 'overlaySha256':sha(root/'staging-hookgen-overlays/staging.json'),
 'sources':{path:sha(root/path) for path in paths},'cecilSha256':sha(cecil),
 'structuralComparison':'PASS','structuralProof':'structural-comparison.json',
 'structuralProofSha256':sha(output/'structural-comparison.json'),
 'changes':{'hook':'OnCollectiblePickedup','identifier':'06dec48365445b6ad8319c8505f6e9a1',
            'injectionIndex':{'before':146,'after':128},'itemLocal':{'before':9,'after':8},
            'parameterMetadata':{'before':['local9','ItemModConsume'],'after':['local8','Item']}},
 'deterministic':True,'methodsChecked':structural['methodsChecked'],'methodsChanged':1,
 'semanticVerification':'Required separately through verify-staging-hooks.sh before deployment.'
}
(output/'qualification.json').write_text(json.dumps(receipt,indent=2)+'\n')
for path in (output/'original').rglob('*'):
    if path.is_file(): path.chmod(0o400)
for path in (output/'candidate').rglob('*'):
    if path.is_file(): path.chmod(0o400)
for name in ('qualification.json','structural-comparison.json'): (output/name).chmod(0o400)
print(json.dumps({'result':'PASS','qualification':str(output/'qualification.json'),'candidateSha256':receipt['candidateSha256']}))
PY
