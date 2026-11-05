#!/usr/bin/env bash
set -euo pipefail
# Frozen template, copied byte-identical to Carbon/tools/build/linux/washed-public-build.sh.
ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../../.." && pwd -P)"
REFERENCE="${WASHED_PUBLIC_REFERENCE_ROOT:?Frozen public reference required}"
HOOK_INPUTS="${WASHED_PUBLIC_HOOK_INPUTS:?Frozen public hook inputs required}"
EVIDENCE="${WASHED_PUBLIC_BUILD_EVIDENCE:?Source build evidence directory required}"
export PATH="${WASHED_LIVE_ACCOUNT_ROOT:-/srv/rust}/.cargo/bin:$PATH"
export CARGO_HOME="${WASHED_LIVE_ACCOUNT_ROOT:-/srv/rust}/.cargo"
export RUSTUP_HOME="${WASHED_LIVE_ACCOUNT_ROOT:-/srv/rust}/.rustup"
export RUSTUP_TOOLCHAIN=1.98.1
for tool in cargo rustc dotnet jq python3 mono mcs nm readelf sha256sum rg awk cp mkdir; do
  command -v "$tool" >/dev/null || { echo "Missing public source prerequisite: $tool" >&2; exit 2; }
done
for file in "$ROOT/src/Carbon.slnx" "$ROOT/build-staging-hooks.sh" "$ROOT/verify-staging-hooks.sh" \
  "$ROOT/public-hookgen-overlays.json" "$ROOT/public-hookgen-expected-skips.json" \
  "$REFERENCE/RustDedicated_Data/Managed/Assembly-CSharp.dll" \
  "$REFERENCE/RustDedicated_Data/MonoBleedingEdge/x86_64/libmonobdwgc-2.0.so" \
  "$HOOK_INPUTS/Rust.opj"; do
  [[ -f "$file" && ! -L "$file" ]] || { echo "Missing regular public build input: $file" >&2; exit 2; }
done
OPERATION="$(cd -- "$EVIDENCE/../.." && pwd -P)"
PLATFORM="$(jq -er '.platform.worktree' "$OPERATION/worktrees.json")"
[[ -x "$PLATFORM/util/profiler-repair/build-profiler.sh" ]] || { echo 'Missing paired native qualifier' >&2; exit 2; }
python3 - "$ROOT" "$REFERENCE" "$HOOK_INPUTS" "$EVIDENCE" "$OPERATION" <<'INPUTS'
import hashlib,json,sys
from pathlib import Path
carbon,reference,hooks,evidence,operation=map(Path,sys.argv[1:])
def files(root):
    result={}
    for path in sorted(root.rglob('*')):
        if path.is_symlink(): raise SystemExit('Redirected public input refused')
        if path.is_file(): result[str(path.relative_to(root))]=hashlib.sha256(path.read_bytes()).hexdigest()
    return result
worktrees=json.loads((operation/'worktrees.json').read_text())
receipt=json.loads((operation/'public-reference.json').read_text())
hook_receipt=json.loads((operation/'public-hook-inputs.json').read_text())
assert carbon.resolve()==carbon and Path(worktrees['carbon']['worktree'])==carbon
assert reference.resolve()==reference and Path(receipt['snapshotRoot'])==reference
assert hooks==operation/'public-hook-inputs' and files(hooks)==hook_receipt['files']
assert hook_receipt['channel']=='public' and hook_receipt['sourceUrl']=='https://raw.githubusercontent.com/OxideMod/Oxide.Rust/develop/resources/Rust.opj'
raw={name:value for name,value in files(reference/'RustDedicated_Data/Managed').items() if name.endswith('.dll')}
(evidence/'consumed-inputs.json').write_text(json.dumps({'rawManagedFiles':raw,'hookInputs':files(hooks)},indent=2,sort_keys=True)+'\n')
INPUTS
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$OPERATION/source-tool-cache/dotnet-home}"
export NUGET_PACKAGES="${NUGET_PACKAGES:-$OPERATION/source-tool-cache/nuget-packages}"
export NUGET_HTTP_CACHE_PATH="${NUGET_HTTP_CACHE_PATH:-$OPERATION/source-tool-cache/nuget-http}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 NuGetAudit=false TreatWarningsAsErrors=true
mkdir -p "$DOTNET_CLI_HOME" "$NUGET_PACKAGES" "$NUGET_HTTP_CACHE_PATH"
MANAGED="$ROOT/rust/linux/RustDedicated_Data/Managed"
python3 - "$REFERENCE/RustDedicated_Data/Managed" "$MANAGED" <<'COPY'
from pathlib import Path
import hashlib,shutil,struct,sys
source,target=map(Path,sys.argv[1:])
assert target.resolve()==target and target.parent.parent.parent.name=='rust'
if target.exists(): shutil.rmtree(target)
target.mkdir(parents=True)
for path in source.glob('*.dll'):
    assert path.is_file() and not path.is_symlink()
    shutil.copy2(path,target/path.name)
