# B1963 CPython comparison after private iterator ownership

Lython `0b50e6fda` and stock CPython 3.13.16 ran on November 28, 2026
UTC on an Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. The
Lython worker uses the optional signed PythonRe 0.5.0 adapter. The
catalog has five workloads and seven execution lanes.

| Workload | Boundary | Lython elapsed | CPython elapsed | Elapsed ratio | Process CPU ratio |
| --- | --- | ---: | ---: | ---: | ---: |
| Ordinary helper and generator reduction | Warm sync | 2.30966–2.32132 ms | 1.96572–1.99783 ms | 1.156–1.181× | 1.132–1.185× |
| Ordinary helper and generator reduction | Warm async | 2.44217–2.45655 ms | 1.98706–2.00274 ms | 1.219–1.236× | 1.214–1.245× |
| Iterable-row shipment | Warm async | 2.14816–2.28583 ms | 1.93894–2.01075 ms | 1.068–1.179× | 1.086–1.179× |
| Repeated explicit tuple loop | Warm async | 1.13926–1.14520 ms | 1.60000–1.61554 ms | 0.705–0.716× | 0.722–0.731× |
| Markdown report from JSON | Warm async | 2.66007–2.78636 ms | 2.58856–2.61006 ms | 1.019–1.076× | 1.065–1.105× |
| File-fed CSV quantity map | Warm async | 0.42442–0.42651 ms | 1.36275–1.37681 ms | 0.308–0.313× | 0.345–0.350× |
| Ordinary helper and generator reduction | Sync compile to result | 2.32491–2.34569 ms | 2.06318–2.06373 ms | 1.127–1.137× | 1.119–1.147× |

Each range spans pairings of medians from two workers per engine. Ratios
divide Lython by CPython. Warm elapsed includes RAM-backed mediated I/O,
output and file verification, and previous-result disposal. Process CPU
also includes fixture reset and request framing. Compile to result includes
compilation and compiled-owner cleanup. Both engines run in prewarmed processes.
The CSV inputs contain 2,048 records and 27,350 bytes. The report input
contains 3,072 records and 189,441 bytes. The iterable-row Lython workers
differ by 6.41% in elapsed. The JSON report has a 4.75% Lython spread.

The previous external checkpoint used `09cecc9ce` with the same workload
boundaries. These endpoints preserve the current gap. The separate matched
comparison records the effect and the adverse control of the last ownership
change. [Previous CPython checkpoint](../2026-11-28-post-generator-cpython/README.md) ·
[Matched change comparison](../2026-11-28-private-outer-ownership/README.md).

Both quiet checks, both five-case output and file verifications, and all
196 timed batches pass. The workers joined and the RAM-backed fixtures
were removed. The persistent archive was verified before the lease ended.
[Timing summary](summary.json) · [Worker identity](identity.json) ·
[Observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal status](terminal-status.json).
