# B1588 One eager JSON builder owner

Production `9e3829615` retains the runtime source measured at `af717101d`.
Eager JSON now uses the shared iterator's builder through traversal and
publishes the string in the same operation. This removes a serializer wrapper,
the default-result wrapper and repeated builder arguments. Traversal scratch
is refunded before metadata admission. The builder stays funded through
pending hooks and native writes.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 with .NET 11 RC3 on November 24,
2026 UTC. The 32-file images at `ccdf43a59` and `af717101d` differ only in
the core DLL. Both verify five CPython 3.13.16 goldens. Two workers per
image warm for eight seconds and supply seven rotated batches. Ranges are
across pairings of worker medians. Warm elapsed includes mediated RAM-backed
I/O, output verification and previous-result disposal. Wider CPU also
includes reset and protocol. Compilation, startup and final teardown sit
outside these clocks.

| Warm script | Elapsed change | Wider process CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| JSON Lines, expanded options | −2.57 to −1.00% | −3.09 to −1.03% | 1.15 / 0.46% |
| JSON Lines, default options | −2.75 to −0.65% | −2.40 to −0.63% | 1.59 / 0.56% |
| JSON Lines, heavy escaping | −2.05 to +0.68% | −1.96 to +0.55% | 1.92 / 0.85% |
| Short text report control | −5.92 to +7.59% | −5.82 to +7.22% | 7.85 / 6.04% |
| Quoted CSV invoice control | −2.19 to −0.16% | −1.27 to −0.58% | 1.41 / 0.66% |

Retain the ownership simplification. Expanded and default options improve on
elapsed and CPU. Escape-heavy timing is inconclusive. The moving CSV control
limits attribution of the modest gains, and high worker spread leaves the
text control inconclusive. Local CLR allocation is effectively flat, all
admitted peaks match and each run returns its allowance.

Windows and Ubuntu each pass 1,210 focused Release checks, including
incremental chunk order, callback unwind, terminal refusal and budget reuse.
All 140 timed batches pass with both quiet gates, joined workers, retired
fixtures and persistent archive verification. Staging reused the exact
frozen B1586 reference and retired only the verified archived B1576 core
copy.

[Timing and spread](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identities](identity.json) · [Reference core](../2026-11-24-json-traversal/candidate-core.dll.gz) ·
[Observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal status](terminal-status.json).