# Match Carbon.Runner.Files.Hash before publicization: first MD5 UInt32, little endian.
(target/'.hash').write_text(str(struct.unpack('<I',hashlib.md5((source/'Assembly-CSharp.dll').read_bytes()).digest()[:4])[0]))
COPY
dotnet build "$ROOT/src/Carbon.Tools/Carbon.Publicizer" -c Release -p:TreatWarningsAsErrors=true --verbosity minimal
dotnet run --no-build --project "$ROOT/src/Carbon.Tools/Carbon.Publicizer" -c Release -- \
  "$MANAGED" "$REFERENCE/carbon/managed" >"$EVIDENCE/publicizer.log" 2>&1
if rg -q 'Failed to patch' "$EVIDENCE/publicizer.log"; then
  echo 'Publicizer reported a failure; inspect retained evidence' >&2; exit 1;
fi
OPJ_SHA="$(sha256sum "$HOOK_INPUTS/Rust.opj" | awk '{print $1}')"
"$ROOT/build-staging-hooks.sh" --server-root "$REFERENCE" --managed "$MANAGED" \
  --opj "$HOOK_INPUTS/Rust.opj" --opj-sha256 "$OPJ_SHA" \
  --overlay "$ROOT/public-hookgen-overlays.json" --expected-skips "$ROOT/public-hookgen-expected-skips.json" \
  --output-root "$EVIDENCE/hookgen"
dotnet restore "$ROOT/src/Carbon.slnx" --property:Configuration=ReleaseUnix \
  --property:TreatWarningsAsErrors=true --verbosity minimal
VERSION="$(jq -er '.carbon.version' "$OPERATION/public-reference.json")" \
  CARBON_GENERATED_HOOK_SOURCE_DIR="$EVIDENCE/hookgen/generated" \
  "$ROOT/tools/build/linux/build.sh" ReleaseUnix HOOKGEN production_build -noarchive -clean
OUTPUT="$ROOT/release/.tmp/ReleaseUnix/carbon/managed"
"$ROOT/verify-staging-hooks.sh" --server-root "$REFERENCE" --carbon-managed "$OUTPUT" \
  --hooks-dir "$OUTPUT/hooks" --strict-no-suppression --all-generated --install-all-dynamic \
  --max-parallel-install-checks 8 \
  >"$EVIDENCE/dynamic-hooks.log" 2>&1
RUSTFLAGS="${RUSTFLAGS:+$RUSTFLAGS }-D warnings" \
  "$PLATFORM/util/profiler-repair/build-profiler.sh" --native-only --server-root "$REFERENCE" \
  --carbon-managed "$OUTPUT" --output-dir "$EVIDENCE/native-profiler"
mkdir -p "$ROOT/release/.tmp/ReleaseUnix/carbon/native"
cp "$EVIDENCE/native-profiler/libCarbonNative.so" "$ROOT/release/.tmp/ReleaseUnix/carbon/native/libCarbonNative.so"
python3 - "$REFERENCE" "$HOOK_INPUTS" "$EVIDENCE" "$ROOT" <<'PROOF'
import hashlib,json,sys
from pathlib import Path
reference,hooks,evidence,carbon=map(Path,sys.argv[1:])
def files(root): return {str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(root.rglob('*')) if p.is_file()}
consumed=json.loads((evidence/'consumed-inputs.json').read_text())
assert consumed['rawManagedFiles']=={k:v for k,v in files(reference/'RustDedicated_Data/Managed').items() if k.endswith('.dll')}
assert consumed['hookInputs']==files(hooks)
proof=json.loads((evidence/'native-profiler/build-receipt.json').read_text())
assert proof['nativeProtocol']==5 and proof['managedProtocol']==127
assert proof['nativeTests']==proof['managedChecks']=='PASS'
for relative,key in [('managed/Carbon.Profiler.dll','Carbon.Profiler.dll'),('native/libCarbonNative.so','libCarbonNative.so')]:
    assert hashlib.sha256((carbon/'release/.tmp/ReleaseUnix/carbon'/relative).read_bytes()).hexdigest()==proof['artifactHashes'][key]
(evidence/'public-build-result.json').write_text(json.dumps({'result':'PASS','nativeProfilerProof':'native-profiler/build-receipt.json',
    'hookGeneratorManifest':'hookgen/manifest.json','dynamicHookProof':'dynamic-hooks.log','nativeProtocol':5,'managedProtocol':127},indent=2)+'\n')
PROOF
echo 'Public source Carbon and its exact paired native profiler qualified; no runtime modified.'
