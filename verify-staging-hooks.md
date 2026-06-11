# verify-staging-hooks

`verify-staging-hooks.sh` checks Carbon's generated hook DLLs against the installed staging Rust managed DLLs without booting the server.

By default this is a strict release gate. It verifies every generated hook in the hook DLLs and does not allow generated hook suppression.

This catches the failure classes that otherwise appear during startup:

- generated hook metadata whose target type, method, or signature no longer exists
- generated Harmony patches/transpilers that produce invalid IL
- accidental reliance on generated hook suppression

The legacy focused compatibility mode is still available with `--focused-compat`, but strict staging upgrades should not use it.

## Usage

```bash
./verify-staging-hooks.sh
./verify-staging-hooks.sh --carbon-managed release/.tmp/ReleaseUnix/carbon/managed
./verify-staging-hooks.sh --hooks-dir release/.tmp/ReleaseUnix/carbon/managed/hooks
./verify-staging-hooks.sh --allow-suppression-file explicit-suppressions.json
```

Defaults:

- server root: `/home/johnm/rust-staging-autoupdate/server`
- Carbon managed: `<server-root>/carbon/managed`
- hooks dir: `<carbon-managed>/hooks`

`upgrade-staging.sh` runs this verifier against the freshly built managed output before copying Carbon into the staging server.

`--allow-suppression-file` is the only supported bypass for generated hook suppression. The file may be either an array of hook full names or an object with a `hooks` array. Object entries may use `hookFullName` or `hook`.
