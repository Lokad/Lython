# B1608 Complete cased text helpers before workspace setup

The measured source `46c967257` completes eligible exact-string helpers
before initializing their native workspace. The same length, exact
arithmetic and cased-split kernels run in order. The helper can assign its
captured width before split admission. Both executable drivers use this
route for immediate iteration. Ordinary calls retain Python lists and
dynamic receivers or rebound `len` keep their existing call path.

The quiet Azure F4as_v7 used Ubuntu 24.04.4 and .NET 11 RC3 on November
24, 2026 UTC. Frozen 32-file images differ only in the core DLL. The
reference matches `aa8064b1c`. Two workers per source run seven rotated
batches after eight seconds of request warmup. Ranges span all pairings
of process medians. Warm elapsed includes mediated RAM-backed I/O,
verification and disposal of the prior result. Wider process CPU includes
reset and request framing. Compile to result adds compilation and runs
inside a warmed host. Startup and process teardown have separate clocks.

| Complete script and boundary | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Short Unicode report, warm | −27.37 to −24.78% | −26.33 to −23.86% | 3.02 / 0.53% |
| Mixed-width histogram, warm | −11.94 to −6.62% | −12.35 to −6.78% | 1.75 / 4.21% |
| Distinct-word histogram, warm | −4.67 to −0.98% | −4.20 to +0.42% | 2.86 / 0.98% |
| Inlined short report, warm | −8.73 to +12.86% | −7.27 to +11.04% | 17.02 / 5.67% |
| Defaulted rules, warm | −2.72 to −0.70% | −0.77 to −0.27% | 1.29 / 0.78% |
| Quoted invoice, warm | −1.31 to −0.47% | 0.00 to +0.97% | 0.83 / 0.03% |
| Short Unicode report, compile to result | −25.68 to −19.01% | −23.36 to −19.31% | 1.37 / 7.52% |

Warm allocations and admitted peaks match for all three text workloads.
The diagnostic build sees 12,288, 12,288 and 1,024 completed helpers
respectively over four invocations each. None sets up the line helper's
workspace. The inlined report takes neither route. Its worker spread
makes the comparison inconclusive. The generated-code inspection finds
`StartNativeCall` grows from 310 to 448 bytes and its frame from 152 to
168 on Windows FullOpts. The native loop and regular workspace sizes
match. Compiler allocation rises by 16–32 bytes per program across the
catalog. Retained admission matches.

Windows and Ubuntu each pass 726 focused Release checks. In both modes
the new admission test observes the committed nonlocal and no loop-body
effect during refusal or cancellation. The first form of the test assumed
an exact cancellation span after unwind. The recorded regression checks
cancellation, state and cleanup, and still pins the first refusal origin.
Five independent programs match CPython 3.13.2 in each mode. Six frozen
goldens, all 196 batches, both quiet gates, joined workers, removed fixtures
and persistent archive verification pass. Exact compressed copies of two
inactive benchmark cores freed VM storage before staging.

[Timings](summary.json) · [Frozen identity](identity.json) ·
[Allocation and admission](allocation-summary-six.json) · [Generated code](jit-summary.json) ·
[Raw observations](observations.tar.xz) · [Procedures and patch](procedures.tar.xz) ·
[Storage custody](storage-custody.json) · [Terminal status](terminal-status.json).
