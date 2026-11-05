# Washed native source publication — October 8, 2026

The operator requested committing and pushing all local platform/Carbon changes.
The maintained build input is this Carbon checkout on `washed/staging`, published
to `git@github.com:jrecursive/Carbon.git`. The Native submodule contained older
uncommitted profiler diagnostics work, independently of the metrics library:
bounded exception fingerprints and GC-pause diagnostics, disabled by default.
This publication preserves that existing work; it does not enable diagnostics,
build new artifacts, or deploy/restart a runtime.

## Source locations and decision

Before publication:

- Carbon parent: `cb7fd8dbe3648b82b3f28432e3ddf977f462d43f`, branch `washed/staging`.
- Native path: `src/Carbon.Native`, detached at upstream `7d35ecc`.
- Native URL: `https://github.com/CarbonCommunity/Carbon.Native.git`.
- No writable `jrecursive/Carbon.Native` repository was accessible with the
  operator's existing GitHub SSH identity.

The operator delegated choosing the build repository and requested explicit
notes for later correction. Native is therefore published as its own source
tree/history on **`washed/native-diagnostics` in the existing `jrecursive/Carbon`
fork**. This is a separate branch, not Native files copied into the framework
tree. It is deliberately a temporary, documented repository arrangement rather
than a claim that a dedicated Native fork exists.

The Native commit is `57bd48f09a96c18253905b2d67abe74cfc6aabac`.
The parent `.gitmodules` URL is `https://github.com/jrecursive/Carbon.git` with
`branch = washed/native-diagnostics`, and its gitlink pins that exact Native SHA.
Exact submodule updates use the pinned SHA; `--remote` uses the declared branch,
not the Carbon framework's default branch. The Native tree has no nested
`.gitmodules`, so this arrangement does not recursively clone itself.

Local Native remotes retain the original repository as `upstream`. `origin` and
`washed` fetch the owned Carbon fork; SSH push URLs use the existing identity.
The parent's `origin` and branch remain unchanged. The Depot submodule is unchanged.
The parent's managed config retains the existing `Diagnostics = false` default.

## Verification and recovery

The publication verifies the GitHub tips against local commits and keeps both
working trees clean. A disposable checkout of the published parent/submodule
should resolve the new Native SHA and find its `Cargo.toml`; see the platform's
publication receipt/notes for the observed result. Source/diff checks during
publication are not a new native build or runtime qualification. Use the existing
platform/profiler build entrypoints before deploying changed Carbon artifacts.

If a dedicated Native fork is created later, move the existing commit without
rewriting its history:

```bash
git -C src/Carbon.Native remote add dedicated git@github.com:jrecursive/Carbon.Native.git
git -C src/Carbon.Native push dedicated HEAD:refs/heads/washed/staging
git config -f .gitmodules submodule.src/Carbon.Native.url https://github.com/jrecursive/Carbon.Native.git
git config -f .gitmodules submodule.src/Carbon.Native.branch washed/staging
git submodule sync -- src/Carbon.Native
git add .gitmodules src/Carbon.Native
git commit -m 'Move Washed native source to its dedicated fork'
git push origin washed/staging
```

Keep the same Native SHA while moving repositories. Do not restore the official
upstream URL while retaining this unpublished-to-upstream gitlink: a fresh clone
would be unable to fetch it. To restore the old source arrangement, revert the
parent publication commit and update the submodule to its restored pinned SHA.
The owned `washed/native-diagnostics` branch preserves the Native work either way.

The root platform `DEV_NOTES.md` records the final parent/Native publication SHAs,
GitHub destinations and checkout verification. Existing qualified deployment
records remain immutable; publication alone does not change remote staging bytes.
