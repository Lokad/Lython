# Complete scripts through synchronous and asynchronous hosting

R388/B775, 2026-11-15 UTC, unchanged **810379be / frozen 8fbfa3f0**. Same executable
code and existing worker, comparing `warm` (`Run`) with `warm-async` (awaited
`RunAsync`) using completed host I/O. No runtime, harness or provider change.

| Complete workload | Run | RunAsync | Async elapsed overhead | Wider CPU overhead |
| --- | ---: | ---: | ---: | ---: |
| CSV fulfillment, 4,096 records | 7.997–8.092 ms | 8.334–8.597 ms | +2.99–7.50% | +1.67–6.28% |
| Captured text helper/counter, 3,072 lines | 1.201–1.218 ms | 1.334–1.351 ms | +9.53–12.55% | +15.15–17.57% |
| Protected file search, stop after 1,025 lines | 0.266–0.274 ms | 0.283–0.284 ms | +3.22–6.77% | +17.92–24.71% |

Two independent processes per API, five rotating rounds; ranges span all four
process-median pairings, not confidence intervals. Captured text has the clearest
controls: sync/async process spreads 1.49/1.26%, maximum IQR/median 2.38%.
CSV async spread is 3.06%; file sync spread is 3.09%. Preserve those uncertainties.
The preceding [local identity-call diagnostic](../2026-11-15-hosting-parity/README.md)
and its original unstable warmup remain separate.

Quiet Azure F4as_v7, Ubuntu 24.04.4, four EPYC 9V45 vCPUs, 16 GiB/no swap, .NET 11
RC3, ordinary workstation GC/tiering. Five-second full-request warmup per worker,
twelve seconds for captured text, 0.5 s request batches. All three jobs finish in
134.756 s. Elapsed includes public invocation, mediated RAM-file I/O, prior-result
disposal and full stdout/file verification. It excludes compilation, startup,
fixture reset, protocol and final cache teardown. Wider process CPU includes
reset/protocol; it cannot isolate guest execution or explain the elapsed gap.
No genuinely pending-I/O latency, heap/RSS, tail latency or CPython measurement.

Decision: asynchronous hosting has a modest cost on these completed-I/O scripts.
Captured text is the next bounded attribution target: distinguish per-line async
file/iteration work, builtin completion/disposal and invocation scheduling.
Source already shares the completed-instruction dispatcher, executable functions
and native helper prefixes. Startup workers are already reused and released at
actual suspension. These results do not justify another execution path or removal
of cancellation, admission, stack protection or funded cleanup.

All exact image/catalog/source/output identities, goldens, quiet-before/after,
shared-balance, Closed/join and archive gates pass. RAM fixtures are gone; no users
remain. Raw evidence was archived and hash-verified persistently under the lease.
Current images, catalogs and toolchains remain; no new image/catalog upload or
retirement. Existing 254/252 MiB floors are unchanged; final free space is
266,100,736 B. Before another staging, restore its floor using only proved archived
working copies; do not relax thresholds or delete sole evidence.

Original preparation failure is preserved: an incorrect catalog filename failed
before any worker or collection started. The correction verified every staged
file and image under the lease, then linked the existing immutable `catalog.json`.
No collection was restarted. This runtime's [full qualification](../2026-11-15-function-slot-binding/delivery/README.md)
is already complete; no unchanged qualification was repeated. Publication remains held.

[All medians, IQRs and wider CPU](summary.json), [source/decision](decision.json),
[raw observations, gates and collection procedures](observations.tgz),
[local preparation and original failure](local-preparation.tar.xz),
[archive proof](archive-receipt.json), [terminal state](terminal-status.json).
