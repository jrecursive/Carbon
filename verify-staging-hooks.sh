#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd -- "$(dirname "$0")" >/dev/null 2>&1 && pwd -P)"
STAGING_ROOT="${CARBON_STAGING_ROOT:-/home/johnm/rust-staging-autoupdate/server}"
CARBON_MANAGED=""
HOOKS_DIR=""
CONFIGURATION="${CONFIGURATION:-Release}"
ALL_GENERATED=1
STRICT_NO_SUPPRESSION=1
ALLOW_SUPPRESSION_FILE=""
VERIFY_COMPAT_SHIMS=0
INSTALL_COMPAT_HOOKS=1
INSTALL_ALL_DYNAMIC=0
INSTALL_HOOKS_FROM_LOG=""
INSTALL_HOOKS=()

usage() {
  cat <<'USAGE'
Usage: ./verify-staging-hooks.sh [options]

Verifies Carbon generated hook DLLs against the installed staging Rust managed DLLs
without starting the Rust dedicated server.

Options:
  --server-root <path>      Rust staging server root.
  --carbon-managed <path>   Carbon managed directory to verify. Defaults to
                            <server-root>/carbon/managed.
  --hooks-dir <path>        Hook DLL directory to verify. Defaults to
                            <carbon-managed>/hooks.
  --all-generated           Verify every generated hook in the hook DLLs. This is the default.
  --focused-compat          Verify only the legacy staging compatibility shim surface.
  --strict-no-suppression   Fail if generated hooks are suppressed. This is the default.
  --allow-suppression-file  JSON allow-list for explicit generated hook suppression.
  --verify-compat-shims     Also verify legacy staging compatibility shims.
  --install-compat-hooks    Install-test generated staging compatibility hooks. This is the default.
  --no-install-compat-hooks Skip staging compatibility hook install tests.
  --install-hook <hook>     Install-test one generated hook by full name, hook name, or log token.
  --install-hooks-from-log  Parse hook request/patch failures from a Carbon/current_errors log.
  --install-all-dynamic     Install-test every generated dynamic hook in child processes.
  -c, --configuration <cfg> Verifier build configuration. Defaults to Release.
  -h, --help                Show this help.
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --server-root)
      STAGING_ROOT="${2:?Missing value for $1}"
      shift 2
      ;;
    --carbon-managed)
      CARBON_MANAGED="${2:?Missing value for $1}"
      shift 2
      ;;
    --hooks-dir)
      HOOKS_DIR="${2:?Missing value for $1}"
      shift 2
      ;;
    --all-generated)
      ALL_GENERATED=1
      shift
      ;;
    --focused-compat)
      ALL_GENERATED=0
      STRICT_NO_SUPPRESSION=0
      VERIFY_COMPAT_SHIMS=1
      shift
      ;;
    --strict-no-suppression)
      STRICT_NO_SUPPRESSION=1
      shift
      ;;
    --allow-suppression-file)
      ALLOW_SUPPRESSION_FILE="${2:?Missing value for $1}"
      STRICT_NO_SUPPRESSION=0
      shift 2
      ;;
    --verify-compat-shims)
      VERIFY_COMPAT_SHIMS=1
      shift
      ;;
    --install-compat-hooks)
      INSTALL_COMPAT_HOOKS=1
      shift
      ;;
    --no-install-compat-hooks)
      INSTALL_COMPAT_HOOKS=0
      shift
      ;;
    --install-hook)
      INSTALL_HOOKS+=("${2:?Missing value for $1}")
      shift 2
      ;;
    --install-hooks-from-log)
      INSTALL_HOOKS_FROM_LOG="${2:?Missing value for $1}"
      shift 2
      ;;
    --install-all-dynamic)
      INSTALL_ALL_DYNAMIC=1
      shift
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

CARBON_MANAGED="${CARBON_MANAGED:-${STAGING_ROOT}/carbon/managed}"
HOOKS_DIR="${HOOKS_DIR:-${CARBON_MANAGED}/hooks}"
PROJECT="${ROOT}/src/Carbon.Tools/Carbon.StagingHookVerifier/Carbon.StagingHookVerifier.csproj"

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

if ! command -v dotnet >/dev/null 2>&1; then
  echo "Missing required tool: dotnet" >&2
  exit 2
fi

require_dir "${STAGING_ROOT}" "server root"
require_dir "${STAGING_ROOT}/RustDedicated_Data/Managed" "Rust managed directory"
require_file "${STAGING_ROOT}/RustDedicated_Data/Managed/Assembly-CSharp.dll" "Assembly-CSharp.dll"
require_dir "${CARBON_MANAGED}" "Carbon managed directory"
require_file "${CARBON_MANAGED}/Carbon.dll" "Carbon.dll"
require_file "${CARBON_MANAGED}/Carbon.SDK.dll" "Carbon.SDK.dll"
require_dir "${HOOKS_DIR}" "hooks directory"
require_file "${PROJECT}" "staging hook verifier project"

echo "Verifying staging hooks"
echo "Server root: ${STAGING_ROOT}"
echo "Carbon managed: ${CARBON_MANAGED}"
echo "Hooks dir: ${HOOKS_DIR}"

dotnet build "${PROJECT}" -c "${CONFIGURATION}" --verbosity minimal
RUN_ARGS=(
  --server-root "${STAGING_ROOT}"
  --carbon-managed "${CARBON_MANAGED}"
  --hooks-dir "${HOOKS_DIR}"
)

if [[ "${ALL_GENERATED}" == "1" ]]; then
  RUN_ARGS+=(--all-generated)
fi

if [[ "${STRICT_NO_SUPPRESSION}" == "1" ]]; then
  RUN_ARGS+=(--strict-no-suppression)
fi

if [[ -n "${ALLOW_SUPPRESSION_FILE}" ]]; then
  RUN_ARGS+=(--allow-suppression-file "${ALLOW_SUPPRESSION_FILE}")
fi

if [[ "${VERIFY_COMPAT_SHIMS}" == "1" ]]; then
  RUN_ARGS+=(--verify-compat-shims)
fi

if [[ "${INSTALL_COMPAT_HOOKS}" == "1" ]]; then
  RUN_ARGS+=(--install-compat-hooks)
else
  RUN_ARGS+=(--no-install-compat-hooks)
fi

if [[ "${INSTALL_ALL_DYNAMIC}" == "1" ]]; then
  RUN_ARGS+=(--install-all-dynamic)
fi

if [[ -n "${INSTALL_HOOKS_FROM_LOG}" ]]; then
  RUN_ARGS+=(--install-hooks-from-log "${INSTALL_HOOKS_FROM_LOG}")
fi

for hook in "${INSTALL_HOOKS[@]}"; do
  RUN_ARGS+=(--install-hook "${hook}")
done

dotnet run --no-build --project "${PROJECT}" -c "${CONFIGURATION}" -- "${RUN_ARGS[@]}"
