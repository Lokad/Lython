# B44 — shared execution-state consolidation (selected BEFORE code).**
Hypothesis: forwarding objects add dependent loads on every instruction and long
builtin/host guard checks. Collapse ExecutionServices, ExecutionGuards and the
ExecutionLimits holder into existing ExecutionState. Move their REAL operations,
current exception, cancellation token and unchanged nesting fields there. Delete
those classes, their construction/back-references and Services/Guards/Limits
aliases; update real callers, without compatibility wrappers. Keep lexical
ExecutionContext/frame objects, cells and storage separate. No frame pooling,
new guard/cache/state abstraction, policy, option or per-object accounting.

Source acceptance: one shared state per execution, no discarded façade types or
forwarding compatibility properties. Host I/O still receives the same authority,
borrowed budget and current token; retained resources keep the same state lifetime.
First refused admission still precedes cancellation/depth checks and remains
terminal through guest suppression/finally. Preserve check locations, default/
explicit recursion limits, 512 managed nesting bound, RuntimeHelpers stack entry,
current exception/traceback/finally behavior, closures/generators, async callbacks,
imports/provider-cache token isolation and deterministic awaited cleanup. Negative
option validation order/messages remain. Coarse estimates need no precision tweak.
No public API change; internal façade APIs disappear, architecture-only assertions
may disappear but all independent behavior/ownership checks remain.

Validation: focused Release state/guard/codec/call/stack/ownership/refusal/import/
regex and representative families, then actual second target. Before freezing,
nonincremental build and verify source/loaded core identities. Freeze byte-identical
auxiliary host/adapter binaries in both roles, replacing only core for candidate.
One fixed text+CSV before/candidate/identical-before control, existing4warmups /
7batches of128 jobs, <=30s/child and90s aggregate, ordinary tiering, complete
goldens/ownership, quiet gates, >100ms CPU batches; CPU control drift<=5% /
allocation drift<=0.2%. Existing compilation-only micro is the unaffected-phase
control (same source; no execution-state construction), with phases reported
separately; all collection <=90s plus owned cleanup headroom. No new framework.

Performance success requires >=5% lower CPU against BOTH controls on at least one
complete execution workload without >5% CPU /2% allocation regression on the other.
Source simplification has independent value: it may remain without a qualified
speedup if every semantic/ownership/source criterion passes and valid controls do
not show >5% CPU regression against BOTH baselines. Flag adverse/inconclusive
observations; do not tune or retime for acceptance. Never call allocation savings
latency. Stop/remove on semantic failure or demonstrated material regression;
retain unchanged tests/evidence. Full platform/stress/package at retained-source
boundary only. Final adapter qualification remains independent.
