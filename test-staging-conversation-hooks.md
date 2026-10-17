# Conversation response hook checks

Run after generating hooks for the selected installed Rust assemblies:

```sh
./test-staging-conversation-hooks.sh \
  --generated-dir /absolute/path/to/current/generated \
  --managed /absolute/path/to/publicized/Managed \
  --raw-managed /absolute/path/to/raw/Managed
```

The wrapper composes the two actual generated response transpilers and verifies
native owner RPC admission, conversation and visible-choice admission, response
bounds/null checks, NPC/player/conversation/response argument bindings, non-null
cancellation before native conditions/actions, and completion after native
`ForceSpeechNode` without running native entities. It installs both transpilers
in the CLR and rejects unsafe mutations of the arguments, gates, cancellation
and completed/ended ordering. It runs against publicized and raw assemblies.
Generated source and build work stay disposable; no runtime or installed
reference is modified. The same semantic check runs in `verify-staging-hooks.sh`.
