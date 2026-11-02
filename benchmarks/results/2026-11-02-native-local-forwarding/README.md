# Native value forwarding — B130

2026-11-02; baseline7da7335a/corea3a53e2e, final core12342de99e6515182980cb286deecc59605c312bfc5814900c25d46e2888a30f.
Retained compiler correction; not a CPython refresh or full IR rewrite.

The [same-core source experiment](../2026-11-02-helper-source-styles/README.md)
demonstrated substantial costs from ordinary named intermediates. Sixteen compiler
lines let the existing native plan forward an adjacent StoreLocal/LoadLocal value.
A lazy set rejects later reads or reassignments that need the actual binding.
Captured/exposed locals remain excluded. Original executable instructions and
local-name metadata are untouched; ordinary execution still materializes bindings
when whole-expression native proof fails. Runtime guard/evaluator unchanged.

No new opcode, statement interpreter, runtime scratch, owner, pool, option or
public API. All native guards still finish before work, with no retry after
admission or effects. Current cancellation and ownership behavior is preserved.
Executable preparation retains its documented compiler-phase cancellation
boundaries; this change adds no in-pass interruption guarantee.

## Complete-script result

Quiet F4as_v7 Ubuntu24.04.4/.NET11RC3, ordinary workstation GC/tiering. Existing
worker protocol, independent original/candidate/unchanged-control processes,
four-second warmup/image, nine interleaved ~200ms batches, complete stdout/files.

| Named-helper case | Elapsed change | Process CPU | Unchanged elapsed / CPU |
| --- | ---: | ---: | ---: |
| CSV warm, block0 |−4.88%|−6.08%|+2.94% /+3.17%|
| CSV warm, block1 |−5.91%|−7.14%|−0.46% /+0.82%|
| Text warm, block0 |−10.69%|−15.00%|−1.19% /−2.94%|
| Text warm, block1 |−9.72%|−10.30%|−1.89% /+0.63%|
| CSV fresh compile-to-result |−4.62%|−17.37%|+0.72% /−10.76%|
| Text fresh compile-to-result |−4.43%|−18.09%|+0.45% /−10.22%|

Against controls, warm CSV elapsed falls about5–8%, text8–10%; fresh elapsed about5%
for both. Warm process CPU also improves. Fresh CPU controls move substantially;
retain those clocks without assigning a precise CPU fraction. Independent B129
and B130 medians cannot be subtracted as causal results.

Compact text control: candidate elapsed+2.79%, unchanged+6.01%; CPU+0.78%/+6.58%.
Its runtime lowering is unchanged, so no unaffected-script improvement is claimed.
Range: elapsed−0.63% versus−0.96%, CPU−2.09% versus−2.62%; effectively tracks control.
The earlier B127 range regression remains open. No universal performance claim.

Warm excludes compilation/startup; fresh includes each compilation and its disposal.
Both include fixture reset/staging, efficient mediated I/O, full output/file checks
and previous-result cleanup. Last result/cached program/process/fixture teardown
is outside batches and joined. CPU is external Linux TGID user+system through
response, including runtime/JIT/response, not guest-only CPU. No isolated compilation
speedup is claimed.

## Storage and semantic acceptance

Separate complete-job diagnostic:192 warmups/image,9x32 public invocations,
isolated images sharing CoreLib/GC, precise process allocation including workers.
Its clocks are diagnostic, not the timing decision above.

| Warm complete script | Baseline bytes/run | Candidate | Change | Unchanged control |
| --- | ---: | ---: | ---: | ---: |
| Named CSV/helpers |8,493,428|5,905,698|−30.47%|+0.0176%|
| Named text/helpers |5,239,171|3,420,481|−34.71%|0.0000%|
| Compact text control |3,420,481|3,420,460|−0.0006%|0.0000%|

All identities/goldens/quiet gates/hash witnesses, reusable balances and final
zero balances pass. Timing152.029s/allocation37.082s; both owned services exit0,
inactive, shared lease free. Current census confirms all four named variants use
the existing three-node native plans; instruction counts and metadata stay intact.

Focused Release192 internal/194 public per actual.NET11/10 PASS; .NET11 public
selection192 plus two existing compiler-cancellation cases. Seven added goldens
independently match isolated CPython3.13.2. Coverage includes parameter reuse and
reassignment, live builtin aliases, unknown protocol/error order, retained aliases,
and an indirect locals() returned by a property: dynamic proof misses must keep
the original normalized binding. The existing real async cancellation regression
now contains a named result; suspension/cleanup/funding assertions remain unchanged.
No architecture-only tests or containment/timeouts weakened.

Full Windows/Ubuntu, original stress and installed-package delivery will batch
this change with retained B127 iteration. Latest cached full GREEN41519435155
describes90785737 only until that new checkpoint completes. Publication postponed;
regex work stays independent. Remaining nonadjacent/reused locals and broader
operand/activation construction remain separate work, with range debt explicit.
Raw observations, source pair, census, scripts, goldens and focused logs are kept.

## Delivery

Full qualification [41535109342](delivery-ci-status.json) PASS at exact060a8359:
Windows/Ubuntu ordinary Release.NET11/10, original Stress both targets and installed
package consumers. It also covers shared iteration7da7335a; later compiler
fallthrough and operand-layout work lie outside this exact-source proof.
