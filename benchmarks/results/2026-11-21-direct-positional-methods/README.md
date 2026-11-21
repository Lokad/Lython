# B1294 direct positional receiver binding

The executable caller already holds the selected method, receiver and
evaluated arguments. A complete positional supply now writes straight
into the final managed slots, with the receiver first. This removes
the generic binder from that path in both executable drivers. Native
callback entry shares the same complete-supply proof. Omitted and named
arguments, variadic calls and live defaults keep the general binding
path. A suspended body transfers its live slots to the existing owned
continuation.

Reference `8ff3a3371` and prototype `ec3e76b89` ran on the quiet Azure
F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on November 21, 2026 UTC.
Two independently warmed workers per revision contribute seven rotated
batches per worker. Each range covers the four pairings of worker
medians. Warm elapsed includes execution, mediated RAM-backed I/O,
golden checks and prior-result disposal. Process CPU covers the wider
request. Compilation, startup and final teardown have separate clocks.

| Complete warm workload | Elapsed change | Wider CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Fully supplied calls through an attribute and stored method | −9.45% to −2.28% | −10.37% to −1.68% | 4.34% / 3.43% |
| CSV invoice with policy methods, 4,096 data rows / 127,031 B | −0.01% to +0.95% | −1.75% to +0.22% | 0.19% / 0.77% |
| Fully supplied free function | −3.18% to +1.52% | −4.12% to +1.00% | 3.26% / 1.55% |

Retain the direct fill. The method script improves in both clocks.
The invoice is effectively flat and the free-function control spans
zero. The local diagnostic initially put the method invoice 0.62–9.80%
slower while the unchanged invoice ran 2.58–10.33% faster. The larger
method-invoice slowdown did not recur on the VM. The unchanged invoice
was measured locally. The archive preserves
the local measurements and their original conditions. On all four
local workloads the per-invocation allocation and accounted peaks are
equal, and every execution returns its allowance to baseline.

Windows .NET 11 and Ubuntu .NET 10 each pass 269 white-box and 1,557
public focused Release checks. Coverage includes receiver selection
before argument effects, keyword/default binding, host suspension,
refusal and cleanup. Both complete 32-file images match four CPython
goldens. The three-lane quiet catalog verifies separately with those
same images. All 84 timed batches and both machine gates pass.
Workers join, fixtures are removed and the archive verifies under
the lease. The wider factory and CSV performance objectives remain open.

[Timing summary](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source, checks and procedures](checks-and-procedures.tar.xz).
