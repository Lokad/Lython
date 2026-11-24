# B1581 Key regex wrappers by requested Python flags

`62a0df6b8` carries the measured source from `dd122b984`. The reference core is the B1580 candidate. Its production source matches `169e13894`.

A single execution-local cache now keys on UTF-8 pattern content and the
requested Python flags. The frontend retains normalized provider options
for compiled artifacts. The wrapper lookup runs guest hash and equality
before normalization and uses current execution context for callbacks.
Equality copies only execution-owned values across suspension. A subsequent
probe requires a live entry after purge or eviction.

| Complete script | Warm elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Compiled log scan | −1.79 to −0.25% | −1.14 to +0.00% | 1.30 / 0.27% |
| Saved bound log scan | −1.71 to +0.67% | −1.96 to +0.00% | 1.32 / 1.08% |
| Module log scan | −2.54 to −2.00% | −3.71 to −0.71% | 0.27 / 0.29% |
| Quoted CSV invoice | −0.68 to +2.80% | −1.96 to +2.63% | 0.68 / 2.80% |

Retain the corrected cache. The module scan takes 2.00–2.54% less warm elapsed with lower wider CPU. The direct compiled scan also finishes sooner with a smaller difference. Saved-bound and quoted-invoice elapsed span zero.
Local CLR allocation differences are -48 B, -42 B, -48 B, +0 B in table order.
All admitted peaks match. Six local warm invocations per case and image
restore their allowances to baseline.

Windows and Ubuntu each pass 73 white-box and 520 public .NET 11 Release
regex checks. CPython 3.13.2 independently matches the identity, cache-hit
normalization, retained wrapper, reentry and raising-hash output. The
public checks cover awaited cleanup and terminal cancellation or refusal
before provider work.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24,
2026 UTC. Both 32-file images match four CPython 3.13.16 output goldens
and differ only in the core DLL. Two workers per image warm for eight
seconds and provide seven rotated batches each. Ranges span their four
median pairings. Warm elapsed includes applicable mediated RAM-file I/O,
golden checks and previous-result disposal. Wider CPU includes request
framing and fixture reset. Compilation and process startup are outside
both measured phases.

All 112 batches complete in 212 seconds. Both quiet gates, worker
joins, guest fixture cleanup and persistent archive verification pass.
An initial staging check stopped before launch because its free-space
margin exceeded the available disk. The replay verified the same images
and preserved ample capacity for the four lanes and durable observations.

[Timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Reference core](../2026-11-24-decoded-ascii-file-values/candidate-core.dll.gz) ·
[Raw observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal status](terminal-status.json).
