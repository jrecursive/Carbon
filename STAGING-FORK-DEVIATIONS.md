# Staging fork deviations

The maintained branch is `washed/staging`, based on
`upstream/rust_beta/staging`. The last qualified runtime baseline (September 19,
2026) used fork commit `450d46cedeb723b5c2a4cf9da5ce5d918bbd6681`, upstream
`ecb06d3de74ba11b5a44b7e5aa7af64fafe4419e`, and installed Rust staging build
`25400304` (`2633.288.1`). Later documentation/tooling commits do not certify a
new runtime binary. Each release's compatibility record is authoritative for its
actual inputs and artifacts; a branch name or displayed Carbon version is not.

Public ports begin with an explicitly pinned official production artifact or
production source commit. For each deviation below, record whether it is already
upstream, ported and tested, staging-only, or unnecessary with evidence. An
unreviewed deviation remains pending; do not merge the whole staging fork into
production or silently assume an official build includes a fork fix.

## Retained deviations

- Rust build `26270245` replaces the CRC-keyed FileStorage API with native
  entity/part slots. ImageDatabase uses `StoreByCrc` and the same CRC as part for
  its shared CommunityEntity images, while reads/removals name that exact slot.
  Its administrative oversized-file cleanup verifies the stored PNG type before
  removing a slot, preserving native recordings and sculptures. Admin chart and
  image sends use the current `CL_ReceiveFile(part, crc, length, bytes)` payload;
  chart CRCs use the native content-only checksum. These source adaptations do
  not migrate retained legacy database rows or persisted image identifiers.
- ImageDatabase startup preserves its recorded CommunityEntity image registry
  after the upgrade's offline CRC conversion. If that entity ID changes, or its
  CRC-keyed slots need restoration, a bounded pass reads only the recorded owner
  and re-stores referenced, CRC-verified PNGs for the current CommunityEntity.
  Keys, aliases and previous-owner slots remain intact; the identifier advances
  only after complete recovery. Missing payloads retain the original registry
  and block image mutations instead of clearing it or replacing it with defaults.
- Rust build `25999658` adds a second native hacked-state guard and shifts the
  Frankenstein table distance calculation. Exact overlays move `CanHackCrate`
  and `OnFrankensteinPetSleep [FrankensteinTable]` to admitted empty-stack points
  before native analytics/ownership/hacking or item-transfer/removal/destruction
  effects. Both retain their existing any-non-null veto contract and argument
  order. Native-veto semantic checks and mutation regressions guard admission,
  empty-stack placement and the untouched native side effects.
- The same build contracts `RelationshipManager.UpdateAcquaintancesFor` after
  removing an occlusion block. Its limited-networking patch retains the typed
  foreach peer and skips acquaintance/last-seen updates to the native enumerator
  continuation, preserving iterator disposal and pooled-list cleanup. The guard
  compares the complete native/transformed instruction and exception structure
  outside the exact three-instruction addition.
