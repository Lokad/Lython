# B1613 Reuse collection facts during compiler invalidation

The patch against `11429d3e9` keeps a conservative summary of the
existing value facts on each abstract binding state. It skips invalidation
and deferred-scope filtering when the state cannot contain affected facts.
Tuple widening visits only branches that can contain mutable sequences.
The existing guard still bounds widening through mixed mutable graphs.
Branch copies, transfers and joins carry the summary with the value map.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Configuration manifest | Compile | −1.56 to −0.05% | −1.80 to +1.68% | 0.73 / 0.79% |
| Sparse generated validator | Compile | −1.84 to +0.13% | −1.05 to +0.59% | 1.96 / 0.05% |
| CSV aggregation with helpers | Compile | −1.29 to +0.34% | −1.79 to +1.70% | 1.05 / 0.59% |
| JSON summary with helpers | Compile | −2.09 to +0.30% | −1.94 to +1.17% | 1.86 / 0.57% |
| Sparse generated validator | Compile to result | −1.16 to +1.53% | −2.70 to +0.16% | 1.18 / 1.52% |
| Configuration manifest | Compile to result | −2.05 to −0.04% | −3.06 to +2.25% | 0.69 / 1.35% |

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November
24, 2026 UTC. Two workers per source warm for eight seconds and provide
seven rotated batches. Ranges cover all pairings of worker medians.
Compile includes preceding-code cleanup. Compile to result adds
execution, mediated RAM-backed I/O, output verification and disposal of
compiled code and the previous result. Wider process CPU includes fixture
reset and request handling. Process startup and final teardown are outside
the elapsed clocks.

The temporary source probes isolate the removed work. Invalidation visits
fall from 1,170 to 14 bindings on the sparse validator and from 178 to 14
on the manifest. The latter also skips 21 deferred-scope binding visits.
The mixed shared-graph control drops from 1,424 widening calls to 17.
It no longer runs out of its existing widening guard on immutable
subgraphs. Median CLR compilation allocation falls 6,240 bytes there
and 3,168 bytes on the immutable graph. Allocation and retained
admission match on all eight representative sources. Retain the scan and
diagnostic correction. CPU comparisons span zero on all timed scripts.

Windows and Ubuntu each pass 285 focused Release checks, including
admission and cancellation. The optional-tuple checks preserve mutable
sibling widening across joins. The only diagnostic differences in 19
paired probes are two statically known `len(int)` errors that previously
escaped checking after unrelated mutations. Every compiled owner refunds
its allowance. Both exact 32-file images pass four complete CPython 3.13.2
goldens. All 168 timed batches, both quiet gates, joined cleanup and the
persistent archive verify. VM staging reused the current measured worker
image and losslessly compressed the earlier sealed reference core.

[Timings](summary.json) · [Work counts](graph-counts.json) ·
[Allocation](allocation-summary.json) · [Images](identity.json) ·
[Quiet observations](observations.tar.xz) · [Probes and patches](procedures.tar.xz) ·
[Storage custody](storage-custody.json) · [Terminal status](terminal-status.json).
