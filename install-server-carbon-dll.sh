#!/usr/bin/env bash

set -euo pipefail

ROOT="$(cd -- "$(dirname "$0")" >/dev/null 2>&1 && pwd -P)"
STAGING_ROOT="${CARBON_STAGING_ROOT:-/home/johnm/rust-staging-autoupdate/server}"
STAGING_CARBON_MANAGED="${CARBON_STAGING_CARBON_MANAGED_PATH:-${STAGING_ROOT}/carbon/managed}"
CARBON_BUILD_CONFIGURATION="${CARBON_BUILD_CONFIGURATION:-ReleaseUnix}"
BUILT_CARBON_DLL="${CARBON_BUILT_DLL:-${ROOT}/src/Carbon/bin/${CARBON_BUILD_CONFIGURATION}/Carbon.dll}"
INSTALLED_CARBON_DLL="${STAGING_CARBON_MANAGED}/Carbon.dll"

require_file() {
  local path="$1"
  local label="$2"

  if [[ ! -f "${path}" ]]; then
    echo "Missing ${label}: ${path}" >&2
    exit 1
  fi
}

require_dir() {
  local path="$1"
  local label="$2"

  if [[ ! -d "${path}" ]]; then
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

file_hash() {
  sha256sum "$1" | awk '{ print $1 }'
}

echo "Repo: ${ROOT}"
echo "Build configuration: ${CARBON_BUILD_CONFIGURATION}"
echo "Built Carbon.dll: ${BUILT_CARBON_DLL}"
echo "Server Carbon managed: ${STAGING_CARBON_MANAGED}"

require_tool sha256sum
require_tool awk
require_dir "${STAGING_CARBON_MANAGED}" "server Carbon managed directory"
require_file "${BUILT_CARBON_DLL}" "built Carbon.dll"

if [[ -f "${INSTALLED_CARBON_DLL}" ]]; then
  BUILT_HASH="$(file_hash "${BUILT_CARBON_DLL}")"
  INSTALLED_HASH="$(file_hash "${INSTALLED_CARBON_DLL}")"

  if [[ "${BUILT_HASH}" == "${INSTALLED_HASH}" ]]; then
    echo "Server Carbon.dll is already up to date (${BUILT_HASH})."
    exit 0
  fi

  BACKUP="${INSTALLED_CARBON_DLL}.bak-codex-$(date +%Y%m%d-%H%M%S)"
  echo "Backing up installed Carbon.dll to ${BACKUP}"
  cp -p "${INSTALLED_CARBON_DLL}" "${BACKUP}"
else
  BUILT_HASH="$(file_hash "${BUILT_CARBON_DLL}")"
  echo "No installed Carbon.dll found; installing a new copy."
fi

echo "Installing Carbon.dll..."
cp "${BUILT_CARBON_DLL}" "${INSTALLED_CARBON_DLL}"

INSTALLED_HASH="$(file_hash "${INSTALLED_CARBON_DLL}")"
if [[ "${BUILT_HASH}" != "${INSTALLED_HASH}" ]]; then
  echo "Installed Carbon.dll hash mismatch: got ${INSTALLED_HASH}, expected ${BUILT_HASH}" >&2
  exit 1
fi

echo "Installed Carbon.dll into ${INSTALLED_CARBON_DLL}"
echo "Installed hash: ${INSTALLED_HASH}"
