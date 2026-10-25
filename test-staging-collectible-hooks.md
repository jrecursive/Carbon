# Collectible pickup notifications

Run against the exact candidate hook DLL and its compatible Carbon libraries:

```sh
./test-staging-collectible-hooks.sh --hooks-dir /absolute/candidate/hooks \
  --carbon-managed /absolute/current/carbon/managed \
  --managed /absolute/publicized/Managed --raw-managed /absolute/raw/Managed \
  --output /existing/evidence/new-verification.json
```

The wrapper composes the actual generated `OnCollectiblePickup` and
`OnCollectiblePickedup` transpilers against current publicized and raw Rust IL,
then tests CLR patch installation in the checker process. It never starts Rust,
constructs entities, performs a harvest, or changes installed assemblies.

Optional `--output` requires a new JSON path in an existing directory. Only a
complete successful raw/publicized run publishes a `carbon-collectible-hook-verification`
PASS receipt, binding the candidate, both native inputs, guard/test sources and
retained log hashes. Failed checks retain their logs without publishing PASS.

The production verifier uses the same semantic guard. It requires the existing
null-only pickup veto, exact entity/player/created-Item arguments, and notification
after receiver analytics and before `GiveItem`. Every native opcode, operand and
branch survives: null-created items, null receivers and consumed food skip the
notification; the output loop creates and notifies separately for each successful
entry before the collectible is killed. Plant-level Catnip deduplication stays
the plugin's responsibility.

Focused negative cases reject the old drop-only index even with the Item argument
corrected, the old consume-component local, wrong receiver identity, changed
notification return handling, branches that admit null/consumed paths, broken
output iteration, altered pickup veto and changed native delivery.
