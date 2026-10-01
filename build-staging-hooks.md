# build-staging-hooks

`build-staging-hooks.sh` generates `Carbon.Hooks.Oxide` source locally for the installed Rust staging server.

The default OPJ input is Oxide.Rust's `staging` branch:

```bash
https://raw.githubusercontent.com/OxideMod/Oxide.Rust/staging/resources/Rust.opj
```

The October 1 staging review pins those source bytes to SHA-256
`04565f727cf661acbba068dbc080f236ab237dcab2b64984f0c72a2e1a4b680e`.
The build fails before applying overlays if the remote content drifts. Override
the expected checksum only together with a reviewed Rust/OPJ update by using
`--opj-sha256` or `CARBON_HOOKGEN_OPJ_SHA256`.

The script applies `staging-hookgen-overlays/staging.json` when the upstream OPJ metadata is stale for the installed staging DLLs. Every overlay patch must match exactly one hook and must actually change its original value. It writes the fetched OPJ, patched OPJ, overlay report, generated C# source, generator summary, and manifest under `release/.tmp`.

The October 1 upstream update adds cutting and livestock hooks and adopts the
previous report, Deep Sea, RCON, inventory, crafting and conversation retargeting.
Those redundant overlay fields are removed. The remaining overlay preserves
native return signatures, construction cleanup, explosive notification ordering,
limited-networking effects and stable container/item notification arguments.

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
