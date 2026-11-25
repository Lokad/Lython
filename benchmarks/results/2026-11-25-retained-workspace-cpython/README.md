# B1713 Qualified runtime against CPython

The frozen `7d24cfe9a` image is source-equivalent to the qualified
`75af75769` runtime, including shared CSV source progression and native
callback workspace borrowing. The Azure F4as_v7 running Ubuntu 24.04.4
measured .NET 11 RC3 and stock CPython 3.13.16 on November 25, 2026 UTC.
All 32 measured image files and seven complete output/file goldens
were verified. Both quiet checks and all 308 timed batches passed.

Each phase warms two processes per engine for five full-request seconds
apiece. Each process contributes seven rotated batches targeting 0.46
request seconds. Ratios divide Lython by CPython and span the four
pairings of worker medians. Async rows use Lython's `RunAsync` and
the same Python script and file API on CPython.

Warm and compile-to-result elapsed include mediated RAM-backed I/O when
applicable, result/file verification and cleanup. Lython uses its efficient
ranged-read and append host with shared memory admission active. CPython
uses native I/O. Compilation-only includes disposal of compiled owners.
Fixture reset and framing are included in the wider CPU clock. Startup and
final process teardown are outside these measurements.

| Workload | Phase | Lython | CPython | Elapsed ratio | Wider CPU ratio | Elapsed worker spread Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| File-fed CSV quantity map, Lython `RunAsync` | Warm async | 0.62796–0.66850 ms | 1.35808–1.36582 ms | 0.460–0.492× | 0.486–0.523× | 6.45% / 0.57% |
| Short Unicode word-count report with a nonlocal helper | Warm sync | 0.87737–0.90172 ms | 0.94829–0.95177 ms | 0.922–0.951× | 0.905–0.953× | 2.77% / 0.37% |
| Generated ordinary definitions | Warm sync | 0.32315–0.32420 ms | 0.14886–0.14890 ms | 2.170–2.178× | 2.170–2.213× | 0.32% / 0.03% |
| Shipment callback report, Lython `RunAsync` | Warm async | 2.61774–2.63627 ms | 1.63253–1.63645 ms | 1.600–1.615× | 1.591–1.616× | 0.71% / 0.24% |
| JSON Lines export with default options | Warm sync | 4.31705–4.40967 ms | 5.11480–5.16655 ms | 0.836–0.862× | 0.847–0.881× | 2.15% / 1.01% |
| Quote-heavy CSV policy invoice | Warm sync | 6.23365–6.23861 ms | 6.32013–6.33260 ms | 0.984–0.987× | 0.971–1.000× | 0.08% / 0.20% |
| Shipment callback report | Warm sync | 2.35417–2.39429 ms | 1.61364–1.61731 ms | 1.456–1.484× | 1.447–1.503× | 1.70% / 0.23% |
| Generated ordinary definitions, 8,641 B of source | Compilation only | 0.57485–0.57872 ms | 0.94452–0.95286 ms | 0.603–0.613× | 0.613–0.625× | 0.67% / 0.88% |
| Generated rules with defaults | Compile-to-result | 0.91329–0.93323 ms | 0.73408–0.74795 ms | 1.221–1.271× | 1.245–1.287× | 2.18% / 1.89% |
| Shipment callback report | Compile-to-result | 2.50925–2.59438 ms | 1.91198–1.92363 ms | 1.304–1.357× | 1.307–1.333× | 3.39% / 0.61% |
| Generated ordinary definitions | Compile-to-result | 0.91658–0.91816 ms | 1.12381–1.12547 ms | 0.814–0.817× | 0.811–0.821× | 0.17% / 0.15% |

These results describe the measured revision. The dictionary prototypes
were measured separately. Earlier CPython observations remain available
without combining ratios across machine collections.

[Delivery checks](https://github.com/Lokad/Lython/actions/runs/45574754646) ·
[Identity](identity.json) · [Timings](summary.json) · [Observations](observations.tar.xz) ·
[Terminal receipt](terminal-status.json) · [Procedures](procedures.tar.xz) ·
[Previous checkpoint](../2026-11-25-retained-csv-cpython/README.md).
