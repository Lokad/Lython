# Fixed builtin dictionary capacities

Two existing per-execution dictionaries now start with capacity hints for their
fixed entries: 17 object members and 103 builtins (a 128-entry hint leaves room
for additions). This avoids repeated backing-array growth during invocation
setup. Dictionaries, callables, mutable type graphs and descriptor caches still
belong to their execution. No new cache, owner, registry, binder or public contract.

| Warm native context construction | Baseline allocated bytes | Candidate allocated bytes |
| --- | ---: | ---: |
| One invocation, median of 32 fresh owners | 26,088 | 19,312 |

The reduction is 6,776 bytes per context. This is an earlier-Lython allocation
comparison, with no elapsed-time, CPython, physical-heap or embedding-latency
claim. External performance comparisons remain in
[BENCHMARKS.md](../../../BENCHMARKS.md).

Measured 2026-10-18 UTC on an Intel Core i7-14700KF, Windows 10.0.26200 x64,
Release net10.0. Baseline is immutable
`b818855bf07b37e077ec7d43215aea0ba182eab3`; candidate is the frozen
[one-file patch](candidate.patch) on that revision. [Validation](validation.json)
records exact source hashes, test receipts and toolchain identity. These
measurements do not describe an arbitrary later HEAD.

The [temporary recipe](BuiltinCapacityAllocationDiagnostic.cs.txt) reuses the
existing fixed-builtin diagnostic. It warms twice, then measures native
ExecutionContext construction for 32 sequential fresh owners sharing one
2 MiB allowance. Host/options creation and owner disposal are outside the
current-thread allocation counter. Compilation, public scheduling, startup and
actual guest execution are excluded. No requested GC or protection switches.
The same builtin count, module entries, object identity and OSError/IOError alias
are checked; each owner returns the allowance to zero. All
[samples and comparison boundaries](allocation.json) are preserved.

All 954 distinct existing focused Release cases pass, including builtin
signatures/binding, private type/module isolation, concurrent suspended executions,
retained results/caches, terminal admission failure, cancellation, managed stack
admission and 58 complete CSV/text/JSON/file goldens. No permanent test was added
or changed to enforce these capacity hints. The diagnostic was removed before
final builds. Existing assertions, timeouts and original Stress scales remain.

Module-variable copying already uses the count-aware dictionary constructor;
it was left unchanged. Signatures are already interned, so repeated signature
layout construction is not claimed. Capacity hints are not guest limits: future
entries can grow naturally. Existing conservative accounting and host headroom
remain. The preceding original-scale stress checkpoint and suffix-call CI are
separate immutable deliveries; this precommit artifact does not claim current
platform or Release-package qualification, which is recorded at delivery.
