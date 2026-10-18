# Sum/list state removal, 2026-10-18 UTC

`sum` now returns its result from managed locals and uses ordinary synchronous
or awaited iteration for cleanup. Its private operation allocation, 128-byte
temporary reservation and internal result wrapper are gone. The remaining
helper is narrowed to list construction; source/current fields, constructor
arguments and reference-clearing/per-item bookkeeping are deleted. List capacity
admission, unpublished-storage rollback and result handoff remain.

Baseline: `92c5b9e6e5eddd86456b002a3549e5029bc7f31b`. Candidate: that revision
plus [three edits and one helper narrowing/rename](candidate.patch), with canonical
source hashes in [validation](validation.json). This does not measure a later HEAD.

| 128 native builtin calls / allocated bytes | Baseline | Candidate |
| --- | ---: | ---: |
| 4,096-byte checksum, synchronous | 22,528 | 12,288 |
| 4,096-byte checksum, completed asynchronous | 22,528 | 12,288 |
| Four-item tuple sum, synchronous | 53,248 | 43,008 |
| Four-item tuple sum, completed asynchronous | 62,464 | 52,224 |
| Four-item custom iterator sum, synchronous | 55,296 | 45,056 |
| Four-item custom iterator sum, completed asynchronous | 79,872 | 69,632 |
| List construction counterpart, synchronous | 57,344 | 55,296 |
| List construction counterpart, completed asynchronous | 81,920 | 79,872 |

Medians of eight samples after eight warmups. [Before](allocation-before.json)
and [after](allocation-after.json) retain every sample. Observed savings are
80 bytes per sum call and 16 per list construction on this machine. These are
allocation measurements; elapsed effects, embedding latency, actual suspended-run
allocation and CPython ratios are unmeasured for this change. Existing external
comparisons remain separately dated in [BENCHMARKS](../../../BENCHMARKS.md).

The [temporary diagnostic](allocation-diagnostic.cs) invokes existing builtins
through ICallable. Context, governed input, callable and argument setup stay
outside the initiating-thread counter. Binding, reduction, iteration and disposal
stay inside. Tuple/custom inputs have four integers; the byte input is 4,096
bytes. Custom cleanup runs exactly 128 times; complete sums/list contents and
zero working reservation are checked. List storage remains charged through its
owner's lifetime; every owner returns the shared allowance to zero on cleanup.
Both async paths complete without actual suspension. Public scheduling, guest
opcode loops, compilation/startup and host I/O are excluded. No GC was requested.
The fixture was removed before final matching Release builds.

Machine: Windows build 26200, x64, Intel Core i7-14700KF, 20 cores / 28 logical
processors. [Toolchain](dotnet-info.txt), environment, source/assembly hashes and
bounded build/test receipts are retained in validation. No VM timing, trace,
long lane or new benchmark framework ran.

The exact-byte reduction/checkpoint loop and sum validation are unchanged apart
from the result destination. Generic arithmetic/protocol dispatch retains its
order; language iteration disposes synchronously or awaits disposal on success,
failure and cancellation. List's conservative 128-byte shell estimate, capacity
preflights, private rollback and result handoff are retained. No generic aggregate
manager or tracing substitute replaces the deleted state. No production or test
reference to the old operation type remains.

The contract tradeoff is lower control headroom for `sum`: there is no longer a
private object to fund. This does not refund source, accumulator aliases or result
storage; existing borrowed execution/result owners keep them charged independently
of CLR liveness. Minor iterator/state-machine storage uses existing runtime
headroom. Identical tiny-budget thresholds are not promised. See
[MEMORY](../../../MEMORY.md).

[Baseline checks](baseline-behavior.trx) pass 150 cases before production edits.
The corresponding candidate group passes 149: one test requiring discarded
private fields/return architecture is deleted. Useful denied-admission/frame
preservation coverage is retained through the list callable. Sum tests now
inspect direct results; arithmetic, before-allocation refusal bounds and retained
funding assertions remain. Six cleanup/failure cases use a borrowed shared budget
and a funded 4,096-byte value. Actual async cleanup blocks until explicitly
released and awaited, runs once and preserves funding through cleanup; allowance
returns to zero when ownership ends. Nested/reentrant guest accumulators remain
covered. No forced-GC test was introduced.

All 963 distinct final focused Release cases pass, including native/generic fuel
and span equivalence at 64/65-byte boundaries, large integer admission, retained
results across shared-budget executions, list rollback/aliases, actual callback
suspension/cancellation, managed stack protection and 58 complete CSV/text/JSON/
file jobs. [Final white-box records](candidate-behavior.trx) and validation retain
the receipts. No external assertion, timeout or Stress scale was relaxed. Full
local Stress was not repeated; platform/package checks are batched at delivery.
