#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd -- "$(dirname "$0")" >/dev/null 2>&1 && pwd -P)"
STAGING_ROOT="${CARBON_STAGING_ROOT:-/home/johnm/rust-staging-autoupdate/server}"
MANAGED_DIR="${CARBON_STAGING_MANAGED_PUBLICIZED:-${ROOT}/rust/linux/RustDedicated_Data/Managed}"
OPJ_SOURCE="${CARBON_HOOKGEN_OPJ_URL:-https://raw.githubusercontent.com/OxideMod/Oxide.Rust/staging/resources/Rust.opj}"
EXPECTED_OPJ_SHA256="${CARBON_HOOKGEN_OPJ_SHA256:-caecfc18b07e958ab63d18819139e47a981af489d37b45c2beaa2f07ede79d25}"
OVERLAY_PATH="${CARBON_HOOKGEN_OPJ_OVERLAY:-${ROOT}/staging-hookgen-overlays/staging.json}"
EXPECTED_SKIPS_PATH="${CARBON_HOOKGEN_EXPECTED_SKIPS:-${ROOT}/staging-hookgen-expected-skips.json}"
OUTPUT_ROOT="${CARBON_HOOKGEN_OUTPUT_ROOT:-${ROOT}/release/.tmp/staging-hookgen}"
VALIDATION_MODE="${CARBON_HOOKGEN_VALIDATION_MODE:-fail}"
CONFIGURATION="${CONFIGURATION:-Release}"

usage() {
  cat <<'USAGE'
Usage: ./build-staging-hooks.sh [options]

Generates Carbon.Hooks.Oxide C# sources locally for the installed staging Rust DLLs.

Options:
  --server-root <path>       Rust staging server root. Used for manifest metadata.
  --managed <path>           Publicized Rust managed DLL directory.
  --opj <path-or-url>        Rust.opj source. Defaults to Oxide.Rust staging branch.
  --opj-sha256 <sha256>      Required source checksum for the frozen OPJ bytes.
  --overlay <path>           OPJ overlay JSON. Defaults to staging-hookgen-overlays/staging.json.
  --expected-skips <path>    Exact expected generator skip list.
  --output-root <path>       Output directory. Defaults to release/.tmp/staging-hookgen.
  --validation-mode <mode>   Generator validation mode. Defaults to fail.
  -c, --configuration <cfg>  Generator build configuration. Defaults to Release.
  -h, --help                 Show this help.
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --server-root)
      STAGING_ROOT="${2:?Missing value for $1}"
      shift 2
      ;;
    --managed)
      MANAGED_DIR="${2:?Missing value for $1}"
      shift 2
      ;;
    --opj)
      OPJ_SOURCE="${2:?Missing value for $1}"
      shift 2
      ;;
    --opj-sha256)
      EXPECTED_OPJ_SHA256="${2:?Missing value for $1}"
      shift 2
      ;;
    --overlay)
      OVERLAY_PATH="${2:?Missing value for $1}"
      shift 2
      ;;
    --expected-skips)
      EXPECTED_SKIPS_PATH="${2:?Missing value for $1}"
      shift 2
      ;;
    --output-root)
      OUTPUT_ROOT="${2:?Missing value for $1}"
      shift 2
      ;;
    --validation-mode)
      VALIDATION_MODE="${2:?Missing value for $1}"
      shift 2
      ;;
    -c|--configuration)
      CONFIGURATION="${2:?Missing value for $1}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

require_tool() {
  local tool="$1"

  if ! command -v "${tool}" >/dev/null 2>&1; then
    echo "Missing required tool: ${tool}" >&2
    exit 2
  fi
}

require_dir() {
  local path="$1"
  local label="$2"

  if [[ ! -d "${path}" ]]; then
    echo "Missing ${label}: ${path}" >&2
    exit 2
  fi
}

require_file() {
  local path="$1"
  local label="$2"

  if [[ ! -f "${path}" ]]; then
    echo "Missing ${label}: ${path}" >&2
    exit 2
  fi
}

require_tool curl
require_tool dotnet
require_tool jq
require_tool sha256sum
require_dir "${STAGING_ROOT}" "staging server root"
require_dir "${MANAGED_DIR}" "managed DLL directory"
require_file "${MANAGED_DIR}/Assembly-CSharp.dll" "Assembly-CSharp.dll"

