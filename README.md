# Carbon.Native
Native library of Carbon.

Profiler config accepts an optional `Diagnostics` boolean (default `false`).
When enabled, each capture also retains a bounded exception census and GC timing
distribution. Exception buckets identify type, assembly, thread category and
capture-local thread/stack fingerprints, up to sixteen method names and the
nearest instrumented method. Exception messages, object values and player data
are never retained. Counts include throws without an instrumented stack. A
maximum of 128 buckets is retained; `exceptions.overflow` counts events from
additional signatures. Repeated retained signatures continue accumulating.

GC timing spans `PreStopWorld..PostStartWorldUnlocked`, including suspension and
restart overhead. It does not identify managed generations or claim precise
stop-the-world subphase timing. Totals, maximum and nine histogram bins cover
the complete capture; the last 256 completed events are retained with capture
offsets. `gc.overwrittenEvents` describes discarded event detail.

`carbon_profiler_diagnostics_snapshot(dst, capacity)` optionally exposes the
completed capture as UTF-8 JSON with `schemaVersion: 1`, without changing the
upstream native protocol **5**, managed protocol **127**, or `GCRecord`. It returns zero when no completed diagnostic
capture exists, `-2` while recording, and `-1` for insufficient capacity. A null
destination with zero capacity probes the positive required byte length. A
successful copy returns that length and writes no trailing NUL. The snapshot
is assembled at stop under the collector mutex; export never enumerates live
thread records. Starting another enabled capture clears the prior snapshot;
aborting produces no completed snapshot.

From the runtime platform root, use
`./util/profiler-repair/build-profiler.sh --output-dir /tmp/washed-profiler-build`
for local native checks and both profiler builds.
Implementation detail: `cargo test --manifest-path
Carbon/src/Carbon.Native/Cargo.toml --lib` checks bounded aggregation, histogram
edges, metadata limits and snapshot serialization. Point `CARGO_HOME` and
`CARGO_TARGET_DIR` at writable caches and include this package's `lib` directory
in `LD_LIBRARY_PATH`. No command requires a running Rust server.

The maintained branch integrates upstream `913fc30f` while retaining diagnostics.
Upstream recording uses synchronized `Arc` thread slots and an atomic stop gate;
completed diagnostic exports never read those live slots. Exception attribution
reads the nearest instrumented frame through the recorder accessor, then uses
public Mono metadata getters with opaque class/method/image handles. Diagnostics
still default off, use no exception message/object payloads, and keep the same
128-bucket/16-frame/256-GC-event limits and JSON snapshot ABI. GC sampling runs
alongside upstream atomic `GCRecord` accounting, including worker callbacks.

The Cargo library target is `carbon_native` to follow Rust crate naming rules.
Packaging renames `libcarbon_native.so` / `carbon_native.dll` to the existing
`libCarbonNative.so` / `CarbonNative.dll` runtime filenames; exported C symbols
and protocol remain unchanged.
