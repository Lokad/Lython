# B1767 Current JSON document and JSON Lines attribution

The warm synchronous profiles use the same frozen 7f7c4ed59 image as the
[B1764 CPython checkpoint](../2026-11-26-converted-update-cpython/README.md).
The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 26,
2026 UTC. Workers warm for at least eight seconds and then sample CPU
at 199 Hz for about six seconds. The capture includes mediated RAM-backed
I/O, output verification, reset, framing and cleanup.

| Observation | JSON service totals | JSON Lines helper report |
| --- | ---: | ---: |
| Verified invocations during capture | 8,192 | 2,048 |
| All-thread samples | 1,249 | 1,430 |
| Samples with resolved runtime ancestry | 1,068 | 1,381 |
| Those with unresolved leaves | 99 | 95 |
| Nearest iterative construction loop | 104 | 131 |
| Nearest JSON string reader | 104 | 47 |
| Nearest decoder entry state machine | 2 | 82 |
| Nearest admission reservation | 151 | 110 |
| Dictionary accumulation ancestry | 194 | 63 |

The nearest admission caller is a UTF-8 copy on 96 document and 29 JSON
Lines samples. Record creation accounts for 49 and 31 respectively.
The decoder entry has 82 nearest samples in JSON Lines, where the parser
starts separately for every line. Its default options are already prepared.
The entry still drives async option preparation and construction. Test
keeping the completed case direct, with a continuation only for actual
pending work. Use the document totals as a low-frequency entry control
and preserve option side effects, hook order, error coordinates, managed
stack admission and shared budget unwind. The allocating token and record
routes still need their independent admission. Counts show where workers
ran. They give no estimate of removable CPU or elapsed improvement.

Both independent CPython goldens and all 32 frozen image files verify
before collection. Both quiet gates and post-capture goldens pass. All
workers and profilers join, guest fixtures are removed, and the
persistent archive verifies under the lease.

[Attribution](attribution.json) · [Identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures](procedures.tar.xz).
