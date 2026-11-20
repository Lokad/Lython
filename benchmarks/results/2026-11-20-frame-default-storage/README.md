# B1164 frame-owned function defaults

Compared `a5214a158` with private `e9e46dbf9` on the quiet Azure F4as_v7
with Ubuntu 24.04.4 and .NET 11 RC3 on November 20, 2026 UTC. The reference
uses the B1163 candidate core, whose production and test sources match
the delivered revision. Both 32-file images matched five CPython goldens.

| Workload | Phase | Elapsed change | Process CPU change | Local allocation change |
| --- | --- | ---: | ---: | ---: |
| Ordinary rule closures | Warm | −0.98…+2.57% | +1.72…+2.88% | −11.24% |
| Eager annotated rules | Warm | −4.38…+5.38% | −0.28…+5.94% | −9.23% |
| Nested annotated factories | Warm | −1.99…+2.59% | −3.15…+2.00% | −6.74% |
| Computed defaults | Warm | −1.74…+7.07% | −1.87…+7.82% | −0.04% |
| Positional CSV invoice | Warm | −4.54…+0.69% | −5.19…+0.65% | 0.00% |
| Ordinary rule closures | Compile-to-result | −1.11…−0.30% | −1.33…−0.43% | — |

Ranges cover all pairings of two process medians per revision. Warm
includes mediated I/O, golden checks and prior-result disposal.
Compile-to-result adds compilation and code-owner disposal. Startup,
fixture reset and final teardown are outside guest elapsed. Process CPU
includes wider batch work. Eager and computed-default candidate warm
spreads are 7.39% and 8.32%, respectively. The unchanged invoice reference
spread is 3.77%.

The prototype removes `PendingFunctionDefaults`. The executable frame holds
the final admitted tuple and dictionary directly through evaluation and
suspension. Lowered definitions use the same final containers in their
existing locals. Slot retirement refunds unpublished default backing.
Publication transfers the containers before a decorator can expose the
function. All local runs returned the shared allowance to baseline. Eager
and nested peak estimates rose by 24 bytes. Other peaks matched.

Keep this revision private. Allocation falls and the ordinary fresh script
has a small gain. Its warm CPU increases and the other warm comparisons
are inconclusive. A refinement can restore the native prefix's existing
cleanup path, since default construction transfers to the ordinary frame
before creating these containers. Any repeat should test that changed path
and retain the original adverse results.

Windows .NET 11 and Ubuntu .NET 10 each passed 2,747 focused Release
checks, including generator suspension, cancellation, live defaults,
failure before publication and retained decorator aliases. All 120 timed
batches and both quiet gates passed. Workers joined and fixtures were
removed under the archive lease.

[Summary](summary.json) · [Observations](observations.tgz) · [Archive receipt](archive-receipt.json) ·
[Images, source and checks](checks-and-procedures.tar.xz) ·
[Measured candidate core](measured-core.dll.gz) ·
[Measured reference core](../2026-11-20-owned-annotation-values/measured-core.dll.gz).
