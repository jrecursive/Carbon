# verify-staging-hooks

`verify-staging-hooks.sh` checks Carbon's generated hook DLLs against the installed staging Rust managed DLLs without booting the server.

By default this is a strict release gate. It verifies every generated hook in the hook DLLs, install-tests the watched staging compatibility hook surface in isolated child processes, and does not allow generated hook suppression.

This catches the failure classes that otherwise appear during startup:

- generated hook metadata whose target type, method, or signature no longer exists
- generated Harmony patches/transpilers that produce invalid IL
- generated dynamic hooks that only fail when a plugin subscribes to them
- accidental reliance on generated hook suppression
- `OnPlayerDisconnected` moving outside Rust's native non-null `BasePlayer` branch
- `OnMarketplaceTerminalPurchase` dispatching with wrong locals or after purchase side effects
- `OnLoseCondition` failing to copy a plugin-mutated by-ref amount back before subtraction

Required semantic checks run in every normal parent verification, including
`--requested-only`. They execute transpilers over the installed Rust IL and
require the disconnect hook to stay in the non-null player branch, the
marketplace hook to cancel before eligibility/power/transaction work, and the
durability gateway to apply the value copied back from hook argument two.

The legacy focused compatibility mode is still available with `--focused-compat`, but strict staging upgrades should not use it.

## Usage

```bash
./verify-staging-hooks.sh
./verify-staging-hooks.sh --carbon-managed release/.tmp/ReleaseUnix/carbon/managed
./verify-staging-hooks.sh --hooks-dir release/.tmp/ReleaseUnix/carbon/managed/hooks
./verify-staging-hooks.sh --requested-only --install-hooks-from-log /home/johnm/git/rust-platform/current_errors.log
./verify-staging-hooks.sh --install-hook 'CanCatchFish[5fbe61]'
./verify-staging-hooks.sh --all-generated --strict-no-suppression --install-all-dynamic
./verify-staging-hooks.sh --allow-suppression-file explicit-suppressions.json
```

Defaults:

- server root: `/home/johnm/rust-staging-autoupdate/server`
- Carbon managed: `<server-root>/carbon/managed`
- hooks dir: `<carbon-managed>/hooks`

`upgrade-staging.sh` runs this verifier against the freshly built managed output before copying Carbon into the staging server.

Use `--requested-only --install-hooks-from-log` when `current_errors.log` contains hook request or patch failures and you want the fastest repair loop. The verifier parses log tokens such as `CanCatchFish[5fbe61]`, resolves them back to generated hook types, and Harmony install-tests each one in a child process. If the local generator emits a different deterministic identifier than the log's installed build, the verifier falls back to testing generated hooks with the same hook name. Child install tests run with `--max-parallel-install-checks 8` by default when called through the shell wrapper; a child crash or timeout is reported as a verifier failure instead of requiring another full server boot.

`--allow-suppression-file` is the only supported bypass for generated hook suppression. The file may be either an array of hook full names or an object with a `hooks` array. Object entries may use `hookFullName` or `hook`.

Use `--install-all-dynamic` for the full pre-install qualification gate. It is
more expensive than the watched compatibility set because each generated
dynamic hook is installed in an isolated child process, but it proves that no
fork-only `HookPolicies` or `PatchManager` exception is still required. The
verifier's compatibility expectation types are tool-local; it does not require
or compile staging shims into `Carbon.dll`. The parent performs the complete
structural pass once; each child indexes only its requested hook before install,
so isolation does not repeat all signature resolution hundreds of times. Child
processes use a bounded `Parallel.For`, avoiding thread-pool starvation from
queuing one blocking task per generated hook.
