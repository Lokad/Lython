# B1791 Representative checkpoint against CPython

Lython `d370fef84` and stock CPython 3.13.16 ran on the quiet Azure F4as_v7
with Ubuntu 24.04.4 and .NET 11 RC3 on November 26, 2026 UTC. Each phase
uses two workers per engine, five seconds of warmup per worker and seven
rotated batches targeting 460 ms. Elapsed ratios divide Lython by CPython
and span all four pairings of worker medians. Above 1× means Lython takes
longer. [Per-worker spreads and CPU](summary.json)

| Workload | Input or source | Phase | Lython | CPython | Elapsed ratio | Status |
| --- | --- | --- | ---: | ---: | ---: | --- |
| Shipment callback report | 2,048 records / 27,350 B | Warm sync | 1.77070–1.78975 ms | 1.62509–1.64124 ms | 1.079–1.101× | Diagnostic. |
| Shipment callback report | 2,048 records / 27,350 B | Warm `RunAsync` | 1.83667–1.97645 ms | 1.63960–1.65973 ms | 1.107–1.205× | Diagnostic. Lython worker spread is 7.61%. |
| Generated ordinary definitions | 96 definitions, built 16 times | Warm sync | 0.18913–0.19417 ms | 0.14880–0.15121 ms | 1.251–1.305× | Diagnostic. |
| Generated ordinary definitions | 96 definitions, built 16 times | Warm `RunAsync` | 0.19222–0.19304 ms | 0.14657–0.14744 ms | 1.304–1.317× | Diagnostic. |
| Generated rules with defaults | 2,048 rule groups | Warm sync | 0.83941–0.86929 ms | 0.68434–0.68462 ms | 1.226–1.270× | Diagnostic. |
| Generated rules with defaults | 2,048 rule groups | Warm `RunAsync` | 0.85033–0.89071 ms | 0.68212–0.69080 ms | 1.231–1.306× | Diagnostic. |
| JSON service quantity totals | 2,048 records / 78,755 B | Warm sync | 0.63272–0.64399 ms | 0.48671–0.51045 ms | 1.240–1.323× | Diagnostic. |
| JSON service quantity totals | 2,048 records / 78,755 B | Warm `RunAsync` | 0.63484–0.64640 ms | 0.48294–0.49659 ms | 1.278–1.338× | Diagnostic. |
| Short Unicode word-count report with a nonlocal helper | 3,072 lines / 35,840 B | Warm sync | 0.86052–0.86730 ms | 0.93399–0.94550 ms | 0.910–0.929× | Diagnostic. |
| Generated ordinary definitions | 8,641 B of source | Compilation only | 0.56433–0.56881 ms | 1.08677–1.09431 ms | 0.516–0.523× | Diagnostic. |
| Shipment callback report | 2,048 records / 27,350 B | Compile-to-result | 1.86382–1.88807 ms | 1.89602–1.90010 ms | 0.981–0.996× | Diagnostic. |
| Generated ordinary definitions | 96 definitions, built 16 times | Compile-to-result | 0.77094–0.77810 ms | 1.12002–1.12085 ms | 0.688–0.695× | Diagnostic. |

Warm execution reuses compiled code in a running process. Elapsed includes
applicable RAM-backed guest I/O, verified output and files, and disposal of
the previous result. Lython uses the mediated comparison host with shared
memory admission. CPython uses native file I/O and ordinary cyclic GC.
Async rows compare Lython `RunAsync` with the same synchronous CPython script.
Compile-only includes disposal of compiled code. Compile-to-result combines
compilation, one synchronous run and both owner cleanups. Fixture reset,
request framing and process startup sit outside elapsed. Process CPU covers
the wider request. Startup has a separate historical benchmark.

The five scripts, fixture bytes and ten matching lanes reuse the
[B1784 catalog](../2026-11-26-post-fixed-row-cpython/README.md). Two
warm async lanes extend the catalog for defaulted rules and JSON totals.
B1784 and B1791 ran separately. The paired Lython trials isolate
[literal keyword defaults](../2026-11-26-literal-keyword-defaults/confirmation/README.md)
and [one-byte JSON allocation](../2026-11-26-json-ascii-character/README.md).

All five independent CPython goldens, the pinned CPython executable and
all 32 Lython image files verify. All 336 batches and both quiet checks
pass. Workers join, guest fixtures are removed and the persistent raw
archive verifies. Staging reused the B1790 worker image and retired
one older VM DLL after checking its full local backup, all dependent
frozen images and the matching committed observations.

[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Terminal receipt](terminal-status.json) · [Procedures and custody](procedures.tar.xz)
