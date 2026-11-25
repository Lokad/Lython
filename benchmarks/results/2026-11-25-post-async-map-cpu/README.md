# B1696 Shipment and defaulted-rule CPU after async map

The profiles use the frozen production `f6cf9796c` image from the
[B1694 CPython comparison](../2026-11-25-retained-async-cpython/README.md).
The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25,
2026 UTC. Each worker warms for eight seconds or more and samples all
threads at 199 Hz for about six seconds. The shipment profiles share the
same script and fixtures and use `Run` and `RunAsync`. The defaulted
rules include compilation and disposal. The samples include verification,
RAM-backed fixture handling and request framing.

| Profile | Invocations | Samples | Resolved runtime | Nearest frontend |
| --- | ---: | ---: | ---: | ---: |
| Shipment, warm sync | 2,560 | 1,261 | 1,229 | 12 |
| Shipment, warm async | 2,304 | 1,287 | 1,248 | 12 |
| Defaulted rules, through result | 6,656 | 1,215 | 1,181 | 39 |

The warm shipment profiles put 225/202 nearest runtime samples in executable
instruction dispatch. The async profile resolves 36 nearest samples to
the zip pull state machine and 34 to the default async iterator wrapper.
Both profiles still spend work in CSV row description and emission. The
comparison host appears in 10/8 inclusive resolved runtime stacks. This
bounded RAM-backed run therefore supports inspecting row transport and
async pull handoff. It cannot quantify the cost of real delayed host I/O.

Defaulted rules have 543 resolved stacks through definition construction.
The nearest runtime methods include 211 in pair member construction and
167 in admission. Of those 167, 98 resolve to callable construction and
69 to default storage. The compiler accounts for a small share in this
complete case. Both observable admission points remain required.

Nearest and inclusive counts describe different views of sampled stacks.
Inlining and unresolved frames limit attribution. The counts establish no
removable CPU percentage. Both quiet gates, golden verification before
and after each profile, worker joins, fixture cleanup and archive
verification passed.

[Identity](identity.json) · [Attribution](attribution.json) ·
[Raw profiles](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures](procedures.tar.xz).
