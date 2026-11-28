# B1917 Batch converted record loops

The existing native dictionary loop can run ordered shared-record field
reads and a converted update across a built-in list or tuple. It borrows
the admitted lookup cache and reads the actual row at each iteration.
This removes separate dispatch for the loop, field bindings and
converted update on eligible rows. A changed field shape, live converter
or unfunded store resumes at the original instruction. Earlier local
bindings are already visible. The normal path retains protocol order,
admission, suspension and exception handling.

The compiler comparison marks one loop in the dense document and one
in the mixed-row case. The other 14 tapes match. All 108 compile budget
levels, first refusal results and emitted layouts match. Both measured
32-file images match four CPython 3.13.16 output and file goldens. Only
the core DLL differs from `a5286813b`. Median managed allocation and
peak admission match for dense JSON in both APIs, with a 2,592,718 B
run peak. The controls differ by at most 224 managed bytes. Their
peaks match. All borrowed allowances return to baseline.

## Local timing

On November 28, 2026, Ubuntu 24.04 WSL uses .NET 11 RC3 pinned to one
logical CPU. Two workers per image warm at least five seconds each
and contribute seven rotated batches per case. Ranges span all
worker-median pairings. Warm elapsed includes mediated I/O, output
and file verification, and previous-result disposal. Process CPU
also includes fixture reset and request framing. Compilation and
startup are outside these clocks.

| Workload | Mode | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Dense nested JSON | Sync | −5.77% to −1.90% | −5.30% to +0.65% |
| Quoted CSV control | Sync | −1.75% to −0.67% | −4.18% to +1.43% |
| Sparse JSON Lines control | Async | −3.09% to −0.33% | −1.97% to +0.08% |
| Dense nested JSON | Async | −4.94% to +4.82% | −8.48% to −0.46% |
| Text histogram control | Async | +0.48% to +4.64% | −1.26% to +3.19% |
| Sparse JSON Lines control | Sync | −6.87% to −1.36% | −5.95% to −1.82% |

All 168 batches verify. The first sparse sync and text async results
move even though their tapes match. The same-image repeat reverses
worker placement and tests both dense modes and those two controls.

| Workload | Mode | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Text histogram control | Async | −8.94% to +14.91% | −13.10% to +19.78% |
| Dense nested JSON | Async | −11.72% to −0.57% | −10.67% to +2.66% |
| Sparse JSON Lines control | Sync | −1.43% to +2.58% | −1.10% to 0.00% |
| Dense nested JSON | Sync | −7.76% to −5.67% | −5.42% to −2.97% |

All 112 repeat batches verify. Dense sync CPU worker spreads are
under 1.4% and the sparse control includes zero. The text reference
spread is 27.99% in CPU, leaving its timing inconclusive. Retain the
segment for the removed dispatch and the dense sync result. Keep both
async observations and the control variation visible.

## Semantics

A mixed list starts with shared JSON records, then reaches a dynamic
row whose second field suspends on a host read. After another exact
row, a missing field leaves its earlier binding visible in the handler.
Both Lython APIs and CPython give the same result and field order.
Lython also checks the single async close and budget reuse. The first
tape harness run counted only the explicit workloads. The validated
count includes the compiler probe's 11 additional fixture programs.
Focused Release checks pass 270 public and 25 white-box cases per
target on Windows .NET 10 and Ubuntu WSL .NET 11. Windows .NET 11
also passes that selection with the original fixture path and all
three converted-update checks with the final relative path.

[Tape changes](tape-summary.json) · [Image identities](identity.json) ·
[Allocation and admission](allocation-summary.json) · [First timing](summary.json) ·
[First observations](observations.tar.xz) · [Repeat timing](repeat/summary.json) ·
[Repeat observations](repeat/observations.tar.xz) · [Source and checks](procedures.tar.xz).
