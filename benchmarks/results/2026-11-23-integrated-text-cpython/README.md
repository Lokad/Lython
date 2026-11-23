# B1458 Integrated text and frontend checkpoint against CPython

Measured the `7bc247f1a` source with the exact B1457 core against
stock CPython 3.13.16 on November 23, 2026 UTC. The quiet Azure
F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3. All 32 files of the
Lython image and the CPython executable and worker were verified.

| Workload | Boundary | Lython elapsed | CPython elapsed | Lython / CPython | Worker spread, Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: |
| Mixed-width word histogram, 3,072 lines / 107,520 B | Warm | 1.610–1.656 ms | 1.686–1.696 ms | 0.949–0.983× | 2.86 / 0.62% |
| Configuration edit, 181 B | Compile | 0.01882–0.01968 ms | 0.03397–0.03404 ms | 0.553–0.579× | 4.54 / 0.22% |
| Typed ASCII CSV meter, 4,096 records / 139,299 B | Warm | 3.054–3.080 ms | 2.492–2.529 ms | 1.208–1.236× | 0.82 / 1.48% |
| Postponed nested factories, 2,048 inputs | Compile to result | 0.987–1.127 ms | 0.6063–0.6190 ms | 1.594–1.859× | 14.23 / 2.10% |
| Short nonlocal word histogram, 3,072 lines / 35,840 B | Warm | 1.334–1.394 ms | 0.9334–0.9461 ms | 1.410–1.494× | 4.51 / 1.36% |
| Configuration edit, 181 B | Compile to result | 0.1073–0.1078 ms | 0.1265–0.1280 ms | 0.839–0.852× | 0.44 / 1.16% |
| Expanded JSON Lines export, 3,072 records / 189,441 B | Warm | 5.355–5.366 ms | 6.508–6.509 ms | 0.823–0.825× | 0.20 / 0.02% |

Ratios divide Lython elapsed by CPython elapsed over all pairings of
two independent process medians per engine. Each worker warms for
eight seconds and supplies seven rotated batches. Warm execution
includes mediated RAM-backed reads and writes on Lython, native file
calls on CPython, verification and previous-result disposal. Shared
admission is active in Lython. Compilation includes code-owner cleanup.
Time to result includes both compilation and one execution with its
cleanup. Process startup and final teardown are separate. These are
diagnostic comparisons. [Prior fresh-process measurements](../2026-11-23-runtime-cpython-checkpoint/README.md#fresh-process).

The mixed-width report sits near parity and the short report still has
a gap. The short ratio overlaps its previous 1.398–1.455× range, leaving
the B1457 source-pair slowdown open. Factory time to result varies
14.23% across Lython workers, so the magnitude of its gap is uncertain.
The configuration comparison holds the fixture constant across its
independently timed phases. [B1457](../2026-11-23-borrowed-cased-words-append/README.md) ·
[B1451](../2026-11-23-runtime-cpython-checkpoint/README.md).

The separate local Tier 1 diagnostic shows Linux stack subtraction
falling 776 → 648 B in the dictionary loop and rising 200 → 232 B in
the borrowed accumulator. The corresponding Windows numbers rise
616 → 696 B and 280 → 296 B. These measurements exclude saved
registers and leave the cause of the short-word CPU cost unresolved.

All six CPython goldens and all 196 timed batches verify. Workers
join and the archive verifies under the quiet lease after guest fixture
cleanup. Initial staging stops on free space and an already retired
replica. Final staging verifies and retires the separately archived
B1454 working core, then uses the retained B1457 image in place.

[Timing](summary.json) · [Image and input identities](identity.json) ·
[JIT diagnostic](short-jit-summary.json) · [Raw observations](observations.tar.xz) ·
[Procedures](procedures.tar.xz) · [Archive receipt](terminal-status.json).
