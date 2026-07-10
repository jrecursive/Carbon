# Staging fork deviations

This branch is rebuilt on top of `upstream/rust_beta/staging`. The installed
local Rust staging server is the source of truth for managed assemblies. The
goal is to carry only deterministic build and hook metadata needed while
official Carbon catches up with Rust staging.

## Retained deviations

- Fork workflow wrappers (`upgrade-staging.sh`, `build-staging-hooks.sh`,
  `verify-staging-hooks.sh`, and their supporting scripts) provide a repeatable
  local staging build without changing Carbon runtime behavior.
- Hook generation records structured failed and skipped results. An explicit
  expected-skip manifest must exactly match generator output, so a newly skipped
  hook fails the build instead of disappearing silently. The current 45 entries
  include Carbon's 10 existing generator blacklists plus 35 installed-staging
  failures covering bonus-item, centralized-ban, corpse, counter, engine,
  fishing, IO-ref, map-marker, save-load, sleeping-bag, and vending hook groups.
  Current Washed Up consumers of corpse, fishing, map-marker, and save-load
  notifications use WashedPlatform bridges; the other excluded groups have no
  `new-plugins` subscriber.
- The staging OPJ overlay contains only exact, non-no-op corrections required by
  the installed staging IL. Every overlay patch must match exactly one hook and
  must change the original value.
- `Carbon.Hooks.Oxide.csproj` can compile generated source from
  `CARBON_GENERATED_HOOK_SOURCE_DIR`, keeping generated artifacts out of source
  control.
- The offline verifier can structurally validate every generated hook and
  install-test every dynamic hook in isolated child processes with generated
  hook suppression disabled.
- The verifier owns its compatibility expectations locally. No staging
  compatibility manifest or IL shim is compiled into the Carbon runtime.
- One generator policy is retained for `CanBeTargeted [FlameTurret]`: the main
  hook emits a stack-safe leave path and its obsolete cleanup dependency emits
  a no-op transpiler. Multiple current plugins subscribe to this canonical hook,
  and the official OPJ-generated IL fails installation on the installed staging
  server. The exhaustive verifier proves the replacement and every remaining
  generated hook install cleanly.
- `Core.Hooks.Commands.cs` initializes `_argumentBuffer` with `[]`. Canonical
  staging currently contains an invalid `with(ArgPool.DefaultCapacity)`
  collection expression at that line; this one-line compile fix should be
  dropped as soon as upstream corrects it.

## Deliberately dropped fork patches

- `Carbon.Extensions.RpcEx` is not carried. Washed Up callers use
  `WashedPlatform.ClientRpc` instead.
- The custom `c.adminpanel` command is not carried. Canonical Carbon's `cpanel`
  and `cp` commands remain authoritative.
- Custom `HookEx` and `PatchManager` runtime policies are not carried. The old
  broad staging `HookPolicies` set is dropped except for the single documented
  FlameTurret generator policy above. The exhaustive offline install test is
  the evidence gate for any future exception.
- Publicizer resolver changes, script compilation resolver changes, hookgen
  helper heuristics, generic/byref hook policies, and build bootstrap changes
  that canonical Carbon now implements are not carried.
- Runtime staging hook suppression and compatibility shims are not carried.

## Review rule

Before adding a deviation, first compare current canonical staging and test the
failure against the installed Rust staging assemblies. Prefer an OPJ metadata
correction or a WashedPlatform compatibility API over a Carbon core change. Any
remaining core change must be listed here with its evidence and removal
condition.
