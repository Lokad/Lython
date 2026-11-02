# Existing compilation trace: sampler inventory — 2 November 2026

**No new guest run, profile capture or timing. No CPU-fraction claim.** This offline
inventory reuses the original B80 trace to check whether it can guide a larger
compiler redesign. It contains sampler events, but does not establish a reliable
normal-execution CPU breakdown.

The original 2.01042-second allocation-trace window contains 12,866 SampleProfiler
Thread/Sample events: 11,021 External and 1,845 Managed. Only 1,818 resolved stacks
contain actual LythonEngine.CompileCore: 1,817 Managed/one External across the same
four compiler threads identified by allocation analysis. Caller-side Compile wait
stacks are excluded. Allocation ticks are never counted as CPU samples.

Of these rooted sampler stacks, **1,683 have Thread.PollGC at the leaf**. The trace
also records 1,860 suspend/restart pairs and 1,897 method-JIT starts in this window.
External samples include waiting/native activity; Managed is not proof of an
uninterrupted execution-time sample. This is an intrusive allocation-collection
window, with sampling/safepoint and active JIT effects. Its stage stacks do not
justify assigning fractions of ordinary compilation latency or ranking removable
CPU costs. The top remaining non-poll leaf is RuntimeHelpers.GetHashCode (30
samples); this is a hypothesis to inspect, not demonstrated dominance.

| CompileCore-rooted stage stacks, all payload types | Count |
| --- | ---: |
| Abstract interpretation | 550 |
| Executable construction | 371 |
| Parsing | 325 |
| Lexical preparation | 231 |
| Annotation diagnostics | 163 |
| Syntax validity | 149 |

Counts are diagnostic stack inventories, not additive elapsed phases, CPU shares
or predicted gains. Different phases have different safepoint/JIT behavior.

The source review also finds constraints on combining validation/preparation:
syntax validity visits reversed stack order; generator checks use source-order
recursive function/bare-yield context; modern type/postponed annotation checks
apply different scope exclusions and concatenate diagnostic groups in a fixed
order. Scope-directive validation consumes completed parent binding facts.
A single common loop cannot simply replace those passes while preserving the
current error list/priority. No new visitor framework, event-sorting layer, cache
or skipped pass is introduced without a stronger cost/design case.

The [pre-assessment-plan.md](pre-assessment-plan.md) fixes this inventory's limits.
[Program.cs](Program.cs), [events.json](events.json) and
[stack-inventory.json](stack-inventory.json) preserve the offline reader and exact
counts/stack examples. [assessment.json](assessment.json) records the original
trace hash. Its unchanged nettrace/worker/quiet/ownership evidence remains in
[B80's archive](../2026-11-02-current-compilation-attribution/README.md).
No duplicate raw trace or new core dependency. Existing delivered runtime and
closed B81/B83 experiments are unchanged.
