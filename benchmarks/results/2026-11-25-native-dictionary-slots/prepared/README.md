# B1712b Prepared CSV field consumers

This private source retains one dictionary value layout and transfers
the final `DictReader` slots from the parser. The compiler marks an
adjacent exact string comparison or positional call in the existing
instruction tape. A confirmed builtin `int()` consumes the decimal
parsed at the field read. Rebound callees and other inputs retain
the canonical Python value and ordinary error behavior.

The reference is `6934aafb9`. The two frozen 32-file images differ only
in the core DLL and each verify six complete CPython 3.13.2 goldens.
The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25, 2026 UTC.
Two workers per source each contribute seven rotated timed batches after
warmup. Ranges span all four pairings of worker medians. Warm elapsed
includes mediated RAM-backed I/O, result verification and cleanup.
Wider CPU includes fixture reset and request framing.

| Workload and boundary | Elapsed change | Wider CPU change | Elapsed spread reference / candidate | Local sync CLR allocation change |
| --- | ---: | ---: | ---: | ---: |
| Shipment, warm sync | −4.97 to −1.11% | −3.13 to +0.48% | 2.97 / 1.07% | −29.94% |
| Shipment, warm async | −5.71 to −0.40% | −6.87 to +1.55% | 1.45 / 4.12% | |
| File-fed map, warm async | −11.47 to −7.85% | −11.22 to −6.77% | 2.19 / 1.86% | −41.83% |
| Quoted invoice, warm | −2.38 to −0.57% | −2.11 to +0.60% | 0.01 / 1.84% | −27.21% |
| Exposed-header invoice, warm | −5.20 to −1.28% | −3.66 to −1.55% | 2.68 / 1.42% | −27.23% |
| Nonlocal text summary, warm | +6.33 to +10.18% | +6.38 to +11.98% | 0.28 / 3.33% | −0.08% |

File-fed CPU falls again and the two invoice regressions from B1712 do
not recur. Shipment elapsed improves in this block and CPU spans zero.
The text regression recurs despite essentially unchanged allocation
and zero prepared record routes. This comparison cannot distinguish
general dictionary storage from instruction dispatch or JIT code shape.
Keep the source private pending focused attribution on that control.

The separate instrumented image confirms each prepared integer is
consumed once in both hosting modes. Shipment uses 4,094 packed
comparisons and 2,047 prepared integer conversions. Each invoice uses
16,380 prepared conversions. Plain `csv.reader` and the text summary
take no prepared path. Plain reader allocation and sampled peak
admission match closely. Dictionary CSV peaks fall by approximately
64 KiB for shipment and file-fed map and 128 KiB for the invoices.

Windows .NET 11 and Ubuntu .NET 10 each pass 5,407 public and 1,786
white-box focused Release checks covering CSV, dictionaries, calls,
annotations, arithmetic, regex, ownership and terminal refusal.
The route and allocation checks also verify allowance recovery after
execution results and compiled owners are disposed. All 168 timed
batches and both quiet gates passed. Workers joined and the archive
verified after fixture removal under the lease. The first build logs
and the local launch-log collision are retained. The VM journal
records the successful launch and complete collection.

[Exact images](identity.json) · [Timing](quiet-summary.json) ·
[Allocation](allocation-summary.json) · [Routes](route-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json) ·
[Procedures and patches](procedures.tar.xz).
