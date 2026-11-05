#!/usr/bin/env bash
set -euo pipefail
root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
server_root="${CARBON_STAGING_ROOT:-/home/johnm/rust-staging-autoupdate/server}"
if [[ "${1:-}" == --server-root && $# -eq 2 ]]; then server_root="$2"; shift 2; fi
[[ $# -eq 0 ]] || { echo 'Usage: ./test-staging-spawn-hooks.sh [--server-root PATH]' >&2; exit 2; }
command -v dotnet >/dev/null || { echo 'Missing required tool: dotnet' >&2; exit 1; }
managed="$server_root/RustDedicated_Data/Managed"
[[ -f "$managed/Assembly-CSharp.dll" ]] || { echo 'Missing installed Rust managed references' >&2; exit 1; }
project="$root/src/Carbon.Tools/Carbon.StagingHookVerifier.SemanticTests/SpawnHookSemanticTests.csproj"
[[ -f "$project" ]] || { echo 'Missing spawn semantic regression project' >&2; exit 1; }
scratch="$(mktemp -d /tmp/carbon-spawn-semantics.XXXXXX)"
trap 'rm -rf -- "$scratch"' EXIT
dotnet build "$project" --configuration Release --nologo --verbosity quiet \
  -p:NuGetAudit=false -p:TreatWarningsAsErrors=true \
  -p:BaseIntermediateOutputPath="$scratch/obj/" -p:OutputPath="$scratch/bin/"
dotnet "$scratch/bin/SpawnHookSemanticTests.dll" "$managed"
