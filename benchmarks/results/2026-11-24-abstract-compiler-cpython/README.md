# Configuration scripts against CPython (B1614)

On November 24, 2026 UTC, an Azure F4as_v7 VM running Ubuntu 24.04.4
compared committed Lython `0bf516c798528ed4ecfa7d3ce78e867043e1b4fd`
on .NET 11 RC3 with stock CPython 3.13.16. The manifest reads an
833-byte pair of input files and writes normalized configuration
and a manifest. The generated validator exercises 96 guards and
reads a 1,714-byte JSON input.

| Workload | Phase | Lython | CPython | Lython / CPython elapsed | Wider process CPU ratio |
| --- | --- | ---: | ---: | ---: | ---: |
| Configuration manifest | Warm | 0.19838–0.20052 ms | 0.16458–0.16692 ms | 1.188–1.218× | 1.039–1.086× |
| Configuration manifest | Compile | 0.11208–0.11333 ms | 0.16582–0.16676 ms | 0.672–0.683× | 0.678–0.684× |
| Configuration manifest | Compile-to-result | 0.38400–0.38449 ms | 0.46215–0.46551 ms | 0.825–0.832× | 0.819–0.822× |
| Sparse generated validator | Compile | 1.27724–1.28559 ms | 1.81308–1.82159 ms | 0.701–0.709× | 0.698–0.709× |
| Sparse generated validator | Compile-to-result | 1.50615–1.52170 ms | 1.99316–2.00943 ms | 0.750–0.763× | 0.745–0.773× |

Two separately warmed workers per engine produced seven rotated batches
each in five serial lanes. The ranges span the four pairings of worker
medians. The largest worker spread was 1.43%. All 140 timed batches
passed output, file and image checks. Both quiet gates passed, all
workers joined and fixture cleanup completed. These are diagnostic
comparisons of the whole execution paths. [B1613](../2026-11-24-abstract-collection-graphs/README.md)
isolates the within-Lython compiler change.

Each lane uses running workers. Compilation includes compiled-owner
cleanup. Warm uses prepared code and includes applicable RAM-backed
guest I/O, output and file checks, and prior-result disposal.
Compile-to-result combines a fresh compilation, one execution and
cleanup. Fixture reset and request framing sit outside elapsed time
and inside wider CPU. Lython uses efficient mediated reads and appends
with shared-budget admission active. CPython uses native file APIs.
Startup and final process teardown are outside these measurements.

The [summary](summary.json), [image identities](identity.json),
[observations](observations.tar.xz), [procedures](procedures.tar.xz)
and [terminal receipt](terminal-status.json) preserve the run.
The verifier checked both goldens using the complete frozen Lython
image and isolated CPython 3.13.2 before staging the VM run.

## Static builtin resolution

Eight independent, untimed compatibility probes on the same Lython
revision pass on isolated CPython 3.13.2 and fail Lython compilation
for both public execution modes. The false diagnostics assume builtin
call targets or return types after a function definition, parameter,
assignment, factory call or import shadows `int`, `len`, `open` or
`str`. Sources and exact results are in the [procedures](procedures.tar.xz).
