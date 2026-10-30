# Native match.expand allocation diagnostic

Measured 2026-10-30 on Windows x64, Intel Core i7-14700KF, .NET 11 RC3.
The control is the actual Release net11.0 DLL from source 31772bdd; the candidate
includes native group-name metadata and the native expansion parser. Exact
[production/assembly identities and raw counts](measurements.json) are recorded.

| Case | Managed bytes / 100 expansions before | After |
| --- | ---: | ---: |
| 4,096-byte ASCII literal template | 2,892,800 | 1,152,800 |
| Eight named references to a 1,024-byte supplementary capture | 4,343,200 | 4,149,600 |

These are allocation diagnostics, not elapsed-time claims. The smaller improvement
on capture-heavy output remains visible: native builder growth and final-copy
capacity still cost memory. No CPython multiplier or primary-table update follows.

The frozen [driver](Expand.cs) constructs a real backend match before sampling,
then calls the actual internal ExpandReplacementTemplate in a reused context.
It warms twenty expansions and samples one hundred with the current-thread
allocation counter. Complete output checks run before/after sampling. Setup,
compilation, matching and CLR projection are excluded. The bounded case's owner
funds retained results and returns to zero on disposal.

Reproduce in a Release net11.0 console named Lokad.Lython.WhiteBox.Tests,
referencing each chosen real core DLL, Lokad.Parsing, the unchanged source adapter
and pinned PythonRe/Core 0.4.0. Rebuild/run each DLL in a separate process, exclude
other core DLLs from project globs and verify the driver's emitted SHA-256.
