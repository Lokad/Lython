# B1701 Synchronous scopes for private cursors

Private B1701 changes ordinary cursor consumers from a completed
disposal await to a synchronous scope. Async pulls and real host
or iterator-owned cleanup stay awaited. Only the outbound CLR
enumerator interface keeps its disposal method. The reference is
committed `7ed1caa74`, whose core source matches B1700. Both complete
32-file images differ only in the core DLL.

The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25,
2026 UTC. Two processes per source each warm for five seconds and run
seven rotated batches. Ranges span pairings of process medians. Warm
elapsed includes applicable mediated RAM-backed I/O, output and file
verification and prior-result disposal. Process CPU also includes
request framing and reset. Compilation, startup and final teardown
are outside both clocks.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| File-fed map | Warm async | +1.95 to +12.36% | +1.75 to +11.01% | 6.91 / 3.08% |
| Generator over map | Warm async | −0.10 to +0.59% | 0.00 to +1.36% | 0.08 / 0.61% |
| Configuration manifest | Warm async | +0.10 to +1.68% | +1.00 to +2.37% | 0.55 / 1.02% |
| Quoted CSV invoice | Warm async | −0.47 to +2.37% | +0.52 to +2.22% | 1.08 / 1.75% |
| Quoted CSV invoice | Warm sync | −0.64 to +0.74% | 0.00 to +1.08% | 0.37 / 1.02% |

Keep the broad rewrite private. The candidate uses more CPU on the
manifest and invoice async lanes. Variation limits the size of the
file-fed regression. This comparison does not locate which changed
consumer contributes to the timing. All local admitted peaks match.
Managed allocation is flat except for 224 B less in the generator-over-map
diagnostic. Every run restores the allowance.

Windows .NET 11 and Ubuntu .NET 10 each pass 718 white-box and 2,605
public focused Release tests, including short-circuit, callback failure,
cancellation, terminal refusal and resource owners. Both worker images
verify four CPython goldens. All 140 timed batches, both quiet machine
gates, worker joins, fixture removal and under-lease archive verification
complete.

[Source and images](identity.json) · [Timings](quiet-summary.json) ·
[Allocation and admission](allocation-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json) ·
[Procedures and patch](procedures.tar.xz).
