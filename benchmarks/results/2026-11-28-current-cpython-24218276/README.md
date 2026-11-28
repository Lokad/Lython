# B1914 Compare `24218276c` with CPython on the quiet VM

The November 28, 2026 UTC comparison uses Azure F4as_v7, Ubuntu 24.04.4,
.NET 11 RC3 and stock CPython 3.13.16. The 32-file Lython image is the
exact delivered B1913 candidate. It reuses 31 immutable files from B1906.
The log cases use the signed PythonRe 0.5.0 adapter. Both engines verify
the same independent outputs and file contents.

Two workers per engine warm independently for at least five seconds
and supply seven rotated batches per lane. Ratios divide Lython by
CPython and span all worker-median pairings. Warm execution includes
applicable mediated RAM-backed I/O, output and file verification and
previous-result disposal. Compile-only uses resident source and includes
previous compiled-owner cleanup. Compile-to-result also runs the script
and includes its I/O and result cleanup. Process CPU includes fixture
reset and framing. Startup is outside these clocks.

| Workload | Phase | Lython elapsed | CPython elapsed | Elapsed ratio | Process CPU ratio |
| --- | --- | ---: | ---: | ---: | ---: |
| Iterable-row shipment | Warm | 2.04167–2.08268 ms | 1.94685–1.96163 ms | 1.041–1.070× | 1.025–1.076× |
| Iterable-row shipment | Warm `RunAsync` | 2.31117–2.36870 ms | 1.94072–1.94675 ms | 1.187–1.221× | 1.195–1.229× |
| Nested JSON document summary | Warm | 0.71611–0.72658 ms | 0.67864–0.68856 ms | 1.040–1.071× | 1.034–1.072× |
| Nested JSON document summary | Warm `RunAsync` | 0.71616–0.75766 ms | 0.67795–0.67815 ms | 1.056–1.118× | 1.082–1.135× |
| File-fed CSV quantity map | Warm | 0.40046–0.40623 ms | 1.36077–1.36727 ms | 0.293–0.299× | 0.314–0.323× |
| File search | Warm | 0.06659–0.06689 ms | 0.05077–0.05096 ms | 1.307–1.318× | 0.992–1.026× |
| Compiled-pattern regex log search | Warm | 2.78428–2.85304 ms | 0.95047–0.99712 ms | 2.792–3.002× | 2.664–2.831× |
| Module-level regex log search | Warm | 3.08377–3.11461 ms | 1.59613–1.63767 ms | 1.883–1.951× | 1.837–1.879× |
| Generated ordinary definitions | Compile only | 0.49495–0.50721 ms | 0.94405–0.94905 ms | 0.522–0.537× | 0.528–0.541× |
| Configuration edit | Compile to result | 0.09532–0.09860 ms | 0.12488–0.12548 ms | 0.760–0.790× | 0.717–0.747× |

The compiled regex search has a 4.91% CPython elapsed spread between
workers. Dense JSON async has a 5.79% Lython spread. The first-line
elapsed gap is about 16 µs and CPU spans parity. The lower dense sync
and shipment ratios relative to B1906 span several Lython changes and
separate observations. Leave the B1906 fresh launch and later-line
search measurements at their original revision. [B1906](../2026-11-27-current-cpython-a159f24/README.md).

All 280 batches and both quiet gates pass in about 409 seconds.
All workers join, fixtures are removed and the persistent archive
verifies under the VM lease. The measured image stays on persistent
storage. One idle benchmark core replica was reclaimed after verifying
the full historical image and observations. Its exact DLL remains
available in the verified local image.

[Timing and CPU](summary.json) · [Image identity](identity.json) ·
[Staging](stage-custody.json) · [Observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Procedures](procedures.tar.xz) ·
[Current attribution](../2026-11-28-current-script-profile/README.md).
