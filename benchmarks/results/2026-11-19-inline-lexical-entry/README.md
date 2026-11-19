# B1117 common lexical call entry

`7fd3a5af3` keeps ordinary borrowed prefixes in the common bound-call entry
and moves promotion to the instruction where a context is required. A factory
can complete a fully supplied lexical leaf call through the existing dispatcher
and forward a fixed return on the same admitted slots. A prepared child that
needs a context joins the ordinary call chain at its current instruction.
The changed call sources and tests were retained at `47fbd2c1b`, alongside a
separate regex contract correction. The measured reference `d0fc1bb93` matches
the runtime and tests at `a52ddb876`.

Both complete 32-file images passed all five CPython workload goldens. B1117
ran on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on November
19, 2026 UTC. Warm timings include mediated RAM I/O, golden checks and previous
result cleanup. Compile-to-result adds compilation and compiled-owner disposal.
The table spans all four pairings of duplicate-process medians. Negative values
indicate less time or allocation.

| Workload | Boundary | Elapsed change | Process CPU change | Local warm allocation change |
| --- | --- | ---: | ---: | ---: |
| Captured rules | Warm | −13.63…−8.11% | −14.15…−7.61% | −26.90% |
| Captured rules | Compile-to-result | −9.86…−7.05% | −10.50…−5.41% | — |
| Ordinary rules | Warm | −6.63…−2.82% | −7.28…−4.57% | −10.28% |
| Annotated rules | Warm | −7.82…+1.22% | −7.64…+0.99% | 0.00% |
| Retained nonlocal siblings | Warm | −3.83…+1.19% | −5.15…+0.11% | 0.00% |
| CSV invoice | Warm | −3.75…−0.22% | −3.42…0.00% | −0.03% |

Captured allocation falls from 3,288,680 to 2,403,944 bytes. Accounted peaks
match and all local checks return the shared allowance to baseline. Windows
.NET 11 and Ubuntu .NET 10 each passed 238 focused Release checks, including
retained captures, real host suspension, cancellation and cleanup.

The annotated reference workers differ by 8.52% in the first run. The repeat
has mixed results, including up to 4.38% higher elapsed and 5.08% higher CPU in
one block. Its reversed block includes faster and slower pairings. B1115 had
5.73–8.60% slower elapsed on all annotated pairings. Retain the captured
and ordinary gains and keep annotated CPU and elapsed open at delivery.

All 120 timed batches verified in 419.82 seconds. The quiet gates passed, workers
joined and fixtures were removed. Staging retired only a B1085 VM copy after
checking the committed core and observation hashes. The persistent archive was
verified under the lease.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Source and checks](checks-and-procedures.tar.xz) · [Lease receipt](archive-receipt.json) ·
[Annotated control repeat](confirmation/README.md) ·
[Original prototype](../2026-11-19-borrowed-lexical-leaf/README.md).
