# Exact collectible hook repair

`./repair-collectible-hook.sh` builds an offline, scoped repair of the captured
staging `Carbon.Hooks.Oxide.dll`. It preserves unrelated runtime hook repairs in
that exact assembly. The durable source correction is the
`OnCollectiblePickedup` entry in `staging-hookgen-overlays/staging.json`.

```sh
./Carbon/repair-collectible-hook.sh --input /path/to/captured/Carbon.Hooks.Oxide.dll --dry-run
./Carbon/repair-collectible-hook.sh --input /path/to/captured/Carbon.Hooks.Oxide.dll \
  --output runtime-logs/catnip-fix-20261024/artifact --apply
```

Run from the platform repository root. Input/native hashes and the source overlay
must match the reviewed October 24 repair. `--server-root` or
`CARBON_STAGING_ROOT` selects the read-only reference. The default is
`/home/johnm/rust-staging-autoupdate/server`. Required tools are Bash, Python 3,
Mono/`mcs`, `realpath`, and the reference's `Mono.Cecil.dll`.

Dry-run validates inputs and makes no writes. Apply accepts only a new output
directory and never writes installed references or contacts a runtime. It adjusts
the generated posthook's injection index from 146 to 128 and item local from 9
to 8, including corresponding parameter metadata. The source input is preserved.

The builder permits changes to exactly one method body and one type's parameter
attribute. It compares the full expected assembly model after round-trip,
including method IL, branch targets, exception handlers, locals, member and type
metadata, custom attributes, references and managed resources. It requires
repeatable deterministic DLL bytes with a distinct deterministic module ID.
`qualification.json` pins original/candidate/native/overlay/tool hashes and the
structural evidence. Successful original/candidate and receipt files are read-only.

This receipt qualifies only the scoped binary transformation. Before deployment,
run the production collectible semantic guard and focused install verification
against this candidate. A successful focused repair is not a full platform
release qualification. Remote publication, stopped-state checks, backup and
rollback are owned by the checked-in deployment wrapper; this command performs
none of those actions.
