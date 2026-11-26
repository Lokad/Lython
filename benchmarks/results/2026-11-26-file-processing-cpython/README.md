# B1744 File processing against CPython

This checkpoint measures Lython `a490e9d5f` against stock CPython 3.13.16
on the quiet Azure F4as_v7 VM running Ubuntu 24.04.4 and .NET 11 RC3 on
November 26, 2026 UTC. Each engine has two independently warmed workers
with seven rotated batches per phase. Lython / CPython divides elapsed
time, with a ratio above 1× meaning Lython takes longer. Ranges span all
pairings of process medians.

| Workload | Input or source | Phase | Lython | CPython | Lython / CPython |
| --- | --- | --- | ---: | ---: | ---: |
| Shipment callback report | 2,048 rows, 27,350 B | Warm sync | 2.22126–2.24476 ms | 1.62256–1.65026 ms | 1.346–1.383× |
| Shipment callback report | 2,048 rows, 27,350 B | Warm `RunAsync` | 2.34810–2.42338 ms | 1.63204–1.70608 ms | 1.376–1.485× |
| File-fed CSV quantity map | 2,048 rows, 27,350 B | Warm `RunAsync` | 0.54147–0.54293 ms | 1.38057–1.38132 ms | 0.392–0.393× |
| Configuration manifest | 1,877 B of source | Compilation only | 0.09717–0.09837 ms | 0.15754–0.16056 ms | 0.605–0.624× |
| Configuration manifest | Two input files, 833 B | Compile-to-result | 0.37382–0.37779 ms | 0.47152–0.47225 ms | 0.792–0.801× |

The compile-only clock includes compiled-owner disposal. Warm execution
includes applicable RAM-backed I/O, output and file checks, and disposal
of the previous result. Compile-to-result adds compilation and its cleanup.
Process startup is outside these clocks. Process CPU also includes fixture
reset and request framing. The async rows use Lython's asynchronous host
API with the same synchronous guest script and native file APIs on CPython.
Lython's mediated host uses efficient ranged reads and appends and an
active shared memory allowance.

All 140 timed batches matched their expected output and file hashes. Three
independent CPython goldens passed for the complete frozen Lython image.
Both VM quiet checks passed and every worker joined. The persistent archive
verified before the lease ended. The raw manifest carries descriptive fields
from its template. The published identity derives the patch base and image
custody directly from B1743 and preserves the raw manifest inside the archive.

The file-fed map and configuration manifest favor Lython on these boundaries.
The shipment report still spends more CPU than CPython in both hosting
modes. The async shipment's largest same-source spread is 4.54%. These are
diagnostic ratios for the measured revision.

## Protected early-exit file search

The same frozen engines ran a separate two-phase block on the same VM.
The script stops after scanning 1,025 lines of a 42,010 B input and writes
one matched line. The output check includes both the count and written file.

| Phase | Lython | CPython | Lython / CPython |
| --- | ---: | ---: | ---: |
| Warm sync | 0.23337–0.26912 ms | 0.13596–0.13848 ms | 1.685–1.979× |
| Compile-to-result | 0.31032–0.31328 ms | 0.21072–0.21076 ms | 1.472–1.487× |

The warm Lython processes differ by 15.32%. The compile-to-result Lython
processes differ by 0.95%. Wider process CPU is 1.386–1.594× warm and
1.281–1.306× through result. All 56 batches passed their output checks.
The initial local Windows diagnostic had 20–29% within-process spread
and stays in the archive. The quiet result establishes an end-to-end gap
and supports profiling the ordinary line-to-predicate path.

[File search summary](file-search/summary.json) · [File search observations](file-search/observations.tar.xz) ·
[File search procedures and local diagnostic](file-search/procedures.tar.xz).

[Identity](identity.json) · [Summary](summary.json) · [Observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Procedures](procedures.tar.xz).
