#!/usr/bin/env bash

set -euo pipefail

ROOT="$(cd -- "$(dirname "$0")" >/dev/null 2>&1 && pwd -P)"
STAGING_ROOT="${CARBON_STAGING_ROOT:-/home/johnm/rust-staging-autoupdate/server}"
STAGING_MANAGED="${CARBON_STAGING_MANAGED_PATH:-${STAGING_ROOT}/RustDedicated_Data/Managed}"
STAGING_CARBON_MANAGED="${CARBON_STAGING_CARBON_MANAGED_PATH:-${STAGING_ROOT}/carbon/managed}"
CARBON_BUILD_CONFIGURATION="${CARBON_BUILD_CONFIGURATION:-ReleaseUnix}"
BUILD_MANAGED="${ROOT}/release/.tmp/${CARBON_BUILD_CONFIGURATION}/carbon/managed"
HOOKGEN_OUTPUT_ROOT="${CARBON_HOOKGEN_OUTPUT_ROOT:-${ROOT}/release/.tmp/${CARBON_BUILD_CONFIGURATION}/staging-hookgen}"
GENERATED_HOOK_SOURCE_DIR="${HOOKGEN_OUTPUT_ROOT}/generated"
CARBON_RELEASES_ENDPOINT="${CARBON_RELEASES_ENDPOINT:-https://api.carbonmod.gg/releases}"
CURRENT_ERRORS_LOG="${CARBON_CURRENT_ERRORS_LOG:-${ROOT}/../current_errors.log}"
CARBON_RELEASE_TAG="rustbeta_staging_build"

require_dir() {
  local path="$1"
  local label="$2"

  if [[ ! -d "${path}" ]]; then
    echo "Missing ${label}: ${path}" >&2
    exit 1
  fi
}

require_file() {
  local path="$1"
  local label="$2"

  if [[ ! -f "${path}" ]]; then
    echo "Missing ${label}: ${path}" >&2
    exit 1
  fi
}

require_tool() {
  local tool="$1"

  if ! command -v "${tool}" >/dev/null 2>&1; then
    echo "Missing required tool: ${tool}" >&2
    exit 1
  fi
}

get_assembly_version() {
  local assembly="$1"

  monodis --assembly "${assembly}" | awk '/^Version:/ { print $2; exit }'
}

normalize_assembly_version() {
  local version="$1"

  printf '%s\n' "${version}" | sed -E 's/\.0$//'
}

echo "Repo: ${ROOT}"
echo "Staging Rust managed: ${STAGING_MANAGED}"
echo "Staging Carbon managed: ${STAGING_CARBON_MANAGED}"
echo "Carbon releases endpoint: ${CARBON_RELEASES_ENDPOINT}"
echo "Current errors log: ${CURRENT_ERRORS_LOG}"

require_tool curl
require_tool git
require_tool jq
require_tool monodis
require_dir "${STAGING_MANAGED}" "staging Rust managed directory"
require_file "${STAGING_MANAGED}/Assembly-CSharp.dll" "staging Assembly-CSharp.dll"
require_file "${STAGING_MANAGED}/Facepunch.Console.dll" "staging Facepunch.Console.dll"

cd "${ROOT}"

echo "Resolving official Carbon ${CARBON_RELEASE_TAG} version..."
OFFICIAL_STAGING_VERSION="$(curl -fsSL "${CARBON_RELEASES_ENDPOINT}" | jq -er --arg tag "${CARBON_RELEASE_TAG}" '.[] | select(.name == $tag) | .version')"

if [[ -z "${OFFICIAL_STAGING_VERSION}" ]]; then
  echo "Failed to resolve official Carbon ${CARBON_RELEASE_TAG} version." >&2
  exit 1
fi

echo "Official Carbon ${CARBON_RELEASE_TAG} version: ${OFFICIAL_STAGING_VERSION}"

echo "Pulling latest repo changes..."
git pull --ff-only

