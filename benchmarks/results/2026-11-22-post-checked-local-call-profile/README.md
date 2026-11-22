# B1366 Call costs after direct local reuse

Profiled delivered `3d88c8095` on November 22, 2026 UTC on the quiet
Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. The 32-file image
was built at `638fc1138` with identical production and test source.
Each script warms independently before Linux perf samples all
process threads at 199 Hz. Warm runs include applicable RAM-backed
mediated I/O, output verification and prior-result disposal.

| Closest resolved runtime method | Function-scoped helper | Helper-driven CSV invoice |
| --- | ---: | ---: |
| All process samples | 1,096 | 1,528 |
| Unmapped leaf | 156 | 155 |
| Native instruction core | 539 | 200 |
| Ordinary instruction prefix | 2 | 155 |
| Native call operation | 103 | 79 |
| Inline function entry | 37 | 58 |
| Native frame continuation creation | 0 | 31 |
| Tuple construction from slots | 0 | 53 |
| Memory reservation | 0 | 54 |

The supplied helper spends most of its sampled time in the native
runner, where useful arithmetic and dispatch overlap. The invoice
shows additional scope promotion and tuple traffic. Of its 54
samples closest to memory reservation, 27 resolve to tuple
construction. That count includes both temporary tuples and the
per-category tuples retained by the invoice. It does not estimate
how much CPU a change would save.

The invoice tape shows that `accumulate` starts in a small native
activation, calls `price_line` immediately before a fixed four-value
unpack and later writes a dictionary subscript. Native-to-native
calls do not currently supply the existing fixed-unpack destination,
and the native caller transfers at `UnpackValues`. The fixed-unpack
destination and the existing non-growing dictionary mutation together
offer a way to remove the temporary tuple and avoid frame creation on
established keys. Preserve ordinary growth and protocol transfer.

Both CPython goldens and both quiet gates pass. Every worker joins
and raw traces are verified in persistent storage before releasing
the RAM fixture mount. Staging retires only an old archived image's
identical working copies.

[Attribution](attribution.json) · [Raw observations](observations.tgz) ·
[Verification and procedures](procedures.tar.xz) ·
[Delivered local reuse](../2026-11-22-checked-augmented-local/README.md).
