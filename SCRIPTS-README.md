# Carbon Fork Workflow Scripts

These scripts keep this checkout owned by `git@github.com:jrecursive/Carbon.git` while still tracking official Carbon staging from `https://github.com/CarbonCommunity/Carbon.git`.

The normal branch for fork work is:

```text
jrecursive/rust_beta-staging-pipeline
```

The official branch used as the reset point is:

```text
upstream/rust_beta/staging
```

## First-Time Setup

Run this once from the repository root:

```bash
./fork-init.sh
```

Use this when the checkout still points at `CarbonCommunity/Carbon`, or when you want to repair the remotes and branch tracking.

What it does:

- changes `origin` to `git@github.com:jrecursive/Carbon.git`
- keeps the official repo as `upstream`
- creates or switches to `jrecursive/rust_beta-staging-pipeline`
- commits any current local changes
- pushes the branch to the fork

## Save Current Work

Run this after editing scripts, build logic, or temporary staging patches:

```bash
./save-pipeline-work.sh
```

To use a custom commit message:

```bash
./save-pipeline-work.sh "Update staging build pipeline"
```

What it does:

- stages all current changes
- commits them if anything changed
- pushes the current fork branch
- exits cleanly if there is nothing to save

## Build And Install Custom Staging Carbon

Run this when the dedicated server staging build is ahead of official Carbon staging and you need a local Carbon build for platform development:

```bash
./upgrade-staging.sh
```

What it does:

- reads the installed Rust staging managed DLLs
- updates and publicizes Rust references
- builds DebugUnix Carbon artifacts
- installs the built managed files into the staging server Carbon directory

Useful environment variables:

```bash
CARBON_STAGING_ROOT=/home/johnm/rust-staging-autoupdate/server
CARBON_STAGING_MANAGED_PATH=/home/johnm/rust-staging-autoupdate/server/RustDedicated_Data/Managed
CARBON_STAGING_CARBON_MANAGED_PATH=/home/johnm/rust-staging-autoupdate/server/carbon/managed
CARBON_RELEASES_ENDPOINT=https://api.carbonmod.gg/releases
```

Defaults are already set for the current local staging server layout.

## Sync After Official Carbon Staging Updates

Run this when official Carbon staging has caught up and you want your fork branch rebuilt on top of the latest official staging branch:

```bash
./sync-official-staging.sh
```

What it does:

- refuses to run if the working tree has uncommitted changes
- fetches `origin` and `upstream`
- creates a timestamped backup branch
- resets `jrecursive/rust_beta-staging-pipeline` to `upstream/rust_beta/staging`
- restores the durable fork pipeline files
- pushes the refreshed branch to the fork

## Drop Temporary Patches

Run this when temporary compatibility patches are no longer needed and you want to keep only the durable fork pipeline files:

```bash
./drop-temp-patches.sh
```

What it does:

- refuses to run if the working tree has uncommitted changes
- creates a timestamped backup branch
- resets the fork branch to official staging
- restores only the durable pipeline files
- pushes the cleaned branch to the fork

## Safety Rules

- Reset-style scripts require a clean working tree.
- Reset-style scripts create a backup branch before changing the fork branch.
- The scripts do not change README, workflow, release, self-update, or submodule URLs that point at `CarbonCommunity`.
- If you are unsure whether your current work is saved, run:

```bash
./save-pipeline-work.sh
```

## Durable Pipeline Files

When the reset scripts rebuild the fork branch, these files are restored from the backup branch:

```text
carbon-git-common.sh
fork-init.sh
save-pipeline-work.sh
sync-official-staging.sh
drop-temp-patches.sh
SCRIPTS-README.md
upgrade-staging.sh
tools/build/linux/bootstrap.sh
tools/build/linux/update.sh
tools/build/runners/update.cs
tools/build/win/bootstrap.bat
tools/build/win/update.bat
```
