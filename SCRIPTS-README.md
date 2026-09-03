# Carbon Fork Workflow Scripts

These scripts keep this checkout owned by `git@github.com:jrecursive/Carbon.git` while still tracking official Carbon staging from `https://github.com/CarbonCommunity/Carbon.git`.

The normal branch for fork work is:

```text
jrecursive/rust_beta-staging-minimal-2633
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
- creates or switches to `jrecursive/rust_beta-staging-minimal-2633`
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
- builds ReleaseUnix Carbon artifacts and official generated hooks
- installs the built managed files into the staging server Carbon directory

Useful environment variables:

```bash
CARBON_STAGING_ROOT=/home/johnm/rust-staging-autoupdate/server
CARBON_STAGING_MANAGED_PATH=/home/johnm/rust-staging-autoupdate/server/RustDedicated_Data/Managed
CARBON_STAGING_CARBON_MANAGED_PATH=/home/johnm/rust-staging-autoupdate/server/carbon/managed
CARBON_BUILD_CONFIGURATION=ReleaseUnix
CARBON_RELEASES_ENDPOINT=https://api.carbonmod.gg/releases
```

Defaults are already set for the current local staging server layout.

## Install Built Carbon.dll On The Dedicated Server

Run this after making a narrow Carbon runtime change and building `src/Carbon/bin/ReleaseUnix/Carbon.dll`:

```bash
./install-server-carbon-dll.sh
```

What it does:

- compares the built `Carbon.dll` with the dedicated server's installed copy
- exits cleanly if the installed DLL is already current
- backs up the current installed DLL with a timestamped `.bak-codex-*` suffix
- copies the built DLL into the server's `carbon/managed` directory
- verifies the installed hash after copying

Useful environment variables:

```bash
CARBON_BUILT_DLL=/home/johnm/git/rust-platform/Carbon/src/Carbon/bin/ReleaseUnix/Carbon.dll
CARBON_BUILD_CONFIGURATION=ReleaseUnix
CARBON_STAGING_ROOT=/home/johnm/rust-staging-autoupdate/server
CARBON_STAGING_CARBON_MANAGED_PATH=/home/johnm/rust-staging-autoupdate/server/carbon/managed
```

The server must be restarted to load the replacement DLL if it is already running.

## Sync After Official Carbon Staging Updates

Run this when official Carbon staging has caught up and you want your fork branch rebuilt on top of the latest official staging branch:

```bash
./sync-official-staging.sh
```

What it does:

- refuses to run if the working tree has uncommitted changes
- fetches `origin` and `upstream`
- creates a timestamped backup branch
- resets `jrecursive/rust_beta-staging-minimal-2633` to `upstream/rust_beta/staging`
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

When the reset scripts rebuild the fork branch, they restore the exact
fork-owned workflow, hook generator, overlay/skip manifest, verifier, and
documented runtime deviations listed in `CARBON_DURABLE_PATHS` inside
`carbon-git-common.sh`. Keep that list synchronized with the reviewed diff from
`upstream/rust_beta/staging`; do not add unchanged upstream files because doing
so would resurrect stale implementations on a later sync.
