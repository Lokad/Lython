# B1163 funded function annotation values

Compared the `e6e1f964c` runtime with private `cf8b7ab63`, delivered at
`cb08bb8a2`. Both complete 32-file worker images matched five CPython
goldens and returned the shared allowance to baseline. Timings compare
Lython revisions on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11
RC3 on November 20, 2026 UTC.

| Workload | Phase | Elapsed change | Process CPU change | Local allocation change |
| --- | --- | ---: | ---: | ---: |
| Eager annotated rules | Warm | −4.67…+0.97% | −2.96…+2.02% | −13.65% |
| Eager rules, annotations read each definition | Warm | −1.60…+2.02% | −2.20…+3.53% | 0.00% |
| Nested annotated factories | Warm | −7.55…−7.08% | −7.17…−4.95% | −10.35% |
| Unannotated rules | Warm | −1.11…+2.40% | −1.83…+3.70% | 0.00% |
| Positional CSV invoice | Warm | −2.40…−0.76% | −2.68…−1.32% | <0.01% |
| Eager annotated rules | Compile-to-result | −4.10…+0.82% | −4.33…+0.92% | — |

Each timing range covers all pairings of two process medians per revision.
Warm includes mediated file I/O, golden checks and prior-result disposal.
Compile-to-result adds compilation and code-owner disposal. Startup, reset
and final teardown are outside guest elapsed. Process CPU uses a wider
request boundary. Accounted peaks were identical in the allocation runs.

The prepared execution path evaluates annotations eagerly and admits the
final value array and dictionary shell together. The existing function
metadata field holds the array directly. First observation adopts it into a
normal mutable dictionary with the same key layout and owner. Replacing
unpublished values refunds the private backing. Once exposed, dictionary
aliases remain funded. This removes eager shell construction for unread
annotations, with no wrapper allocation on the observed path.

Retained for the nested factory gain and lower allocation. Eager elapsed
and CPU, including the read-every-definition control, span zero. The
unannotated control also spans zero, and the unchanged invoice moves
favorably. Those results limit attribution outside the nested workload.
The [earlier adverse prototype](../2026-11-19-retained-annotation-values/README.md)
used an extra wrapper and remains recorded.

Windows .NET 11 and Ubuntu .NET 10 passed 1,289 focused Release checks each.
The tests cover first access at full allowance, independent dictionary
identity, retained aliases, private refund and cancellation. Initial setup
omitted sparse-checkout benchmark scripts, and the first Ubuntu public run
resolved a subprocess fixture from the base checkout. Restoring the scripts
and running the prototype fixture from the standard output location resolved
those setup failures. Both quiet gates and all 120 timed batches passed.
Workers joined and fixtures were removed before persistent archive verification.

[Summary](summary.json) · [Observations](observations.tgz) · [Archive receipt](archive-receipt.json) ·
[Images, source and validation procedure](checks-and-procedures.tar.xz) ·
[Measured candidate core](measured-core.dll.gz) ·
[Measured reference core](../2026-11-20-representative-cpython/measured-core.dll.gz).
