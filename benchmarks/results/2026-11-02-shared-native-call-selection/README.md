# B116: shared native unary-call selection

2026-11-02, baselinebe96e231/runtime609e5de1 versus recorded uncommitted source.
One selector replaces the sync and async unary branch matrices and the separate
NativeReturnBuiltin selector. It reuses existing operation delegates and accepts
actual operands or already-proved native result shapes. Leaf operands no longer
require classification merely to select the same operation again. No new cache,
descriptor, argument storage, owner, policy or compiler metadata. About15 net
production lines removed. Native sum(bytes) uses the existing implementation too.

Ordinary len retains its broader non-PyInstance behavior. A native return still
requires a closed proof, so unknown/protocol-bearing lengths use the existing
engine. All eligibility precedes native work; no retry after effects/refusal.
Real contexts, evaluation/source/error order, ownership and current tokens remain.

## Measurements and decision

Retain as a coherent simplification. No demonstrated general CPU or allocation
improvement is claimed. The earlier6–7% CSV CPU debt is **not closed**.

Two independent-process blocks reuse the established worker/protocol with4s timed
warmup,9 interleaved~100ms batches and unchanged current-source control. Existing
shared-CoreLib image controls were unstable; this prospective method choice is
recorded before measurement, without rerunning or replacing old observations.

| Complete script | Elapsed change, blocks0/1 | Unchanged-control change |
| --- | ---: | ---: |
| CSV/helpers |+0.10% /–2.22%|–0.79% /–0.50%|
| Text/helpers |–8.35% /+0.64%|+1.77% /+2.70%|
| JSON/helpers |+0.91% /–1.50%|+2.62% /–2.04%|
| Helper report |–0.65% /–0.62%|–0.02% /+1.17%|
| Search |–5.00% /+3.77%|–6.77% /+0.91%|

These elapsed observations include I/O and are mixed, especially search. CSV's
extra pre-B113 process is4.52%/0.88% faster than the candidate in the two blocks,
without its own unchanged legacy control in this first campaign.

The worker protocol reports no CPU or allocation metric. Fill only that material
CSV CPU gap with two bounded CSV blocks: current/candidate/current-control and
pre-B113/legacy-control processes,4s warmup and9×~200ms batches. External Linux
TGID user+system ticks are sampled before command and after response; weighted
means across all9 batches handle clock-tick granularity. This includes runtime,
JIT and response work, not guest-only CPU, and adds no production telemetry.

| CSV CPU diagnostic | Block0 | Block1 |
| --- | ---: | ---: |
| Candidate versus current baseline |–3.36%|+0.99%|
| Current unchanged control versus current baseline |–1.03%|0.00%|
| Candidate versus pre-B113 |0.00%|+4.40%|
| Legacy unchanged control versus pre-B113 |–0.22%|–0.68%|

The older regression is not consistently reproduced at its original magnitude;
it is not disproved, and the second block still shows regression. No retiming,
threshold relaxation or further selector-specific tuning. Keep the prior adverse
conditions and change the next work toward a broader demonstrated material cost.

## Boundary and validation

Same quiet F4asv7/.NET11 RC3, ordinary GC/tiering, identical auxiliary DLLs with
only core differences, exact hashes/source/fixture/stdout/files checks. Warm
batch includes fixture reset, actual mediated I/O, full output checks and previous
result disposal. Last result and final compiled/process/fixture teardown remain
outside batch timing; every worker is joined and fixtures removed. Compilation
and startup excluded. Compiler source unchanged; B115 external baseline reused.

Owned services172.335s and64.537s exit0; before/after quiet gates, all ten elapsed
jobs and both CPU jobs complete, all workers joined. Units inactive, lease free.
Release focused148 internal/121 public per actual.NET11/.NET10 pass; all21
semantic goldens independently pass CPython3.13.2. Coverage includes builtin
aliases/rebinding, closed versus broader lengths, invalid operands, real protocol
suspension/cancellation, first admission refusal after work, retained storage,
cleanup and budget reuse. No architecture-only counter tests.

Full Windows/Ubuntu/Stress/installed consumers are batched with the next retained
runtime boundary. Cached41472979014 proves B113 only, not this source delta.
Publication and adapter release remain postponed; performance objective open.
