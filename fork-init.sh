#!/usr/bin/env bash

set -euo pipefail

ROOT="$(cd -- "$(dirname "$0")" >/dev/null 2>&1 && pwd -P)"
source "${ROOT}/carbon-git-common.sh"

carbon_require_tool git
carbon_cd_repo

carbon_log "Converting this checkout to use the fork as the writable origin."
carbon_print_context

carbon_ensure_remotes
carbon_fetch_remotes
carbon_switch_or_create_work_branch
carbon_commit_if_dirty "Add fork-owned staging pipeline workflow"
carbon_push_work_branch

carbon_log "Fork setup complete."
carbon_print_context
