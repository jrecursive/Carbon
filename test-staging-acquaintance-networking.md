# Acquaintance limited-networking guard

For build `26341433`, Rust includes the peer limit check itself and upstream
removes the generated patch. The wrapper detects that inventory and runs the
shared native networking test project against raw and publicized assemblies.
Four checks per assembly validate the native peer identity, limit branch,
relation/timestamp bypass and loop/disposal/pool cleanup, and reject reversed
polarity, wrong peer and return shortcuts. The authoritative verifier uses the
same guard. Historical generated-patch inputs retain the checks below.

Run `./test-staging-acquaintance-networking.sh --generated-dir PATH` against the
candidate generator output. `--managed` and `--raw-managed` select the publicized
and original Rust managed trees; defaults match the other focused hook wrappers.
Requires .NET 10, Python 3, and the existing Harmony package. No Rust server or
native game method is executed.

The tests extract the actual generated `LimitNetworkingAcquaintances [patch]`
transpiler, apply it to each selected assembly, and run the same semantic guard
used by the authoritative hook verifier. Nine checks per assembly cover accepted
IL, seven unsafe mutations, and actual CLR installation. Mutations reverse the
limit condition, use the wrong peer, bypass cleanup, enter relation work, or
remove native timestamp/disposal/pool-release behavior.

For build 25999658 the guard inserts before `GetRelations` argument setup at
native index 35 and branches to the existing enumerator advance at native index
96. This skips both relation creation and `lastSeenTime` updates for a limited
peer. Unlimited peers retain every native instruction. Existing self/dead/sleep/
invisible admission, visibility and safe-zone behavior remain unchanged, and the
branch retains the native loop, enumerator finally, and pooled-list release.

This hook has no declared base dependency or sibling target in the reviewed OPJ.
The authoritative verifier rejects an unexpected dependency for explicit review;
it does not compose unrelated `SetRelationship` hooks into this target.
