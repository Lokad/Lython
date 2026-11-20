# B1222 Dataclass declaration delivery against CPython

Measures `72a1426dd` with the source-matched `842c2c8f9` image
against stock CPython 3.13.16. The quiet Azure F4as_v7 ran Ubuntu
24.04.4 and .NET 11 RC3 on 2026-11-20 UTC. Two independent processes
per engine warm for each case, followed by five rotated rounds
targeting 0.4-second batches. The ratio is Lython elapsed divided by
CPython elapsed. Ranges cross the four process median pairings.

| Workload | Boundary | Lython | CPython | Ratio | Duplicate elapsed spreads, Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: |
| Inherited dataclasses | Warm | 0.198–0.204 ms | 4.881–5.089 ms | 0.039–0.042× | 3.19% / 4.26% |
| Inherited dataclasses | Compile only | 0.20160–0.20564 ms | 0.46393–0.46557 ms | 0.433–0.443× | 2.00% / 0.35% |
| Inherited dataclasses | Compile to result | 0.420–0.430 ms | 5.496–5.725 ms | 0.073–0.078× | 2.37% / 4.17% |
| Configuration manifest | Warm | 0.19562–0.19898 ms | 0.15924–0.16073 ms | 1.22–1.25× | 1.72% / 0.94% |

The dataclass script defines a base and 48 derived classes and creates
48 instances. Warm times include class creation and decoration, output
checks and previous-result cleanup. The manifest reads 833 bytes
across two files for four targets. Warm includes mediated RAM-backed
file I/O and checks. Compile-to-result adds compilation and code-owner
disposal. Compilation alone includes code-owner disposal. Fixture
reset, process startup and final teardown are outside the guest clocks.
Wider process CPU includes request framing and reset. Lython runs with
one unlimited shared allowance and active accounting. CPython uses
ordinary runtime and native I/O.

The dataclass figures describe this generated workload and its exact
execution boundary. The manifest remains 1.22–1.25× warm. Use
[B1220](../2026-11-20-direct-declaration-operands/README.md) to attribute
the compiler change. Prior ratios retain their own source revisions.

All 80 timed batches, complete worker and reference identities, output
goldens and both quiet checks passed. Workers joined, the persistent
archive verified under lease and guest fixtures were removed.

[Timings and controls](summary.json) · [Raw observations](observations.tgz) ·
[Source and procedures](procedures.tar.xz).
