# B1030 retained function metadata keys against CPython

Measured Lython `2409f0596` with core `7e525683` against stock CPython
3.13.16 on 2026-11-19 UTC. The quiet Azure F4as_v7 ran Ubuntu 24.04.4
and .NET 11 RC3. The exact retained B1028/B1029 image was reused.
All five workloads are regex-free. The source predates the regex window
consolidation. [Exact image and paired comparison](../2026-11-19-function-record-keys/README.md).

| Workload | Input | Compilation only | Warm | Compile-to-result |
| --- | --- | ---: | ---: | ---: |
| Expanded forwarding report | 1,024 records / 26,112 B | 0.83–0.84× | 1.81–1.84× | 1.70–1.73× |
| Unannotated rule closures | 2,048 inputs / 4,096 definitions | 0.79–0.80× | 4.23–4.47× | 4.07–4.31× |
| Annotated rule closures | 2,048 inputs / 2,048 definitions | 0.81× | 5.25–5.54× | 4.94–5.31× |
| Shipment report with format selectors | 3,072 records / 189,441 B | 0.97× | 1.42–1.45× | 1.37–1.42× |
| CSV invoice | 4,096 data records / 127,031 B | 0.96–0.97× | 1.10–1.13× | 1.09–1.12× |

The ratio divides Lython elapsed by CPython elapsed for all four pairings
of process medians. Warm same-source Lython spread was 5.37% for
unannotated rules and 4.09% for annotated rules. Annotated compile-to-result
spread was 5.53%. This limits comparisons with prior snapshots. The
forwarding and two rule cases use the exact B1022 catalog rows. The shipment
case uses B1025's format-subscript script and the CSV invoice uses B1002's
script and fixture. The CSV input includes a header and 1,024 records with quoted newlines.
[B1022](../2026-11-19-function-and-split-cpython/README.md) · [B1025](../2026-11-19-prepared-format-selectors/README.md) · [B1002](../2026-11-18-direct-defaults-cpython-checkpoint/README.md).

All five cases verified with the frozen worker images before staging.
Each case used two processes per engine, with all three phases in each
process and five rotated rounds. The five jobs completed in 310.70
seconds. Both quiet gates passed, and all 300 measured responses passed
the protocol and golden checks. The archive was verified under the lease
after workers joined and guest fixtures were removed.

Warm includes mediated RAM-backed I/O, stdout/file goldens and previous-result
cleanup. Compile-only includes disposal of compiled storage. Compile-to-result
also includes compilation, execution and its cleanup. Startup, fixture reset,
request framing and final cache teardown are outside guest elapsed. The
raw observations include the wider process CPU boundary. Platform and
installed-package qualification for this source was in progress at measurement.

[Per-process elapsed and CPU](summary.json) · [Raw observations](observations.tgz) · [Procedures and frozen hashes](procedures.tar.xz).
