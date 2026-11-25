# B1712 Shared dictionary value slots and packed CSV fields

The private prototype stores native dictionary values in the existing managed
executable slots. Keys no longer carry a separate integer value. Sharing
immutable keys leaves value storage in place. Shallow copies first publish each
canonical object in its original slot. Defaults and annotations retain their
adoptable storage. The private `DictReader` row uses its final slot array
from parsing onward, with ordinary `csv.reader` retaining object arrays.
Proved adjacent string comparisons and decimal `int()` calls can read a
packed field without constructing its Python string.

The reference is `6934aafb9`. Both 32-file frozen images differ only in the
core DLL and independently verify six complete CPython goldens. The Azure
F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25, 2026 UTC.
Two processes per source warm for eight seconds and supply seven rotated
timed batches each. Ranges span all four pairs of process medians.
Warm time includes mediated RAM-backed I/O, output and file verification,
and result disposal. Wider process CPU also covers reset and request
framing. The plain reader is an allocation and semantic control.

| Workload and boundary | Elapsed change | Wider CPU change | Elapsed worker spread reference / candidate | Local sync allocation change |
| --- | ---: | ---: | ---: | ---: |
| Shipment, warm sync | −3.64 to +4.30% | −4.07 to +3.74% | 6.61 / 1.53% | −29.94% |
| Shipment, warm async | −3.86 to +2.59% | −3.52 to +2.79% | 1.57 / 5.06% | |
| File-fed map, warm async | −9.00 to −1.83% | −6.68 to −1.47% | 2.29 / 5.46% | −41.83% |
| Quoted invoice, warm | +0.57 to +4.60% | +1.69 to +5.15% | 1.92 / 2.05% | −27.21% |
| Exposed-header invoice, warm | +1.27 to +8.89% | +1.08 to +7.64% | 0.47 / 7.02% | −27.21% |
| Nonlocal text summary, warm | +4.16 to +9.71% | +4.51 to +8.53% | 4.04 / 1.24% | +0.01% |

Managed array allocation confirms that key/value elements move from 40/8
to 32/16 bytes. Private dictionaries keep the same combined element width.
Shared layouts spend eight additional physical bytes per value. In the
local diagnostic, admitted peaks fall by about 64 KiB for the two shipment
variants and 128 KiB for both invoice variants. Cleanup restores every
allowance. Plain reader allocation and admitted peaks match.

The separately instrumented image confirms both hosting modes take the same
packed routes. Shipment produces 8,192 packed fields and uses 4,094 direct
comparisons and 2,047 direct decimal reads. Quoted and exposed-header
invoices each have 21,504 packed fields and 16,380 direct decimal reads.
The text control takes none of these routes. This instrumentation proves
route coverage and carries no timing evidence.

Keep the source private while isolating CPU costs. File-fed async execution
uses less CPU, and all three dictionary CSV workloads substantially reduce CLR
allocation. Both invoice variants take longer. The text control slows with
essentially unchanged allocation. The current comparison combines the
storage layout and byte-consumer entry changes, so it cannot attribute
those regressions to either part in isolation.

Windows .NET 11 and Ubuntu .NET 10 each pass 1,090 white-box focused Release
checks. Windows passes 2,523 public checks and a subsequent 46-case CSV
check with the added CPython alias golden. Ubuntu passes 2,524 focused
public checks with that golden included. Admission refusal, awaited header
cleanup, independent retained rows, shallow-copy identity and reentrant
header changes are covered. Early build and budget-expectation failures
remain in the procedures. All 168 timed batches and both quiet checks
pass. Workers join and fixture removal and the archive verify under the
lease.

[Exact source and images](identity.json) · [Timings](quiet-summary.json) ·
[Allocations and admission](allocation-summary.json) · [Routes](route-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json) ·
[Procedures and patches](procedures.tar.xz).
