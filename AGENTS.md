# AGENTS.md

## Rust Decompiled Source Reference

When fixing hook, signature, IL transpiler, or Rust API compatibility problems in this repository, you may use the decompiled Rust server source at `~/rust-src` as a read-only reference.

- Prefer `~/rust-src` when source-level context is needed to understand current Rust method names, compiler-generated local function names, parameter lists, locals, or control flow.
- Cross-check decompiled source against the installed managed assemblies under `rust/linux/RustDedicated_Data/Managed` or the active staging server when exact runtime signatures matter.
- Do not modify files under `~/rust-src`; treat them as external reference material only.
- If `~/rust-src` disagrees with the installed managed DLLs, treat the installed DLLs/runtime being patched as authoritative.
