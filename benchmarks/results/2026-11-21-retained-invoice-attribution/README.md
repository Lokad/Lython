# B1301 retained CSV policy invoice attribution

Profiled `8f210d967`, with the same production source as
`d51891009`, on November 21, 2026 UTC. This is the exact Lython image
from [B1298](../2026-11-21-cpython-method-invoice/README.md).
The workload reads 4,096 CSV records, applies two policy methods per
row and writes a grouped report. The 127,031-byte input has six fields
per record. It includes 1,024 quoted records with embedded newlines.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3. Sampling
covered complete warmed execution batches under the runtime perf map
setting. Worker requests include fixture work and cleanup.

| Observed scope or nearest resolved method | Samples |
| --- | ---: |
| All process samples | 1,541 |
| Unmapped leaf | 118 |
| General instruction prefix, nearest | 229 |
| Native instruction loop, nearest | 173 |
| CSV parser feed, nearest | 113 |
| CSV record finalization, nearest | 52 |
| Ordinary Python instance-method lookup, nearest | 52 |
| CSV input and parsing, inclusive | 224 |
| CSV finalization, inclusive | 83 |
| CSV row lookup and adoption, inclusive | 31 |
| Text line acquisition, inclusive | 10 |
| Fixture work without a resolved runtime ancestor | 19 |

Inclusive groups overlap. Inline and unresolved work also limits attribution.
The profile provides no speedup estimate. Line acquisition and header adoption
are smaller visible targets than parsing and finalization. The next prototype
should build final fields directly from ranges in the owned line buffer when
the existing grammar needs no transformation. Preserve general quoted and
multiline parsing, Python error locations, cancellation, row admission and
retained aliases. Include a quote-heavy control as well as the invoice.

Before staging, the complete 32-file image passed the same CPython golden.
Both machine gates passed. The worker verified output before and after
capture, and all users joined. The persistent archive verified before the
fixture was unmounted. The first staging attempt named the timing-only
archive script in its validation list. Recovery verified the already staged
files and archives under a fresh idle lease before starting the profiler.

[Attribution](attribution.json) · [Raw observations](observations.tgz)
· [Image proof and procedures](procedures.tar.xz).
