# build-staging-hooks

`build-staging-hooks.sh` generates `Carbon.Hooks.Oxide` source locally for the installed Rust staging server.

The default OPJ input is Oxide.Rust's `staging` branch:

```bash
https://raw.githubusercontent.com/OxideMod/Oxide.Rust/staging/resources/Rust.opj
```

The script applies `staging-hookgen-overlays/staging.json` when the upstream OPJ metadata is stale for the installed staging DLLs. Every overlay patch must match exactly one hook and must actually change its original value. It writes the fetched OPJ, patched OPJ, overlay report, generated C# source, generator summary, and manifest under `release/.tmp`.

`staging-hookgen-expected-skips.json` is an exact allowlist for hooks the
generator intentionally skips. The build fails when the actual skipped hook set
differs, including when a previously expected skip becomes generatable. Update
the list only after reviewing the installed Rust IL and the generator result.

## Usage

```bash
./build-staging-hooks.sh
./build-staging-hooks.sh --opj /path/to/Rust.opj
./build-staging-hooks.sh --opj https://example.invalid/Rust.opj
./build-staging-hooks.sh --output-root release/.tmp/ReleaseUnix/staging-hookgen
```

Environment overrides:

- `CARBON_STAGING_ROOT`
- `CARBON_STAGING_MANAGED_PUBLICIZED`
- `CARBON_HOOKGEN_OPJ_URL`
- `CARBON_HOOKGEN_OPJ_OVERLAY`
- `CARBON_HOOKGEN_EXPECTED_SKIPS`
- `CARBON_HOOKGEN_OUTPUT_ROOT`
- `CARBON_HOOKGEN_VALIDATION_MODE`

`upgrade-staging.sh` uses this script before building Carbon with `HOOKGEN`.