if [[ -n "${OVERLAY_PATH}" ]]; then
  require_file "${OVERLAY_PATH}" "OPJ overlay"
fi
require_file "${EXPECTED_SKIPS_PATH}" "expected hook skip list"

SOURCE_OPJ="${OUTPUT_ROOT}/Rust.source.opj"
PATCHED_OPJ="${OUTPUT_ROOT}/Rust.patched.opj"
GENERATED_DIR="${OUTPUT_ROOT}/generated"
SUMMARY_PATH="${OUTPUT_ROOT}/generation-summary.json"
PATCH_REPORT="${OUTPUT_ROOT}/opj-overlay-report.json"
MANIFEST_PATH="${OUTPUT_ROOT}/manifest.json"
GENERATOR_PROJECT="${ROOT}/src/Carbon.Hooks/Carbon.Hooks.Generator/src/Carbon.Hooks.Generator.csproj"

mkdir -p "${OUTPUT_ROOT}"
rm -rf "${GENERATED_DIR}"
mkdir -p "${GENERATED_DIR}"

echo "Building staging hook generator"
echo "Staging root: ${STAGING_ROOT}"
echo "Managed DLLs: ${MANAGED_DIR}"
echo "OPJ source: ${OPJ_SOURCE}"
echo "Overlay: ${OVERLAY_PATH:-<none>}"
echo "Output root: ${OUTPUT_ROOT}"

