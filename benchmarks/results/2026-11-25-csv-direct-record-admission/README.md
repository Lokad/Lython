# B1719 Admit completed CSV row storage directly

Two fixed-size reservations use the governor's existing reserve, commit
and rollback operations. The parser admits complete row capacity before
construction. Native dictionary adoption separately admits its shell and
rechecks live header identity after the admission callback. A mismatch
refunds the shell and leaves the input owned. Both paths previously
allocated a managed reservation holder.

The reference is `36a4c681f`, including skipped-row retirement before refund
callbacks. The candidate patch and exact image hashes are archived below.
The two 32-file images differ only in the core DLL and both verify all five
CPython-derived goldens. Compilation allocation and retained admission
match. A separate local diagnostic measured these warm allocation totals.

| Workload | Reference CLR bytes | Candidate CLR bytes | Reduction | Peak admitted bytes on both |
| --- | ---: | ---: | ---: | ---: |
| Quoted CSV invoice | 3,281,366 | 3,019,222 | 7.99% | 9,040,266 |
| Shipment callback report | 1,311,688 | 1,180,616 | 9.99% | 2,817,644 |
| File-fed CSV quantity map | 939,416 | 808,512 | 13.93% | 2,270,071 |
| Plain CSV reader | 2,984,998 | 2,854,510 | 4.37% | 8,252,450 |
| Nonlocal Unicode text control | 322,856 | 322,856 | 0.00% | 603,795 |

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25,
2026 UTC. Each image has two workers. Each worker warms for five full-request
seconds in the first block and seven in confirmation, then contributes
seven rotated batches per phase. Ranges span the four worker-median
pairings. Warm elapsed includes mediated RAM-backed I/O, result verification
and cleanup. Wider CPU also includes fixture reset and request framing.

## First block

| Workload and phase | Elapsed change | Wider CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| File-fed CSV quantity map, warm async | −6.54 to −1.44% | −5.13 to −1.70% | 1.91% / 3.48% |
| Quoted CSV invoice, warm sync | −4.77 to −0.03% | −3.27 to +0.76% | 4.02% / 0.92% |
| Shipment callback report, warm async | −3.68 to −1.49% | −3.78 to −1.64% | 1.54% / 0.73% |
| Nonlocal Unicode text control, warm sync | +2.68 to +9.26% | +1.36 to +8.01% | 2.63% / 3.68% |
| Plain CSV reader, warm sync | −3.46 to −0.16% | −4.52 to −1.28% | 2.15% / 1.24% |
| File-fed CSV quantity map, warm sync | −6.07 to +0.56% | −4.78 to +0.84% | 2.36% / 4.59% |
| Quoted CSV invoice, warm async | −1.73 to +1.79% | −2.60 to +1.35% | 1.34% / 2.22% |

## Reordered confirmation

| Workload and phase | Elapsed change | Wider CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Nonlocal Unicode text control, warm sync | −5.47 to +10.10% | −6.55 to +8.66% | 5.48% / 10.43% |
| Plain CSV reader, warm sync | −2.89 to +1.60% | −4.20 to +1.22% | 1.90% / 2.67% |
| File-fed CSV quantity map, warm async | −8.77 to −5.50% | −8.38 to −5.30% | 2.90% / 0.66% |

The two CSV construction paths retain direct admission. Quoted invoice
allocates 262,144 fewer managed bytes per invocation. File-fed map allocates
130,904 fewer bytes and uses 1.70–5.13% less warm async CPU in the first block
and 5.30–8.38% less in confirmation. Plain reader and both quoted invoice
CPU phases remain inconclusive across the recorded conditions. The unchanged
text control uses 1.36–8.01% more CPU in the first block. Its confirmation
spans −6.55 to +8.66%, with candidate elapsed spread of 10.43%. The source of
that control movement remains unresolved.

Windows .NET 11 and Ubuntu .NET 10 each passed 560 focused Release checks
covering CSV semantics, terminal refusal, header reentry, alias funding and
cleanup. All 280 timed batches passed the golden checks. Both blocks verified
the persistent archives after workers joined and guest fixtures were removed.

[Exact identity](identity.json) · [Runtime allocation](allocation-summary.json) ·
[Compiler allocation](compile-allocation-summary.json) ·
[First timing](quiet-summary.json) · [First observations](quiet-observations.tar.xz) ·
[First terminal receipt](quiet-terminal-status.json) · [Procedures and patch](procedures.tar.xz) ·
[Confirmation timing](confirmation/quiet-summary.json) ·
[Confirmation observations](confirmation/quiet-observations.tar.xz) ·
[Confirmation terminal receipt](confirmation/quiet-terminal-status.json) ·
[Confirmation procedures](confirmation/procedures.tar.xz).
