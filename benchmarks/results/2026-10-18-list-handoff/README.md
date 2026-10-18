# Private list handoff removal, 2026-10-18 UTC

List construction now owns its private result lexically. It admits the same
capacity, populates the list and returns only after iterator cleanup finishes.
Failure discards only unpublished backing through the existing rollback helper.
The 82-line NativeListOperation class, its 128-byte temporary reservation,
registration flag and both general binder unwrapping/disposal cases are deleted.
No replacement operation, owner, manager, mode or framework is added.

Baseline: `19cd6654e4f85d4e64dbb12a2be80a44ef7c553e`. Candidate: that revision
plus [two edits and one class deletion](candidate.patch), identified by canonical
source hashes in [validation](validation.json). This does not measure later HEADs.

| 128 native builtin calls / allocated bytes | Baseline | Candidate |
| --- | ---: | ---: |
| Known-capacity list, synchronous | 53,248 | 45,056 |
| Known-capacity list, completed asynchronous | 62,464 | 54,272 |
| Custom iterator list, synchronous | 55,296 | 47,104 |
| Custom iterator list, completed asynchronous | 79,872 | 71,680 |
| Byte-sum control, synchronous | 12,288 | 12,288 |
| Byte-sum control, completed asynchronous | 12,288 | 12,288 |

Medians of eight samples after eight warmups. [Before](allocation-before.json)
and [after](allocation-after.json) retain every sample. Observed list savings are
64 bytes per call on this machine. Elapsed effects, embedding latency, actual
suspended-run allocation and CPython ratios are unmeasured for this change.
The external baseline remains separately dated in [BENCHMARKS](../../../BENCHMARKS.md).

The [temporary fixture](allocation-diagnostic.cs) calls existing builtins through
ICallable. Context, governed input, callable and argument preparation stay outside
the initiating-thread counter. Binding, list construction, iteration and disposal
stay inside. Lists have four integers from a known tuple or custom iterator;
the sum control consumes 4,096 bytes. Complete values, exactly 128 custom cleanup
calls, retained list funding, zero working reservation and return of each owner's
allowance to zero are checked. Async operations complete without suspension.
Public scheduling, guest opcode loops, startup/compile and host I/O are excluded.
No GC was requested. The diagnostic was removed before final matching builds.

Machine: Windows build 26200, x64, Intel Core i7-14700KF, 20 cores / 28 logical
processors. [Toolchain](dotnet-info.txt), environment, source/assembly hashes and
bounded process receipts are in validation. No VM timing, trace, long lane or new
benchmark framework ran.

The deleted wrapper performed no budget-owner transfer at registration; it only
disabled private rollback before extracting the result. The constructor now
expresses that boundary directly. Existing capacity/replacement admission,
DiscardUnpublishedStorage, element ownership, arity checks and collection/fuel
checks remain. Both successful result and source/element aliases stay charged
to their existing owners. Only failed private backing is refunded. Removing the
control object lowers required headroom; identical tiny thresholds are not
promised. See [MEMORY](../../../MEMORY.md).

[Baseline](baseline-behavior.trx) and [final](candidate-behavior.trx) preserve
18 exact sync/async records for native/custom empty, two- and 65-item inputs,
move/cleanup/both failures and collection limits. Every fuel cutoff freezes span,
effects and disposal/exception order. Complete record digests keep the source
fixture small; raw records retain their values. There is no runtime capture gate.

Three explicit-budget cases pause inside an actual DisposeAsync implementation.
They check committed backing separately from temporary control funding and prove
the result is unavailable while cleanup is pending. Success retains output and
its alias; cleanup error/cancellation refunds only private backing. Cleanup runs
once, all users are released/awaited before owner disposal, and allowance returns
to zero. Existing native-list growth/refusal/rollback and frame witnesses remain.

All 984 distinct final focused Release cases pass, including shared allowances,
retained results/caches, terminal refusal, real callback suspension/cancellation,
managed stack protection and 58 complete CSV/text/JSON/file jobs. Validation
records 197 white-box and 787 public cases. No external assertion, timeout or
Stress scale was relaxed. Full local Stress was not repeated; preserved original
scales are the next aggregate-ownership checkpoint, with platform/package checks
batched at delivery. No old operation class or binder handoff remains in supported
production/test sources.
