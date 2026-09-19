# Spawn hook semantic regressions

Run `./test-staging-spawn-hooks.sh` from this Carbon checkout. Use
`--server-root PATH` or `CARBON_STAGING_ROOT` to select installed Rust references.
The script requires .NET 10 and the verifier's pinned Harmony package. All build
artifacts are temporary; no Rust process starts and no server files change.

The regression executable reads the actual installed Rust IL and inserts the
generated-hook dispatch shape at the reviewed staging offsets. The same
`SpawnHookSemantics` checker used by the production verifier must accept all three
correct mappings and reject nine malformed mappings: early throw dispatch, failed
creation dispatch, post-consumption dispatch, prediction ID passed as an entity,
drop before native spawn, success-branch labels bypassing the hook, wrong player,
build before the non-null guard, and a non-GameObject build argument.

`verify-staging-hooks.sh` separately invokes the real generated transpilers and
checks their rewritten instructions on every normal verification, including
`--requested-only`. It proves that explosive notifications receive the original
RPC player and exact spawned entity after native setup/cooldown but before item
consumption, while failed creation bypasses notification. It also traces the
DoThrowImpl out-entity assignment and native SpawnThrownEntity helper.

OnEntityBuilt retains Planner plus the placed GameObject as its public arguments.
Its notification must begin the successful non-null placement branch, after the
native spawn path and before OnConstructionBuilt and PayForPlacement. The new
construction helper does not change that existing event boundary.

These offline checks establish argument identity and control-flow placement; the
normal remote staging startup and user gameplay tests still qualify behavior with
the complete plugin stack.
