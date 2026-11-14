# Bulk cased UTF8 buffer: withdrawn prototype

2026-11-14, R343/B713. Compare qualified `2f606519` / frozen `53becdb6`
against prototype `a9d54f99`, with identical complete scripts, inputs, goldens,
worker and optional 0.4 adapter. The prototype cases a whole line into an owned
reusable UTF8 buffer, then sizes/scans final words instead of storing ranges.
It removes range growth/copy/clear machinery, but adds complete buffer scans.

| Complete workload | Reference ms | Prototype ms | Elapsed change | Wider CPU change |
| --- | --- | --- | --- | --- |
| Captured text, warm | 1.394–1.413 | 1.531–1.580 | +8.35..+13.41% | +8.59..+12.54% |
| Captured text, compile-to-result | 1.518–1.546 | 1.666–1.707 | +7.72..+12.45% | +7.83..+12.23% |
| Direct text, warm | 0.916–0.979 | 1.101–1.122 | +12.48..+22.58% | +10.85..+21.77% |
| Unchanged fulfillment control, warm | See raw summary | See raw summary | −0.10..+0.48% | −0.88..+1.11% |

Changes compare all fresh-process source pairings, positive means slower.
Two processes per source/job, 12s full-request warmups and five rotating 1s batches
on quiet Azure F4as_v7/Ubuntu24.04.4/.NET11. Captured text uses 3072 lines/35,840B;
direct text uses the same input with the count-only helper explicitly inlined.
Its reference spread is 6.92%, so the precise adverse magnitude is uncertain;
all pairings remain adverse. The CSV control is nearly flat. Separate four-process
desktop allocation checks save only 88B/job on either text shape (0.016/0.020%);
fulfillment is effectively flat. No material allocation reduction is established.

Warm clocks include mediated RAM-fixture I/O, output/file checks and prior-result
cleanup. Compile-to-result adds compilation/script disposal. Startup, protocol/reset
and final cache shutdown are excluded; wider CPU includes protocol/reset. There is
no CPython timing, tail-latency, physical-heap or whole-runtime claim here.

**Decision: withdraw this version.** The extra scans outweigh range storage removal.
All 581 production input bytes are restored exactly to the fully qualified R341
snapshot. No prototype mechanism or modified internal architecture fixture remains.
Four independent CPython3.13.2 semantic matrices and a retained-result/shared-budget
test remain useful public regressions. A materially different pipeline can be
reconsidered if it removes the extra work; unchanged retiming is not justified.

Both corrected prototype and restored baseline pass 103 internal/484 public cases
and all three original 64MiB cancellation/rollback cases per actual11/10. A genuine
prototype-only cancellation bug initially retained 64MiB scratch; its failure,
sources/binary and correction survive. The baseline's original temporary admission
already preserves that guarantee. Six range-capacity fixture failures and two
obsolete inventory-presence assertions also remain in the evidence. Assertions,
stress scales and timeouts were not relaxed. A forced nonincremental Release
solution rebuild precedes restored checks. The existing R341 full Windows/Ubuntu,
stress and installed-consumer delivery remains the production qualification;
unchanged qualification is not repeated for a withdrawn runtime.

Initial disk preflight refused before a service/guest started. Exact Git-committed
archive replicas were subsequently retired under a fresh idle lease; canonical
evidence, current images and 254/252MiB staging/archive floors remain intact.
B713 completes in298.45s under360s ownership; all workers join, RAM fixtures are
removed and the raw archive is verified under the lease. Terminal state is cached.

[Timings](summary.json), [allocation](allocation-summary.json),
[decision/restored checks](decision.json), [raw observations](observations.tgz),
[sources, binaries, initial failures, corrections and tools](source-and-checks.tar.xz).
The large archive omits duplicated auxiliary images/payload; complete image hashes
and worker identity remain recorded. Publication remains postponed.
