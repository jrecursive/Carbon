# build-staging-hooks

`build-staging-hooks.sh` generates `Carbon.Hooks.Oxide` source locally for the installed Rust staging server.

The default OPJ input is Oxide.Rust's `staging` branch:

```bash
https://raw.githubusercontent.com/OxideMod/Oxide.Rust/staging/resources/Rust.opj
```

The October 9 staging review pins those source bytes to SHA-256
`30b0a96c8b7587845982a16c7d84e9348825bdc8303f37fdbc45030696be7bc5`.
The build fails before applying overlays if the remote content drifts. Override
the expected checksum only together with a reviewed Rust/OPJ update by using
`--opj-sha256` or `CARBON_HOOKGEN_OPJ_SHA256`.

The script applies `staging-hookgen-overlays/staging.json` when the upstream OPJ metadata is stale for the installed staging DLLs. Every overlay patch must match exactly one hook and must actually change its original value. It writes the fetched OPJ, patched OPJ, overlay report, generated C# source, generator summary, and manifest under `release/.tmp`.

For Rust build `26270245`, the overlay retargets the five renamed async/coroutine
types used by eight Deep Sea, report and Steam inventory hook entries. Deep Sea
and report bodies are unchanged apart from their type sequence numbers. Steam
inventory retains its existing successful `SetItems` notification boundary and
the newly added native default-item skin update after disposal. Hook arguments,
injection offsets, frozen OPJ bytes and the expected skip set are unchanged; see
`staging-hookgen-overlays/README.md` for the exact native assembly identity.

The October 1 upstream update adds cutting and livestock hooks and adopts the
previous report, Deep Sea, RCON, inventory, crafting and conversation retargeting.
Those redundant overlay fields are removed. The remaining overlay preserves
native return signatures, construction cleanup, explosive notification ordering,
limited-networking effects and stable container/item notification arguments.

For Rust build 25830079, the October 9 source adds
`CanLivestockAnimalRefuseTarget` and corrects the sense-component target hook.
The overlay additionally binds the new native privilege-lookup `ignorePosition`
argument while retaining the previous plugin arguments, and retargets both
report async bodies to their new numbered types. The report IL remains identical
after normalizing only type sequence numbers and method RVA comments. Generation
produces 817 hooks with the unchanged 45 expected skips.
The same review moves construction notification after the new failed-placement
diagnostics, restores quarry cancellation before native container-move arguments,
and preserves the new native fired-projectile check before suppressing limited
players' clientside projectile effects.

`staging-hookgen-expected-skips.json` is an exact allowlist for hooks the
generator intentionally skips. The build fails when the actual skipped hook set
differs, including when a previously expected skip becomes generatable. Update
the list only after reviewing the installed Rust IL and the generator result.

## Usage

```bash
./build-staging-hooks.sh
./build-staging-hooks.sh --opj /path/to/Rust.opj
./build-staging-hooks.sh --opj https://example.invalid/Rust.opj
./build-staging-hooks.sh --opj /path/to/Rust.opj --opj-sha256 <sha256>
./build-staging-hooks.sh --output-root release/.tmp/ReleaseUnix/staging-hookgen
```

Environment overrides:

- `CARBON_STAGING_ROOT`
- `CARBON_STAGING_MANAGED_PUBLICIZED`
- `CARBON_HOOKGEN_OPJ_URL`
- `CARBON_HOOKGEN_OPJ_SHA256`
- `CARBON_HOOKGEN_OPJ_OVERLAY`
- `CARBON_HOOKGEN_EXPECTED_SKIPS`
- `CARBON_HOOKGEN_OUTPUT_ROOT`
- `CARBON_HOOKGEN_VALIDATION_MODE`

`upgrade-staging.sh` uses this script before building Carbon with `HOOKGEN`.
