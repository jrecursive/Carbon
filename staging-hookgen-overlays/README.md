# Staging Hookgen OPJ Overlays

`build-staging-hooks.sh` fetches `Rust.opj` from the Oxide.Rust staging branch and applies the overlay in this directory before running `Carbon.Hooks.Generator`.

Use overlays only when the upstream staging OPJ is stale for the installed staging server DLLs. Each patch must target one exact OPJ hook `Name`, include a reason, and set only the fields needed to make generated hooks match the installed staging IL.

The build rejects overlay entries that match zero or multiple hooks and rejects
stale entries whose old and new values are equal. Remove a patch as soon as the
upstream OPJ carries the same correction.

Fetched OPJ files, patched OPJ files, generated C# source, summaries, and patch reports are written under `release/.tmp` and are not committed.
