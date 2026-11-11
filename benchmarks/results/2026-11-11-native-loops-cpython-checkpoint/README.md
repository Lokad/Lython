# CSV/text against CPython — 2026-11-11

Committed and fully qualified `fe6648b2`, frozen core `bf93c7ce`, all 568 production
source hashes matching. The measured precommit binary has `1ce` assembly metadata;
source identity is recorded separately. Exact-revision delivery
[43024046530](https://github.com/Lokad/Lython/actions/runs/43024046530) passes
Windows/Ubuntu, actual .NET 11/10, original stress and installed Release consumers.
Later runtime changes require their own qualification.

| Workload | Boundary | Lython | CPython 3.13.16 | Lython / CPython | Status |
| --- | --- | ---: | ---: | ---: | --- |
| Ordinary text; 3,072 lines / 35,840 B | Compile + result | 1.481–1.545 ms | 1.269–1.281 ms | 1.16–1.22× | Variable |
| Captured text helper + counter; same input | Compile + result | 2.172–2.175 ms | 1.032–1.060 ms | 2.05–2.11× | Variable |
| CSV validation; 5,120 records / 68,625 B | Compile + result | 4.720–4.822 ms | 4.442–4.448 ms | 1.06–1.09× | Variable |
| Ordinary text | Warm | 1.410–1.412 ms | 1.157–1.168 ms | 1.21–1.22× | Diagnostic |
| Captured text helper + counter | Warm | 2.010–2.035 ms | 0.895–0.896 ms | 2.24–2.27× | Diagnostic |
| CSV validation | Warm | 4.614–4.639 ms | 4.283–4.339 ms | 1.06–1.08× | Diagnostic |
| Ordinary text | Compilation | 44.26–45.48 µs | 41.33–41.52 µs | 1.07–1.10× | Variable |
| Captured text helper + counter | Compilation | 44.23–44.45 µs | 54.18–54.38 µs | 0.81–0.82× | Variable |
| CSV validation | Compilation | 70.86–71.81 µs | 65.63–65.74 µs | 1.08–1.09× | Variable |

Above one means Lython is slower. Ranges span independent process medians, not
confidence intervals. Complete-script Lython controls differ by 4.32% ordinary
text and 2.16% CSV; captured-text CPython controls differ by 2.76%. Warm controls
are tighter. Compilation remains variable. The captured-helper gap remains large
and is not explained by compilation. No isolated optimization attribution follows
from comparing this refresh with previous revision-specific checkpoints.

The existing dedicated Azure F4as_v7 VM runs Ubuntu 24.04.4, four AMD EPYC 9V45
vCPUs and 16 GiB/no swap. Stock CPython 3.13.16 and .NET 11 RC3 use normal GC/tiering.
Four independent worker processes per job warm for five seconds each, followed
by seven rotating rounds targeting 150 ms per batch. Common worker `fec79afd`,
optional adapter 0.4 and the existing protocol are unchanged.

Nine jobs finish in 244.887 s. Both engines execute identical source and fixtures.
Text shapes match their previous external checkpoint; CSV uses the original
published script, preserving its distinction from captured-builtin source-pair
controls. Warm and compile-to-result include mediated RAM-file I/O, complete
output/file checks and prior-result cleanup. Fixture reset, protocol, startup
and final cache teardown are excluded. Compilation includes construction and
disposal. Independent phase medians need not add. Tables use synchronous Run;
real pending I/O, embedding tail latency, RSS and startup remain unqualified.

[Summary and controls](summary.json), [source identity](source-identity.json),
[loaded images](identity.json), [raw archive](observations.tgz),
[terminal cleanup proof](archive-receipt.json), [delivery](delivery-ci-status.json)
and [previous guide](prior-BENCHMARKS.md) preserve the evidence. All hashes,
goldens, ownership, quiet gates and joined cleanup pass. Raw data was archived
and verified under the owned lease, then SHA-verified after retrieval. B577 is
terminal/cached; never query it again. Publication remains postponed.
