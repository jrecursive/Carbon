# Limited-networking FX regression checks

Run `./test-staging-limit-networking.sh --generated-dir PATH` against a candidate
hook generator output. Defaults and `--managed` / `--raw-managed` options match
the RCON semantic regression wrapper. Requires .NET 10, Python 3 and the existing
Harmony dependency. No Rust process or game method is executed.

The test extracts all six actual generated base/companion transpilers for
LimitNetworkingNoEffect patches 1–3 and composes them with raw and publicized
installed IL. Eleven checks per assembly validate the production semantic guard,
seven unsafe mutations, and real CLR installation of the headshot base/patch.
Teardown removes the dependent patch before its base to avoid temporarily applying
base-relative offsets to an unprefixed method.

Patch 1 retains native impact FX for null/unlimited initiators. Patch 2 preserves
projectile accounting and skips only clientside projectile visuals for a limited
player. Patch 3 uses the actual HitInfo.InitiatorPlayer local; null/unlimited
initiators retain the native headshot effect, while limited initiators skip only
Effect.server.Run. Damage, flinch signals, statistics, spectator RPCs and all native
instructions outside the insertion must remain unchanged.

The staging overlay anchors patch 3 before the headshot prefab argument block,
accounting for Carbon's actual six-instruction IOnBasePlayerAttacked prefix.
Future Rust or generator changes must satisfy the semantic guard, not merely
produce IL that installs. Do not suppress this patch or substitute a method return.
