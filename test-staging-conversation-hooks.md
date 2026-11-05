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
and completed/ended ordering. Build `26341433` uses the scoped response policy:
all native `HandleResponse` instructions survive except its final transition
call, which uses the exact reviewed native ended/successful split. A local keeps
the original selected response across effects, including null for mission paths.
The handler and four host helpers are bound by exact module and resolved-body
pins at runtime installation. The focused checks exercise null, false, true and
other non-null veto returns using plain authoring-node data and a stub dispatcher;
no entity, conversation condition, reward, mission or network method executes. It runs against publicized and raw assemblies.
Generated source and build work stay disposable; no runtime or installed
reference is modified. The same semantic check runs in `verify-staging-hooks.sh`.

For build `26445886`, the reviewed module advances to
`d49eb2a7-6812-4cba-aca8-e36577de4e28`. Independent previous/raw/publicized
inspection proves all five pinned response/host method shapes unchanged,
including resolved instructions and branch targets, local types, stack size,
local initialization and exception-region count. Only the module guards change;
the five body hashes, callback ABI, null-only veto, original response selection,
successful/ended transition and either-order detach policy stay intact.
