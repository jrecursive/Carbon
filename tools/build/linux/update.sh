#!/usr/bin/env bash

set -euo pipefail

BASE="$(cd -- "$(dirname "$0")" >/dev/null 2>&1 && pwd -P)"
ROOT="$(realpath "${BASE}/../../../")"

infer_target() {
  local branch
  branch="$(git -C "${ROOT}" rev-parse --abbrev-ref HEAD 2>/dev/null || true)"

  case "${branch}" in
    rust_beta/staging) echo "staging" ;;
    rust_beta/aux01) echo "aux01-staging" ;;
    rust_beta/aux02) echo "aux02-staging" ;;
    rust_beta/aux03) echo "aux03-staging" ;;
    *) echo "release" ;;
  esac
}

TARGET="${1:-}"
if [[ -z "${TARGET}" ]]; then
  TARGET="$(infer_target)"
fi

"${BASE}/_runner.sh" tools/build/runners/update.cs "${TARGET}"
