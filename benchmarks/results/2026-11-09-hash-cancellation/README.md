# Native-hash cancellation scheduling correction

R137,2026-11-09. CI42558487979/source6d73 passed full Ubuntu actual11/10,
original Stress both and installed-core consumers; Windows ordinary actual11
failed only the native binary-hash fixture's IsCompleted assertion, before any
cancellation request. Its parent resumed after the64MiB scan completed. Remaining
Windows stages were skipped; do not label that checkpoint fully qualified.

Keep the same64MiB input, allowance,10s failure deadline, exact failure/span,
unpublished cache, later-caller hash, unchanged input and refund assertions.
Arm1ms cancellation on the hashing worker immediately before the warmed native
entry instead of waiting for the parent's scheduled continuation. Move these two
timed cases to Stress; fourteen small deterministic cases remain ordinary. Await
the actual user on all paths before disposing funded input. This is concurrent
stress evidence, not a deterministic claim about thread scheduling.

Mutation check removes only the two in-loop checks in PyString.HashUtf8, retaining
its entry check. Both text/bytes tests fail with no exception, demonstrating that
this observed run detects missing in-loop cancellation. Restore exact source,
force fresh nonincremental builds, then16/16 cases pass on each actual.NET11/10.
No runtime code/checkpoints, fixture size, timeout or containment guarantees change.
New full delivery qualification batches with the next coherent source checkpoint.
Publication remains postponed.

Original fixture/CI failure, mutation log and fresh build/test logs are retained
here. The current implementation stays unchanged after restoring the mutation.