- `LivestockAnimal.AddFamiliarity` now takes only `ulong userId, float seconds`;
  the removed `FamiliarityReason` enum is not fabricated. The two familiarity
  hook metadata entries target that signature. A scoped generator policy emits
  admission as a null-only bool prefix and completion as an
  [`__runOriginal`-guarded postfix](https://harmony.pardeike.net/articles/patching-injections.html).
  This covers all six current native return paths and suppresses completion
  after a veto or native exception. Callbacks expose the available animal,
  user ID and seconds arguments; no maintained plugin consumes the removed
  reason argument. Static generated-method checks, actual native installation
  and an inert six-return Harmony fixture guard callback order and argument
  identity. Remove this policy when canonical generation preserves these
  contracts on the current native signature and the same checks pass.
- Rust build `25999658` replaces `NPCTalking.Server_ResponsePressed(RPCMessage)`
  with the typed `Server_HandleResponse(BasePlayer, int, int, uint, uint,
  NetworkableId)` called by the owner-only player RPC. Two exact overlays retain
  `OnNpcConversationRespond` after native conversation, visible-choice, bounds and
  response-null admission and before conditions/actions; every non-null result
  still cancels. `OnNpcConversationResponded` retains its dependency and follows
  `ForceSpeechNode`, while ended conversations return before it. Both callbacks
  retain NPC/player/conversation/response arguments. The authoritative verifier
  composes both actual generated transpilers and checks native owner admission,
  callback arguments, veto continuation and completed/ended ordering. Keep the
  existing 45 generator skips unchanged. Remove the overlays when reviewed
  upstream metadata carries these exact contracts and the same checks pass.
- `HookCallerInternal` owns reflected-overload argument buffers through the
  dispatch lifetime and returns them to the pool after the call. This generic
  ownership fix requires explicit review in each public port, independently of
  staging-specific IL bindings.
- `PlayerInventoryProxyPatch` adapts one confirmed legacy ChaosNPC coroutine
  instruction pair on installed staging build `25830079` (`2635.289.1`). Raw
  ChaosNPC SHA-256 `f11d3b88887a9b93dc272e56f8b7207b5a6e917e0e7ccb09a6dafae888e9f270`
  and the previous converted copy both call `BasePlayer.get_inventory()` at
  `IL_03e1`, followed by `UnityEngine.Object.op_Implicit(Object)` at `IL_03e6`.
  Current `PlayerInventory` is an `EntityComponentProxy<BasePlayer>`, with no
  Unity object inheritance. Type-load success does not verify this method body.
  The Oxide and Harmony converters replace only a direct typed inventory
  getter/field followed by that exact Unity truthiness call, keeping the same
  instruction and branch/exception targets. The stable Carbon.Common helper
  `Carbon.Compatibility.LegacyPlayerInventory.Exists(PlayerInventory)` checks
  managed inventory presence and its real owner's Unity lifetime; it adds no
  destruction policy. Genuine Unity operators and raw vendor DLLs are unchanged.
  The patch skips a Unity inventory layout and rejects an unreviewed nonproxy
  layout. `util/carbon-compat-check` in the platform repository checks the real
  raw and converted vendor IL, the one replacement, its typed target, and safe
  null/ownerless calls. Remove this deviation when the official vendor or Carbon
  conversion handles the proxy correctly and the same exact-input checks pass.
  Public ports must inspect their actual inventory inheritance and vendor IL
  before deciding whether to carry it.
- The September 19 hook refresh includes report state-machine, RCON admission,
  explosive, construction and limited-networking corrections. The corresponding
  verifier checks ordering and cancellation semantics against the exact Rust
  input. These bindings are staging-specific until a public reference proves
  their applicability; successful installation alone is insufficient.

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
- The verifier owns its hook compatibility expectations locally. No staging
  hook compatibility manifest or hook IL shim is compiled into the Carbon runtime.
- `OnMarketplaceTerminalPurchase` uses semantic IL anchors instead of fixed
  instruction/local indexes. It dispatches after the RPC vending/order inputs
  are validated and before delivery eligibility, power, fees, or transaction
  side effects; any non-null hook result cancels the vanilla purchase. The
  verifier checks this ordering and the hook's exact argument locals.
- The verifier checks that `CorePlugin.IOnLoseCondition` copies a plugin-mutated
  by-ref amount back before applying condition loss, and that generated
  `OnPlayerDisconnected` remains inside Rust's non-null player branch.
- A generator policy is retained for `CanBeTargeted [FlameTurret]`: the main
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
  broad staging `HookPolicies` set is dropped except for the documented
  FlameTurret and two familiarity generator policies above. The exhaustive offline install test is
  the evidence gate for any future exception.
- Publicizer resolver changes, script compilation resolver changes, hookgen
  helper heuristics, generic/byref hook policies, and build bootstrap changes
  that canonical Carbon now implements are not carried.
- Broad runtime staging hook suppression and compatibility shims are not carried;
  the scoped inventory vendor conversion above is an explicitly reviewed exception.
- August-only Recycler, Research, Newtonsoft, Vault, timer, and Carbon core
  disconnect guards are not carried because canonical staging now owns the
  corresponding compatibility behavior.

## Review rule

The October 17 Native merge retains protocol 5 and the official per-thread
recording architecture together with bounded opt-in diagnostics. Its library
crate uses the Rust identifier `carbon_native` to compile without the inherited
crate-name warning. Build/profiler packaging renames the resulting artifacts to
the established runtime filenames `libCarbonNative.so` and `CarbonNative.dll`;
exported C symbols and loader names remain unchanged. Remove this packaging
deviation when upstream adopts the same warning-free crate naming.

Before adding a deviation, first compare current canonical staging and test the
failure against the installed Rust staging assemblies. Prefer an OPJ metadata
correction or a WashedPlatform compatibility API over a Carbon core change. Any
remaining core change must be listed here with its evidence and removal
condition.
