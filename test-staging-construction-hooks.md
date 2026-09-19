# Construction veto semantic regressions

Run `./test-staging-construction-hooks.sh --generated-dir <generated-hook-directory>`
after local hook generation. Defaults and overrides match the RCON semantic test:
`--managed` selects publicized Rust references; `--raw-managed` selects the installed
raw references. The matching environment variables are `CARBON_HOOKGEN_GENERATED`,
`CARBON_STAGING_MANAGED_PUBLICIZED` and `CARBON_STAGING_MANAGED_PATH`.
Requires Python 3, .NET 10 and the verifier's pinned Harmony package. This test
does not start Rust, contact a remote server, or alter runtime files.

The test extracts the actual generated OnConstructionPlace transpiler and its
generated local-variable helper, applies it to both native assemblies, checks the
production semantic guard, and Harmony install-tests the exact rewrite. Six
negative mutations cover wrong entity/player, reversed veto behavior, early
return without cleanup, cleanup of a different entity and omitted destruction.

An executable copy of the generated veto instruction block substitutes instrumented
entity doubles for native engine methods. Sixteen cases exercise null, boxed false,
true and a string result, crossed with valid/unspawned and decay/non-decay entities.
Counters verify exact callback arguments, one callback, exactly one appropriate
cleanup path, no native-helper continuation on veto, and one untouched continuation
on null. Every non-null result still vetoes, including boxed false. Native cleanup
remains KillMessage for an already-valid entity, or optional DecayEntity cleanup
plus TerminateOnServer and EntityDestroy for an unspawned entity; veto returns null.

The current Rust refactor moved skin/grade/health initialization and spawning from
Planner.DoPlacement into SpawnConstruction. The hook now runs in DoPlacement's
validated non-null entity branch immediately before that helper. It retains the
exact entity, Construction component, boxed Construction.Target and owner-player
arguments, and veto still precedes OnPlaced/ownership/spawn and placement payment.
It now also precedes the helper's skin/grade/health setup; plugins must not depend
on those initialization values already being applied. No maintained first-party
plugin currently subscribes to this hook. This scoped compatibility limitation is
deliberate; the platform does not duplicate native initialization or add per-thread
context solely to reproduce the old inlined setup timing.

The normal `verify-staging-hooks.sh` invokes the same production semantic guard on
the compiled generated hook. Its checks are required even in requested-only mode,
so successfully installing a numerically valid but semantically misplaced hook is
not sufficient to qualify a staging upgrade.
