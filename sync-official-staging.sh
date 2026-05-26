#!/usr/bin/env bash

set -euo pipefail

ROOT="$(cd -- "$(dirname "$0")" >/dev/null 2>&1 && pwd -P)"
source "${ROOT}/carbon-git-common.sh"

carbon_require_tool git
carbon_cd_repo

carbon_log "Syncing the fork branch to the latest official Carbon staging branch."
carbon_print_context
carbon_require_work_branch
carbon_require_clean_worktree
carbon_fetch_remotes

BACKUP="$(carbon_create_backup_branch "before-official-staging-sync")"
carbon_reset_work_branch_to_official "${BACKUP}" "Reapply fork staging pipeline after official staging sync"

carbon_log "Sync complete. Backup branch: ${BACKUP}"
