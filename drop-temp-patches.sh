#!/usr/bin/env bash

set -euo pipefail

ROOT="$(cd -- "$(dirname "$0")" >/dev/null 2>&1 && pwd -P)"
source "${ROOT}/carbon-git-common.sh"

carbon_require_tool git
carbon_cd_repo

carbon_log "Dropping temporary patches and keeping only durable staging pipeline files."
carbon_print_context
carbon_require_work_branch
carbon_require_clean_worktree
carbon_fetch_remotes

BACKUP="$(carbon_create_backup_branch "before-dropping-temp-patches")"
carbon_reset_work_branch_to_official "${BACKUP}" "Keep fork staging pipeline after dropping temporary patches"

carbon_log "Temporary patch drop complete. Backup branch: ${BACKUP}"
