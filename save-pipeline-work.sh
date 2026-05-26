#!/usr/bin/env bash

set -euo pipefail

ROOT="$(cd -- "$(dirname "$0")" >/dev/null 2>&1 && pwd -P)"
source "${ROOT}/carbon-git-common.sh"

MESSAGE="${*:-Save Carbon staging pipeline work}"

carbon_require_tool git
carbon_cd_repo

carbon_log "Saving current work to the fork branch."
carbon_print_context

carbon_require_work_branch
carbon_commit_if_dirty "${MESSAGE}"
carbon_push_work_branch

carbon_log "Save complete."
