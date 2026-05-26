#!/usr/bin/env bash

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
  echo "carbon-git-common.sh is a helper and should be sourced, not executed." >&2
  exit 1
fi

CARBON_REPO_ROOT="$(cd -- "$(dirname "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd -P)"
CARBON_FORK_REMOTE="${CARBON_FORK_REMOTE:-origin}"
CARBON_FORK_REMOTE_URL="${CARBON_FORK_REMOTE_URL:-git@github.com:jrecursive/Carbon.git}"
CARBON_UPSTREAM_REMOTE="${CARBON_UPSTREAM_REMOTE:-upstream}"
CARBON_UPSTREAM_REMOTE_URL="${CARBON_UPSTREAM_REMOTE_URL:-https://github.com/CarbonCommunity/Carbon.git}"
CARBON_OFFICIAL_BRANCH="${CARBON_OFFICIAL_BRANCH:-rust_beta/staging}"
CARBON_WORK_BRANCH="${CARBON_WORK_BRANCH:-jrecursive/rust_beta-staging-pipeline}"

CARBON_DURABLE_PATHS=(
  "carbon-git-common.sh"
  "fork-init.sh"
  "save-pipeline-work.sh"
  "sync-official-staging.sh"
  "drop-temp-patches.sh"
  "SCRIPTS-README.md"
  "upgrade-staging.sh"
  "tools/build/linux/bootstrap.sh"
  "tools/build/linux/update.sh"
  "tools/build/runners/update.cs"
  "tools/build/win/bootstrap.bat"
  "tools/build/win/update.bat"
)

carbon_log() {
  printf '[carbon] %s\n' "$*"
}

carbon_die() {
  printf '[carbon] ERROR: %s\n' "$*" >&2
  exit 1
}

carbon_require_tool() {
  local tool="$1"

  if ! command -v "${tool}" >/dev/null 2>&1; then
    carbon_die "Missing required tool: ${tool}"
  fi
}

carbon_cd_repo() {
  cd "${CARBON_REPO_ROOT}"
  if [[ "$(git rev-parse --show-toplevel 2>/dev/null)" != "${CARBON_REPO_ROOT}" ]]; then
    carbon_die "Not running from the expected Carbon repository: ${CARBON_REPO_ROOT}"
  fi
}

carbon_remote_url() {
  local remote="$1"

  git remote get-url "${remote}" 2>/dev/null || true
}

carbon_remote_exists() {
  local remote="$1"

  git remote get-url "${remote}" >/dev/null 2>&1
}

carbon_current_branch() {
  git branch --show-current
}

carbon_has_changes() {
  [[ -n "$(git status --porcelain)" ]]
}

carbon_require_clean_worktree() {
  if carbon_has_changes; then
    git status --short
    carbon_die "Working tree is dirty. Run ./save-pipeline-work.sh first, or commit/stash manually."
  fi
}

carbon_require_work_branch() {
  if [[ "$(carbon_current_branch)" != "${CARBON_WORK_BRANCH}" ]]; then
    carbon_die "Expected branch ${CARBON_WORK_BRANCH}. Run ./fork-init.sh first."
  fi
}

carbon_print_context() {
  carbon_log "Repo: ${CARBON_REPO_ROOT}"
  carbon_log "Branch: $(carbon_current_branch)"
  carbon_log "${CARBON_FORK_REMOTE}: $(carbon_remote_url "${CARBON_FORK_REMOTE}")"
  carbon_log "${CARBON_UPSTREAM_REMOTE}: $(carbon_remote_url "${CARBON_UPSTREAM_REMOTE}")"
}

carbon_backup_branch_name() {
  local reason="$1"
  local timestamp

  timestamp="$(date +%Y%m%d-%H%M%S)"
  printf 'backup/%s-%s\n' "${reason}" "${timestamp}"
}

carbon_create_backup_branch() {
  local reason="$1"
  local backup

  backup="$(carbon_backup_branch_name "${reason}")"
  git branch "${backup}" HEAD
  printf '[carbon] Created backup branch: %s\n' "${backup}" >&2
  printf '%s\n' "${backup}"
}

carbon_fetch_remotes() {
  carbon_log "Fetching ${CARBON_FORK_REMOTE}..."
  git fetch "${CARBON_FORK_REMOTE}" --prune
  carbon_log "Fetching ${CARBON_UPSTREAM_REMOTE}..."
  git fetch "${CARBON_UPSTREAM_REMOTE}" --prune
}

carbon_ensure_remotes() {
  local origin_url

  origin_url="$(carbon_remote_url "${CARBON_FORK_REMOTE}")"

  if [[ "${CARBON_FORK_REMOTE}" == "origin" && "${origin_url}" =~ github.com[:/]CarbonCommunity/Carbon(.git)?$ ]]; then
    if carbon_remote_exists "${CARBON_UPSTREAM_REMOTE}"; then
      carbon_log "Existing upstream remote found; setting it to ${CARBON_UPSTREAM_REMOTE_URL}."
      git remote set-url "${CARBON_UPSTREAM_REMOTE}" "${CARBON_UPSTREAM_REMOTE_URL}"
      git remote set-url "${CARBON_FORK_REMOTE}" "${CARBON_FORK_REMOTE_URL}"
    else
      carbon_log "Renaming current origin to upstream."
      git remote rename "${CARBON_FORK_REMOTE}" "${CARBON_UPSTREAM_REMOTE}"
      git remote add "${CARBON_FORK_REMOTE}" "${CARBON_FORK_REMOTE_URL}"
    fi
  else
    if carbon_remote_exists "${CARBON_FORK_REMOTE}"; then
      git remote set-url "${CARBON_FORK_REMOTE}" "${CARBON_FORK_REMOTE_URL}"
    else
      git remote add "${CARBON_FORK_REMOTE}" "${CARBON_FORK_REMOTE_URL}"
    fi

    if carbon_remote_exists "${CARBON_UPSTREAM_REMOTE}"; then
      git remote set-url "${CARBON_UPSTREAM_REMOTE}" "${CARBON_UPSTREAM_REMOTE_URL}"
    else
      git remote add "${CARBON_UPSTREAM_REMOTE}" "${CARBON_UPSTREAM_REMOTE_URL}"
    fi
  fi
}

carbon_switch_or_create_work_branch() {
  if git show-ref --verify --quiet "refs/heads/${CARBON_WORK_BRANCH}"; then
    carbon_log "Switching to existing local branch ${CARBON_WORK_BRANCH}."
    git switch "${CARBON_WORK_BRANCH}"
  else
    carbon_log "Creating local branch ${CARBON_WORK_BRANCH} from current HEAD."
    git switch -c "${CARBON_WORK_BRANCH}"
  fi
}

carbon_commit_if_dirty() {
  local message="$1"

  if ! carbon_has_changes; then
    carbon_log "No local changes to commit."
    return 0
  fi

  carbon_log "Committing current local changes."
  git add -A
  git commit -m "${message}"
}

carbon_push_work_branch() {
  carbon_log "Pushing ${CARBON_WORK_BRANCH} to ${CARBON_FORK_REMOTE}."
  git push -u "${CARBON_FORK_REMOTE}" "HEAD:refs/heads/${CARBON_WORK_BRANCH}"
}

carbon_reset_work_branch_to_official() {
  local backup="$1"
  local message="$2"

  carbon_log "Resetting ${CARBON_WORK_BRANCH} to ${CARBON_UPSTREAM_REMOTE}/${CARBON_OFFICIAL_BRANCH}."
  git switch -C "${CARBON_WORK_BRANCH}" "${CARBON_UPSTREAM_REMOTE}/${CARBON_OFFICIAL_BRANCH}"

  carbon_log "Restoring durable pipeline files from ${backup}."
  for path in "${CARBON_DURABLE_PATHS[@]}"; do
    if git cat-file -e "${backup}:${path}" 2>/dev/null; then
      git checkout "${backup}" -- "${path}"
    fi
  done

  if carbon_has_changes; then
    git add -A
    git commit -m "${message}"
  else
    carbon_log "No durable pipeline changes needed after reset."
  fi

  carbon_push_work_branch
}
