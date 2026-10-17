# Livestock familiarity hook checks

Run after generating hooks for the selected installed Rust assemblies:

```sh
./test-staging-livestock-familiarity.sh \
  --generated-dir /absolute/path/to/current/generated \
  --managed /absolute/path/to/publicized/Managed \
  --raw-managed /absolute/path/to/raw/Managed
```

The wrapper compiles the actual generated admission prefix and completion
postfix. It checks the native two-argument signature and six return paths,
argument boxing/identity, null-only continuation, and the `__runOriginal` gate;
installs both patches on the native method without executing an entity; and
rejects unsafe replacements. The same generated patches execute against an
inert six-return fixture for null, boxed false, true and string veto results,
including the native exception path. Both publicized/raw assembly sets are
checked. No Rust server or installed reference is changed. The authoritative
verifier runs the same static semantic guard during normal qualification.
