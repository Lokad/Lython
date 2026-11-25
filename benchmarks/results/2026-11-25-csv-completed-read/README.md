# B1704 Inline completed CSV line reads

The candidate keeps completed line reads in the existing synchronous
prefix. A pending refill, failed read, or deferred release uses the
awaited continuation. The active read still funds borrowed decoder
windows through record admission. Row bytes and captured aliases retain
their existing owners.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25,
2026 UTC. The frozen `701f0b1e5` reference and private candidate differ
only in the core DLL. Each source has two processes, five seconds of
warmup per process, and seven rotated batches with a 0.59-second target.
Ranges span all pairings of process medians. Warm elapsed includes
applicable mediated RAM-backed I/O, output and file verification and
previous-result disposal. Wider process CPU includes fixture reset and
framing. Compilation, startup and final teardown stay outside both clocks.

| Complete script and boundary | Elapsed change | Wider process CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Typed Unicode meter, warm async | −6.67 to +0.86% | −6.67 to +2.00% | 3.42 / 4.49% |
| Explicit-float meter, warm async | −3.93 to −0.68% | −4.08 to +0.13% | 3.34 / 0.03% |
| Quoted invoice, warm sync | −4.29 to −0.69% | −4.80 to −0.47% | 2.60 / 1.13% |
| File-fed map, warm async | −6.98 to −1.73% | −6.46 to −1.18% | 1.17 / 4.43% |
| Text summary control, warm async | +3.95 to +9.24% | +3.92 to +10.15% | 5.03 / 0.06% |
| Typed Unicode meter, warm sync | −1.28 to +0.91% | −0.49 to +2.48% | 0.88 / 1.33% |

An instrumented build enters the inline path for all 5,122 invoice,
2,050 file-fed map and 4,098 meter reads. The text control exercises
zero CSV reads. Both hosting modes produce the same counts. Managed
allocation is effectively flat and all admitted peaks and restored
allowances match. In the [narrow confirmation](confirmation/README.md),
file-fed map again uses less CPU and elapsed time. The invoice effect
is smaller. The text control moves in the opposite direction, with
high worker variation across blocks.

Windows .NET 11 and Ubuntu .NET 10 each pass 900 focused checks plus
17 borrowed-reader checks on the same runtime. The held-close test
requests release during admission of a buffered row, observes retained
window funding, verifies the asynchronous read returns a pending value,
and releases the host gate before verifying the retained row. Both
frozen images match five CPython goldens. All 168 timed batches, both
quiet gates, worker joins, fixture removal and archive verification
complete.

[Source and images](identity.json) · [Routes](routes.json) ·
[Allocation and admission](allocation-summary.json) · [Timings](quiet-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json) ·
[Builds and procedures](procedures.tar.xz).
