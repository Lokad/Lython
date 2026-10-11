# Typed function-body plans: rejected short experiment

The isolated prototype **is rejected**. Method time changes **+1.36% / -8.68%**,
with retained spreads **1.37% / 1.26%**. The first pass does not improve, and
keyword calls regress **4.33% / 2.06%** beyond their retained bounds in both passes.
The declared rule requires method gains beyond max(2%, spread) in both passes,
no repeated material control regression and no repeated allocation growth above
0.5%. Every control remains visible; no observations are discarded or recollected.

| Complete case | First pass change | Second pass change |
|---|---:|---:|
| calls.keyword.medium | +4.33% | +2.06% |
| calls.method.medium | +1.36% | -8.68% |
| calls.positional.medium | -0.37% | +7.56% |
| control.empty.control | -2.42% | -1.92% |
| lists.stable-sort.medium | -2.78% | -3.89% |
| loops.integer.large | +0.71% | -4.25% |
| strings.pipeline-ascii.medium | -17.52% | +2.04% |

Fourteen paired microbenchmarks take **8.039–8.057 seconds including cleanup**,
under **30-second process-group caps**. Separate ordinary allocation and native
groups each take **4.034–4.039 seconds**. Workers retain ordinary tiering/PGO/GC;
the controller alone disables tiering. This is short diagnostic evidence without
confidence intervals or milestone qualification. No ten-minute timing lane ran.

## Execution model and compatibility

Immutable plans compile blocks, if/for/while control and common expressions inside
the actual PyFunction. The existing callable/factory, argument mirroring, actual
invocation contexts, namespace/class cells, before-child guards, compound-depth
spans/unwind, logical funding and definition/default/decorator/annotation pipelines
remain. Separate awaited operations preserve genuine suspension and cancellation.
Calls retain their original admission before argument expansion; complex operations
retain their original handlers. No new executable frame, extra null-span admission,
context pooling or fee discount is introduced. Existing module IR stays unchanged.

The weak cache uses original definition-body identity. Both actual factories reuse
those source lists. Lowered lambdas use the separate unchanged LambdaFunction;
the preliminary concern about fresh lambda body-list cache misses was incorrect.
First plan construction and retained compilation costs remain real, with startup
and source-artifact qualification still separate. The compiler also affects nested
lowered functions, so all module-call, loop, empty, sort/output and pipeline controls
are retained. Passing this prototype does not complete broader body compilation.

Both clean producers pass **9,503 Windows Debug and 9,503 Linux Release tests**
(1,645 white-box + 7,858 public), with matching Probe builds and **seven complete
canonical CPython correctness comparisons each**, before timing. The same test
tree includes 37 new original-production cases covering exact fuel/funding,
definitions, defaults/annotations/type scopes, live class cells, escaped closures
and generators, real host suspension/reentry/cancellation and budget/depth cleanup.

## Allocation, native code and cleanup

Complete method-job managed allocation changes by
**3.166% saved / 0.00314% growth**.
There is no repeated growth above 0.5% in any control. Allocation does not override
the timing decision; these are process-wide managed totals, not exact guest-type
bytes or a change in logical funding.

Native inspection retains **230 baseline and 274 candidate listings**, including
**50 candidate body-plan listings**, with every emitted tier, inline summary and
call target. Code presence/size does not establish a CPU improvement or a projected
gain. The [complete evidence](evidence.json) retains all timing/allocation rows and
native inventories. Raw artifacts, receipts, journals and producer/helper hashes
remain archived privately; the frozen diagnostic helper is reused without rebuild.

All **20 owned services are terminal**, all **66 recorded PIDs are absent**, both
sources are clean, inputs rehash and the shared lease is available. The rejected
source remains isolated at `069094055bd6ff5e6829c57a5c29ebff161460e3`; delivered production stays
`f006f62cad79f63bc1b2564494ce742b6fe74000`. The remaining compiler/design work, original
pipeline cause, C05, external regex integration and milestone qualification stay
open. This experiment is not repeated to obtain a favorable result.
