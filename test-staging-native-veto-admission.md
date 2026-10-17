# Native crate and Frankenstein admission hooks

Run after generating hooks for the selected Rust input:

```sh
./test-staging-native-veto-admission.sh \
  --generated-dir /absolute/current/generated \
  --managed /absolute/publicized/Managed \
  --raw-managed /absolute/raw/Managed
```

The wrapper compiles the actual generated `CanHackCrate` and
`OnFrankensteinPetSleep [FrankensteinTable]` transpilers and applies them to the
real methods for semantic inspection and CLR installation, without creating or
executing Rust entities. Both publicized and raw input assemblies are checked.

The shared verifier guard requires native eligibility checks before admission,
the existing argument identities, and null-only continuation before analytics,
owner/item mutation, networking and lifecycle effects. Every original opcode,
operand and branch target must survive the injection. Native branches to the
admission point must enter the hook. The proof rejects the old occupied-stack
offsets and mutations to arguments, veto comparison/return, eligibility checks
and branch targets. It does not run a Rust server or change installed references.
