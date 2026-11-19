# B1153 execution profile of the retained factory workloads

Sampled the exact B1151 image, whose executable sources match `0d74b5b18`,
on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on
November 19, 2026 UTC. The complete 32-file image and catalog were already
verified. Both workers checked their CPython output golden before and
after sampling. Each warmed for eight seconds or more, then recorded
about five seconds of CPU samples for whole-script warm execution.

| Samples | Annotated rules | Nested factories |
| --- | ---: | ---: |
| Runtime ancestry | 1,057 | 1,082 |
| Definition execution ancestry | 180 | 218 |
| Default storage and annotation attachment ancestry | 156 | 112 |
| Nearest runtime method is the shared instruction prefix | 220 | 222 |
| Nearest runtime method is result consumption | 80 | 63 |
| Nearest runtime method is bound call entry | 43 | 54 |
| Nearest runtime method is storage reservation | 98 | 100 |
| Scope promotion ancestry | 0 | 0 |

The ancestry groups overlap. Inlining and incomplete stack coverage also
affect counts. Source confirms the ordinary factory begins with prepared
default construction, making its first instruction ineligible for the
native prefix. Definitions can already complete in borrowed work without
promotion. The runtime binds into final local slots, then initializes
work and publishes through the ordinary result consumer. The profile
supports examining these transitions and the still-visible metadata
construction together. It supplies no elapsed comparison or removable
CPU percentage.

Both quiet gates passed. Workers joined, guest fixtures were removed and
the persistent traces were verified under the lease before unmount.

[External timing](../2026-11-19-factory-cpython-checkpoint/README.md) ·
[Attribution](attribution.json) · [Raw observations](observations.tgz) ·
[Collection and verification](procedures.tar.xz) ·
[Lease receipt](archive-receipt.json).