if [[ "${OPJ_SOURCE}" =~ ^https?:// ]]; then
  curl -fsSL "${OPJ_SOURCE}" -o "${SOURCE_OPJ}"
else
  require_file "${OPJ_SOURCE}" "OPJ source"
  cp "${OPJ_SOURCE}" "${SOURCE_OPJ}"
fi

SOURCE_SHA="$(sha256sum "${SOURCE_OPJ}" | awk '{ print $1 }')"
if [[ ! "${EXPECTED_OPJ_SHA256}" =~ ^[0-9a-f]{64}$ ]]; then
  echo "Expected OPJ SHA-256 must be 64 lowercase hexadecimal characters: ${EXPECTED_OPJ_SHA256}" >&2
  exit 2
fi
if [[ "${SOURCE_SHA}" != "${EXPECTED_OPJ_SHA256}" ]]; then
  echo "Rust.opj checksum drifted; refusing mixed hook inputs." >&2
  echo "Expected: ${EXPECTED_OPJ_SHA256}" >&2
  echo "Actual:   ${SOURCE_SHA}" >&2
  exit 1
fi

if [[ -n "${OVERLAY_PATH}" ]]; then
  jq --argfile overlay "${OVERLAY_PATH}" '
    def path_parts($value): $value | split(".");
    def target_matches($patch): [ .Manifests[].Hooks[] | select(.Hook.Name == $patch.name) | .Hook ];
    def changes($hook; $patch):
      [ (($patch.set // {}) | to_entries[]) as $entry |
        { path: ($entry | .key), old: ($hook | getpath(path_parts(($entry | .key)))), new: ($entry | .value) } ];

    {
      patchCount: (($overlay.patches // []) | length),
      patches: [
        ($overlay.patches // [])[] as $patch |
        target_matches($patch) as $matches |
        {
          name: $patch.name,
          reason: ($patch.reason // ""),
          matchCount: ($matches | length),
          changes: (if ($matches | length) == 1 then changes($matches[0]; $patch) else [] end)
        }
      ]
    }
  ' "${SOURCE_OPJ}" > "${PATCH_REPORT}"

  if jq -e '.patches[] | select(.matchCount != 1)' "${PATCH_REPORT}" >/dev/null; then
    echo "OPJ overlay patches must match exactly one hook each:" >&2
    jq '.patches[] | select(.matchCount != 1)' "${PATCH_REPORT}" >&2
    exit 1
  fi

  if jq -e '.patches[].changes[] | select(.old == .new)' "${PATCH_REPORT}" >/dev/null; then
    echo "OPJ overlay contains stale no-op patches:" >&2
    jq '.patches[] | select(any(.changes[]; .old == .new))' "${PATCH_REPORT}" >&2
    exit 1
  fi

  jq --argfile overlay "${OVERLAY_PATH}" '
    def path_parts($value): $value | split(".");
    reduce ($overlay.patches // [])[] as $patch (.;
      (.Manifests[].Hooks[] | select(.Hook.Name == $patch.name) | .Hook) |=
        reduce (($patch.set // {}) | to_entries[]) as $entry (.;
          setpath(path_parts(($entry | .key)); ($entry | .value))
        )
    )
  ' "${SOURCE_OPJ}" > "${PATCHED_OPJ}"
else
  cp "${SOURCE_OPJ}" "${PATCHED_OPJ}"
  jq -n '{ patchCount: 0, patches: [] }' > "${PATCH_REPORT}"
fi

PATCHED_SHA="$(sha256sum "${PATCHED_OPJ}" | awk '{ print $1 }')"

dotnet build "${GENERATOR_PROJECT}" -c "${CONFIGURATION}" --verbosity minimal
dotnet run --no-build --project "${GENERATOR_PROJECT}" -c "${CONFIGURATION}" -- \
  --input "${PATCHED_OPJ}" \
  --managed "${MANAGED_DIR}" \
  --output "${GENERATED_DIR}" \
  --validation-mode "${VALIDATION_MODE}" \
  --summary-output "${SUMMARY_PATH}" \
  --deterministic

FAILED_COUNT="$(jq -er '.failedCount' "${SUMMARY_PATH}")"

if [[ "${FAILED_COUNT}" != "0" ]]; then
  echo "Hook generation failed for ${FAILED_COUNT} hook(s)." >&2
  jq '.failed' "${SUMMARY_PATH}" >&2
  exit 1
fi

if ! jq -e --argfile expected "${EXPECTED_SKIPS_PATH}" '
  ([.skipped[].Name] | sort) == ($expected.skipped | sort)
' "${SUMMARY_PATH}" >/dev/null; then
  echo "Generated hook skip set differs from the reviewed allow-list." >&2
  jq '{actual: ([.skipped[].Name] | sort)}' "${SUMMARY_PATH}" >&2
  jq '{expected: (.skipped | sort)}' "${EXPECTED_SKIPS_PATH}" >&2
  exit 1
fi

jq -n \
  --arg stagingRoot "${STAGING_ROOT}" \
  --arg managedDir "${MANAGED_DIR}" \
  --arg opjSource "${OPJ_SOURCE}" \
  --arg sourceOpj "${SOURCE_OPJ}" \
  --arg patchedOpj "${PATCHED_OPJ}" \
  --arg sourceSha256 "${SOURCE_SHA}" \
  --arg expectedSourceSha256 "${EXPECTED_OPJ_SHA256}" \
  --arg patchedSha256 "${PATCHED_SHA}" \
  --arg overlayPath "${OVERLAY_PATH}" \
  --arg expectedSkipsPath "${EXPECTED_SKIPS_PATH}" \
  --arg generatedSourceDir "${GENERATED_DIR}" \
  --arg summaryPath "${SUMMARY_PATH}" \
  --arg patchReport "${PATCH_REPORT}" \
  --argjson generatedCount "$(jq -er '.generatedCount' "${SUMMARY_PATH}")" \
  --argjson failedCount "${FAILED_COUNT}" \
  --argjson skippedCount "$(jq -er '.skippedCount // 0' "${SUMMARY_PATH}")" \
  '{
    stagingRoot: $stagingRoot,
    managedDir: $managedDir,
    opjSource: $opjSource,
    sourceOpj: $sourceOpj,
    patchedOpj: $patchedOpj,
    sourceSha256: $sourceSha256,
    expectedSourceSha256: $expectedSourceSha256,
    patchedSha256: $patchedSha256,
    overlayPath: $overlayPath,
    expectedSkipsPath: $expectedSkipsPath,
    generatedSourceDir: $generatedSourceDir,
    summaryPath: $summaryPath,
    patchReport: $patchReport,
    generatedCount: $generatedCount,
    failedCount: $failedCount,
    skippedCount: $skippedCount
  }' > "${MANIFEST_PATH}"

echo "Generated hook source: ${GENERATED_DIR}"
echo "Generation summary: ${SUMMARY_PATH}"
echo "OPJ patch report: ${PATCH_REPORT}"
