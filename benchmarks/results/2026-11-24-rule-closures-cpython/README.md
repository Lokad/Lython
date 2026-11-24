# B1561 generated rule closures against CPython

Each script builds and calls 2,048 rule groups. The Lython image is
`5d54f7afb`, with source and tests matching shipped `566259ebc`.
The baseline is stock CPython 3.13.16.

| Workload and phase | Lython elapsed | CPython elapsed | Lython / CPython | Worker spread, Lython / CPython |
| --- | ---: | ---: | ---: | ---: |
| Default rules, warm | 1.62443–1.63024 ms | 0.68466–0.69571 ms | 2.335–2.381× | 0.36 / 1.61% |
| Annotated rules, warm | 0.72794–0.77428 ms | 0.41026–0.41184 ms | 1.768–1.887× | 6.37 / 0.38% |
| Captured rules, warm | 1.73819–1.77995 ms | 0.76788–0.78525 ms | 2.214–2.318× | 2.40 / 2.26% |
| Captured rules, compile to result | 1.82285–1.87297 ms | 0.81602–0.82182 ms | 2.218–2.295× | 2.75 / 0.71% |
| Captured rules, compilation only | 0.02651–0.02658 ms | 0.04693–0.04695 ms | 0.565–0.566× | 0.27 / 0.05% |

Warm measures fully interpreted guest programs through output verification
and disposal of the prior result, using already compiled code. These
scripts perform no file I/O. The compile-to-result clock adds compilation
and disposal of the compiled owner. The compile-only clock includes that
disposal. Process startup, fixture reset and final teardown are outside
the elapsed clocks. Wider process CPU is available in the summary.

On November 24, 2026 UTC the dedicated Azure F4as_v7 ran Ubuntu
24.04.4 and .NET 11 RC3. Each lane uses eight seconds of warmup and
seven samples for each of two workers per engine. Ratios span all four
pairings of process medians. The annotated lane's Lython spread leaves
the exact gap uncertain. All 140 batches match their goldens. Both quiet
gates, joined cleanup and the persistent archive verify. The first
launch stopped before timing because the collector omitted the
auxiliary-file manifest. Its diagnostics and cleanup check are retained.

Separately, Windows x64 .NET 11 RC3 EventPipe captures allocation ticks
from 3,648 default, 6,848 annotated and 2,656 captured-rule invocations.
The allocation weights attribute 29.79% to lexical cells and 7.28% to
cell arrays in the captured script. Compiler tapes show four captured
locals in the factory, with three referenced directly by its returned
rule. The default and annotated scripts instead spend about 27% of
sampled allocation in singleton positional-default objects. These
weights provide allocation attribution. They do not establish CPU
costs, exact object counts or a percentage of time saved.

[Timing](summary.json) · [Image identity](identity.json) ·
[Raw timing](observations.tar.xz) · [Terminal receipt](terminal-status.json) ·
[Allocation profile](allocation-profile.json) · [Traces and tapes](allocation-traces.tar.xz) ·
[Initial launch](initial-launch.tar.xz) · [Procedures](procedures.tar.xz) ·
[Earlier rule comparison](../2026-11-19-retained-return-cpython/README.md).
