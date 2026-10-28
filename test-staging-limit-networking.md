# Limited-networking FX regression checks

Rust build `26270245` routes projectile RPCs through
`ProcessCLProject(RPCMessage, bool, BaseMountable)`. The generated notification,
FX guard and verifier fixture target that exact shared processing method; native
wrapper validation, mounted admission and projectile accounting remain intact.

Run `./test-staging-limit-networking.sh --generated-dir PATH` against a candidate
hook generator output. Defaults and `--managed` / `--raw-managed` options match
the RCON semantic regression wrapper. Requires .NET 10, Python 3 and the existing
Harmony dependency. No Rust process or game method is executed.

The test extracts all six actual generated base/companion transpilers for
LimitNetworkingNoEffect patches 1–3 and composes them with raw and publicized
installed IL. Fourteen checks per assembly validate the production semantic guard,
nine unsafe mutations, and real CLR installation of the headshot and projectile
base/patch pairs.
Teardown removes the dependent patch before its base to avoid temporarily applying
base-relative offsets to an unprefixed method.

Patch 1 retains native impact FX for null/unlimited initiators. Patch 2 preserves
projectile accounting and skips only clientside projectile visuals for a limited
player. Patch 3 uses the actual HitInfo.InitiatorPlayer local; null/unlimited
initiators retain the native headshot effect, while limited initiators skip only
Effect.server.Run. Damage, flinch signals, statistics, spectator RPCs and all native
instructions outside the insertion must remain unchanged.

For Rust build 25830079, patch 2 also retains the native
`HasFiredProjectile` check after projectile accounting. The semantic guard rejects
a removed or reversed native check and requires both native and limited-player
branches to skip the same effect block.

The staging overlay anchors patch 3 before the headshot prefab argument block,
accounting for Carbon's actual six-instruction IOnBasePlayerAttacked prefix.
Future Rust or generator changes must satisfy the semantic guard, not merely
produce IL that installs. Do not suppress this patch or substitute a method return.
