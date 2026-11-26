# B1794 Use live row count to avoid wide starmap argument growth

The [first candidate](../2026-11-26-starmap-argument-buffer/README.md)
shares short operands and the admitted call accumulator across
synchronous and asynchronous starmap. Its five-item rows start with
four argument slots and must grow every row. The refinement on
`f69105573` uses the exact builtin sequence's live count as a capacity
hint at the fourth item. The live cursor still supplies every argument
in order. If admission changes the row, the same accumulator can grow
or invoke with fewer values.

## Allocation and admission

Windows .NET 11 diagnostics use the same scripts and worker tooling.
Medians below use `RunAsync` with a cooperating comparison host. They
cover complete warm runs with applicable I/O, result checks and cleanup.

| Workload | Production | First candidate | Capacity hint |
| --- | ---: | ---: | ---: |
| Three-item list rows | 2,129,200 B | 1,572,144 B | 1,572,144 B |
| Five-item list rows | 2,293,608 B | 2,588,470 B | 2,129,942 B |
| Existing zip-to-starmap control | 1,100,520 B | 1,100,464 B | 1,100,470 B |
| CSV meter control | 992,024 B | 992,024 B | 992,024 B |

Three-item rows allocate 26.16% less than production and five-item
rows allocate 7.14% less. Five-item allocation falls 458,528 B versus
B1793. In the sync diagnostic, the same cases allocate 26.73% and
21.69% less, respectively. Five-item peak admission rises 40 B in
sync and 120 B in async versus production. The three images restore
the allowance after disposal. [Async allocation](async-allocation-summary.json) ·
[Sync allocation](allocation-summary.json).

## Reordered quiet comparison

The two frozen 32-file images compare the refinement with production
on November 26, 2026 UTC on Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3.
Two workers per source warm for five seconds and contribute seven
rotated batches targeting 440 ms. Ranges span four pairings of worker
medians. Negative values indicate less time. Elapsed includes mediated
RAM-backed file I/O, result and file checks and prior-result cleanup.
Process CPU also includes fixture reset and request framing.

| Complete script | Warm lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Three-item list rows | Sync | −6.54% to −2.19% | −6.11% to −1.16% |
| Five-item list rows | Async | −1.31% to +10.61% | −1.37% to +9.70% |
| Three-item list rows | Async | −6.72% to +1.67% | −5.13% to +2.27% |
| Existing zip-to-starmap control | Async | −3.02% to −0.80% | −2.73% to −0.92% |

The short sync case uses less elapsed time and CPU in both quiet
comparisons. Async five-item elapsed and CPU span zero with 9.38%
candidate elapsed spread. The async zip control improves even though
the revised drain is bypassed. Keep the candidate private until the
wide timing is resolved. The capacity hint removes the first
candidate's guaranteed replacement and lowers allocation. The quiet
repeat leaves its wider elapsed effect uncertain.


Windows .NET 11 and Ubuntu .NET 10 each pass 279 focused Release
checks, covering in-flight cancellation, ordered source advancement,
aliases, refusal, managed stack safety and reuse of the allowance.
Both images match four independent CPython goldens. The quiet run
completes all 112 batches and both machine checks. Workers join and
fixture storage is removed. The observations archive verifies.

Initial staging stopped 33,856 B below the 9,000,000 B start guard.
The verified source images were reused. Staging reclaimed expanded
observation copies from B1793 and the incomplete B1790 block after
checking each file against the persistent archive and pushed copy.
The two original archives remain available.

[Image identity](identity.json) · [Timing and spreads](summary.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures and exact patch](procedures.tar.xz).
