# B1593 Annotated and generated rules against CPython

This checkpoint measures four complete factory scripts on the quiet Azure
F4as_v7 running Ubuntu 24.04.4 on November 24, 2026 UTC. The 32-file Lython
image at `4eb8c9559` matches production `5313f9e71` and runs on .NET 11 RC3.
The external engine is stock CPython 3.13.16. Both engines use two independent
workers per case and phase. Each warms for eight seconds and provides seven
rotated batches. Ratios divide Lython elapsed by CPython elapsed across the
four pairings of worker medians. Values above 1× mean Lython takes longer.

| Workload | Boundary | Lython | CPython | Lython / CPython | Worker spread Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: |
| Eager annotated rules | Warm execution | 0.43634–0.47390 ms | 0.41110–0.41435 ms | 1.053–1.153× | 8.61 / 0.79% |
| Postponed nested annotated rules | Warm execution | 0.59811–0.61674 ms | 0.53988–0.54404 ms | 1.099–1.142× | 3.11 / 0.77% |
| Generated rules with captured locals | Warm execution | 1.55133–1.60904 ms | 0.76751–0.77539 ms | 2.001–2.096× | 3.72 / 1.03% |
| Generated rules with defaults | Warm execution | 1.39021–1.39747 ms | 0.68374–0.69142 ms | 2.011–2.044× | 0.52 / 1.12% |
| Eager annotated rules | Compilation only | 0.01642–0.01643 ms | 0.03059–0.03088 ms | 0.532–0.537× | 0.08 / 0.94% |
| Eager annotated rules | Compile-to-result | 0.46870–0.47116 ms | 0.43951–0.44135 ms | 1.062–1.072× | 0.53 / 0.42% |
| Postponed nested annotated rules | Compilation only | 0.02137–0.02188 ms | 0.03949–0.03962 ms | 0.539–0.554× | 2.42 / 0.34% |
| Postponed nested annotated rules | Compile-to-result | 0.60983–0.63550 ms | 0.57547–0.58786 ms | 1.037–1.104× | 4.21 / 2.15% |

Each script constructs and applies rules across 2,048 iterations. The warm
clock uses already compiled code in a running worker. The four scripts use
captured stdout without guest file I/O. The clock includes result verification
and prior-result disposal. The compilation-only lanes create and dispose a
fresh compiled owner each time. Compile-to-result adds one execution and its
result cleanup. Request handling and fixture resets are outside elapsed and
inside wider process CPU. Startup and final worker teardown are separate.

Eager and postponed annotated execution remains closer to CPython than the
captured and defaulted workloads. The 8.61% same-source spread makes the size
of the eager warm gap uncertain. Its compile-to-result gap is 6.20–7.20%
with tighter worker agreement. The postponed first-use range is wider.
Captured and defaulted warm ratios remain around 2×. This external run
measures the residual gap. It does not resolve the postponed comparison to
older Lython sources.

All four CPython goldens, 224 timed batches and both quiet gates verify.
Workers join, fixtures retire and the archive verifies under the VM lease.
The ratios are diagnostic. The prior files/JSON and earlier startup
comparisons still describe their own revisions.

[Timings and CPU](summary.json) · [Image identity](identity.json) ·
[Raw observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal status](terminal-status.json) ·
[Native entry attribution](../2026-11-24-current-annotated-rule-attribution/README.md) ·
[Earlier checkpoint](../2026-11-24-shared-traversal-cpython/README.md).
