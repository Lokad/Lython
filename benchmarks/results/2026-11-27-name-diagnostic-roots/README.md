# B1891 Reuse function discovery for local-name diagnostics

The candidate collects function definitions in the existing syntax validation
walk. Local-name diagnostics consume those declarations in source order, removing
their separate search through module and class statements outside function
bodies. Checks within each function still follow their existing binding flow.
Modules without a function skip the diagnostic pass. The candidate adds a
temporary declaration list for modules with functions.

Both images derive from `d5f27ef3a` and differ only in the core DLL. All 32 files
per image were verified. Four CPython 3.13.16 goldens and 140 timed batches
verified on pinned Ubuntu WSL with .NET 11 RC3. The generated configuration
contains 128 guarded updates and no functions. The guarded report contains 96
functions. Two earlier catalog validations rejected the new fixture's size
metadata before warmup. Both catalogs are preserved. The Windows .NET 11
focused Release run passed 482 tests, including a regression for diagnostic
spans and order across branches, classes and nested functions.

Four workers warm at least five seconds each per lane and supply seven rotated
batches. Ranges compare pairings of worker medians. Warm elapsed includes
mediated I/O and result cleanup. The compile-to-result lane adds compilation
and compiled-owner cleanup. Process CPU includes fixture reset and request
framing. Process startup is outside the reported intervals.

| Workload | Boundary | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Generated guarded configuration | Compile | −1.91% to +1.34% | −3.30% to +3.07% |
| Ordinary definitions | Compile | −1.15% to −0.09% | −1.17% to +6.13% |
| Generated guarded report | Compile | −3.38% to +1.94% | −4.07% to +0.44% |
| Generated guarded report | Compile to result | −2.02% to +3.62% | −0.26% to +6.72% |
| Nested JSON control | Warm | −1.11% to +8.22% | −5.29% to +5.68% |

CPU spans zero throughout. The unchanged nested JSON lane varies by 8.90%
between candidate worker elapsed medians. Withdraw the standalone prototype.
Its extra declaration list and remaining nested body walk leave little design
simplification. Reconsider discovery when an existing compiler fact can carry
it or a deeper consolidation can also retire the separate local assignment walk.

[Timing summary](summary.json) · [Image identity](identity.json) ·
[Observations](observations.tar.xz) · [Procedures](procedures.tar.xz).
