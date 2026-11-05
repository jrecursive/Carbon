# Web RCON admission semantic regressions

Run `./test-staging-rcon-hooks.sh` after hook generation. Override generated C#,
publicized references or installed raw references with `--generated-dir`,
`--managed`, and `--raw-managed` respectively. Corresponding environment settings
are `CARBON_HOOKGEN_GENERATED`, `CARBON_STAGING_MANAGED_PUBLICIZED` and
`CARBON_STAGING_MANAGED_PATH`.

The test extracts the actual generated web admission base and companion patch
transpilers, composes them on the installed Listener.OnConnection IL, and runs the
production semantic guard. It repeats against raw and publicized assemblies.
No listening socket, RCON request, Rust process or remote operation is created.
Seven checks per assembly cover valid composition and rejection of a missing
companion patch, veto-to-registration, veto-to-return without close, wrong socket
argument, removed Close call and skipped native password check.

Required behavior: banned sockets close without entering the plugin hook; a null
hook result continues native password validation; every nonnull result follows
the existing failed-connection profiler/Close/accounting path and cannot allocate
a client ID or register callbacks. Numeric insertion positions are staging-specific
and must be requalified from IL on future updates. Do not suppress these hooks or
substitute unconditional return for socket cleanup.

`RconHookSemantics` is also invoked by the normal staging verifier on the real
compiled hook assembly; the focused test supplies mutation regressions around that
same guard. The test project needs the existing .NET 10 SDK and Harmony dependency.
