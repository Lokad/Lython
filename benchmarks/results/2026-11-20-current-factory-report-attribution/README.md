# B1204 Current factory and report CPU attribution

Profiled Lython `50d63cf31` with the source-matched `32c5f2ef7` image
on the quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3, on
2026-11-20 UTC. Workers warmed for at least eight seconds before
five-second sampled batches. The sampling boundary includes worker
framing, fixture reset, goldens and prior-result cleanup.

| Warm workload | All samples | Runtime ancestry | Unknown leaf | Selected nearest runtime frames |
| --- | ---: | ---: | ---: | --- |
| Eager annotated rules | 1,059 | 1,046 | 84 | Native instruction loop 257, general prefix 149, reservation 87, prepared definition dispatch 60 |
| Expanded forwarding | 999 | 945 | 150 | General prefix 142, positional text join 69, split result construction 68, reservation 50, keyword preparation 48, trimmed split entry 45 |
| Named shipment | 1,266 | 1,166 | 237 | Template append continuation 115, general prefix 107, reservation 68, append formatted value 54, JSON string reading 52, JSON construction 44, integer formatting 42 |

Named shipment has 380 formatting ancestry samples. Inlining can shift
attribution and the groups overlap. Examine synchronous field traversal and the point
where a callback needs suspension. The split and keyword samples show
different remaining work in forwarding. Eager factory activity still
spans instruction dispatch, admission and construction. These counts
locate work for inspection. They provide no timing comparison or
estimate of removable CPU.

The original collection completed the factory and forwarding profiles.
The combined profiling window inherited from the two-case procedure
interrupted the third profile before symbol export and post-capture
verification. Its partial raw trace and failure are preserved. The
named shipment ran separately within the original limit, using the
same frozen image and verified catalog. Both collections passed quiet
gates, joined workers and archived trace bytes before guest fixture
cleanup. Each completed profile passed output and file checks before
and after collection.

[Attribution](attribution.json) · [Original observations](initial/observations.tgz) ·
[Named shipment observations](named-report/observations.tgz) ·
[Source identity and procedures](procedures.tar.xz).
