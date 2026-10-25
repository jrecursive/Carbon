# Staging Hookgen OPJ Overlays

The October 24 collectible review found two stale `OnCollectiblePickedup`
fields against installed build `25999658`: the created `Item` is local 8 while
local 9 is `ItemModConsume`, and the old composed instruction 146 falls inside
the receiver-null drop branch. The overlay now uses `this, a0, l8` at instruction
128: native 119, after successful receiver analytics and before `GiveItem`, plus
the existing pickup-veto dependency's nine instructions. It preserves ordinary
player harvests, item-null/eat/null-receiver exits and every native output-loop
and kill effect. The required verifier composes both hooks and rejects the old
drop-only placement and wrong local; run `test-staging-collectible-hooks.sh`
against the exact candidate DLL for focused raw/publicized and CLR-install
regressions. The skip list remains unchanged.

Build `25999658` moves the unchanged report async bodies to
`OnFeedbackReport>d__795` / `OnPlayerReported>d__794`. A Cecil comparison against
the October 9 qualified native assemblies finds identical instructions, operands,
local types and indexed branch targets after normalizing only the target type's
sequence number. The three report overlays therefore change target names only.

Strict installation additionally found a newly added fully-hacked admission
gate in `RPC_Hack` and shifted Frankenstein table distance evaluation. Their
veto injections move to admitted empty-stack native instructions 7 and 41,
respectively, before any gameplay side effect. The acquaintance networking
branch now lands on native enumerator `MoveNext` at 96 after the removed
occlusion block, retaining its cleanup. The familiarity hook signatures lose
the removed reason enum; their generator policy supplies prefix admission and
native-execution-gated postfix completion across the inlined six-return body.
Dedicated native-veto, acquaintance and familiarity semantic wrappers cover
these repairs. None changes the expected skip list.

That build also replaces `NPCTalking.Server_ResponsePressed(RPCMessage)` with the
typed `Server_HandleResponse(BasePlayer, int, int, uint, uint, NetworkableId)`.
The two response overlays preserve the regular-response veto after native
conversation/visible-choice/bounds/null admission, with arguments `this, a0, l0,
l14`, at native instruction 189. The completed notification remains immediately
after `ForceSpeechNode`, at 248 including the actual nine-instruction veto
dependency. Native `ForceEndConversation` still returns before that notification.
The authoritative verifier and `test-staging-conversation-hooks.sh` compose both
generated transpilers and guard these contracts; the 45 expected skips stay
unchanged.

`build-staging-hooks.sh` fetches `Rust.opj` from the Oxide.Rust staging branch and applies the overlay in this directory before running `Carbon.Hooks.Generator`.

Use overlays only when the upstream staging OPJ is stale for the installed staging server DLLs. Each patch must target one exact OPJ hook `Name`, include a reason, and set only the fields needed to make generated hooks match the installed staging IL.

The build rejects overlay entries that match zero or multiple hooks and rejects
stale entries whose old and new values are equal. Remove a patch as soon as the
upstream OPJ carries the same correction.

Fetched OPJ files, patched OPJ files, generated C# source, summaries, and patch reports are written under `release/.tmp` and are not committed.

Build 25529090 renumbers the unchanged player report async bodies to
`OnFeedbackReport>d__777` / `OnPlayerReported>d__776` and Deep Sea open/close
coroutines to `OpenDeepSeaAsync>d__76` / `CloseDeepSeaAsync>d__78`. Their source
bodies match the hash-verified build 25400304 baseline. The seven affected
overlays change only target type names; hook arguments and injection points
remain subject to strict generation and installation verification.

The same build adds the native inventory `sourcePlayer` parameter. Container
add/remove and clothing overlays retain their existing plugin-facing arguments
while updating native signatures and successful-completion injection points.
The extra optional argument at `Item.RemoveFromContainer` shifts the crafting
admission and gambling branch targets. `FixItemKeyId` and `OnBigWheelLoss` use
indices in their already base-hook-patched bodies, including the unchanged
13-instruction craft and eight-instruction win injections.

NPC conversation responses gained animal selection and bounds checks. The
existing regular-response veto stays after the response null check and before
conditions/actions, now using ConversationData local 6 and ResponseNode local
20. The completed notification retains its existing position after
`ForceSpeechNode`; its index includes the nine-instruction veto injection.
Use `verify-staging-hooks.sh --dump-hook` to inspect the native indexed IL, and
focused `--requested-only --install-hook` checks during repairs before the full
qualification. Keep the generation skip list unchanged for these repairs.

Build 25830079 renumbers the unchanged report bodies again, to
`OnFeedbackReport>d__781` / `OnPlayerReported>d__780`. A native ILSpy 9.1.0.7988
comparison against the October 1 qualified reference confirms identical report
instructions, locals, operands and exception boundaries after normalizing only
the type sequence numbers and method RVA comments. The three report overlays
therefore change target type names only.

The same build adds `ignorePosition` to
`BaseEntity.GetBuildingPrivilege(OBB, bool, float, BuildingPrivlidge, bool)`.
The privilege overlay updates the native signature but passes the original
entity, bounds, cache flag, duration and exclusion arguments to plugins. The
new native flag continues through the original method when plugins return null.

Failed-placement toast/log diagnostics move `OnEntityBuilt` to native instruction
199, immediately after the successful GameObject null check. Quarry gathering
now calls `OnQuarryGather` at instruction 107, after item creation and analytics
but before pushing container-move arguments. A veto removes the same local-7
item and resumes the native loop at instruction 126 without disabling the quarry.

`BaseProjectile.CLProject` now gates clientside effects on
`BasePlayer.HasFiredProjectile` after `NoteFiredProjectile`. Patch 2 preserves
both operations: its insertion is 259 and its skip target is 278 in the body
with the actual eight-instruction `OnWeaponFired` dependency installed. The
semantic guard explicitly requires the new native check and its loop target.