echo "Syncing and publicizing installed staging Rust DLLs..."
CARBON_STAGING_MANAGED_PATH="${STAGING_MANAGED}" tools/build/linux/update.sh staging

echo "Generating local Oxide hook sources against installed staging Rust DLLs..."
"${ROOT}/build-staging-hooks.sh" \
  --server-root "${STAGING_ROOT}" \
  --managed "${ROOT}/rust/linux/RustDedicated_Data/Managed" \
  --output-root "${HOOKGEN_OUTPUT_ROOT}"

require_dir "${GENERATED_HOOK_SOURCE_DIR}" "generated hook source directory"

echo "Building ${CARBON_BUILD_CONFIGURATION} Carbon artifacts with locally generated hooks as ${CARBON_RELEASE_TAG} ${OFFICIAL_STAGING_VERSION}..."
CARBON_GENERATED_HOOK_SOURCE_DIR="${GENERATED_HOOK_SOURCE_DIR}" \
  VERSION="${OFFICIAL_STAGING_VERSION}" \
  tools/build/linux/build.sh "${CARBON_BUILD_CONFIGURATION}" "RUST_STAGING;HOOKGEN" "${CARBON_RELEASE_TAG}" -noarchive

require_dir "${BUILD_MANAGED}" "${CARBON_BUILD_CONFIGURATION} build managed directory"
require_file "${BUILD_MANAGED}/Carbon.dll" "built Carbon.dll"
require_file "${BUILD_MANAGED}/Carbon.Preloader.dll" "built Carbon.Preloader.dll"
require_file "${BUILD_MANAGED}/hooks/Carbon.Hooks.Base.dll" "built Carbon.Hooks.Base.dll"
require_file "${BUILD_MANAGED}/hooks/Carbon.Hooks.Community.dll" "built Carbon.Hooks.Community.dll"
require_file "${BUILD_MANAGED}/hooks/Carbon.Hooks.Oxide.dll" "built Carbon.Hooks.Oxide.dll"

BUILT_PRELOADER_VERSION="$(get_assembly_version "${BUILD_MANAGED}/Carbon.Preloader.dll")"
BUILT_STAGING_VERSION="$(normalize_assembly_version "${BUILT_PRELOADER_VERSION}")"

if [[ "${BUILT_STAGING_VERSION}" != "${OFFICIAL_STAGING_VERSION}" ]]; then
  echo "Built Carbon.Preloader.dll version mismatch: got ${BUILT_PRELOADER_VERSION}, expected ${OFFICIAL_STAGING_VERSION}.x" >&2
  exit 1
fi

echo "Verified built Carbon.Preloader.dll version: ${BUILT_PRELOADER_VERSION}"

echo "Verifying generated hooks against installed staging Rust DLLs..."
VERIFY_ARGS=(
  "${ROOT}/verify-staging-hooks.sh"
  --server-root "${STAGING_ROOT}" \
  --carbon-managed "${BUILD_MANAGED}" \
  --hooks-dir "${BUILD_MANAGED}/hooks" \
  --all-generated \
  --strict-no-suppression
)

if [[ -f "${CURRENT_ERRORS_LOG}" ]]; then
  VERIFY_ARGS+=(--install-hooks-from-log "${CURRENT_ERRORS_LOG}")
fi

"${VERIFY_ARGS[@]}"

echo "Installing ${CARBON_BUILD_CONFIGURATION} managed artifacts..."
mkdir -p "${STAGING_CARBON_MANAGED}"
cp -a "${BUILD_MANAGED}/." "${STAGING_CARBON_MANAGED}/"

echo "Installed Carbon ${CARBON_BUILD_CONFIGURATION} managed artifacts into ${STAGING_CARBON_MANAGED}"
echo "Self-update can remain enabled: official Carbon will replace this custom build when ${CARBON_RELEASE_TAG} changes from ${OFFICIAL_STAGING_VERSION}."
