# B1139 annotation analysis bounds during parsing

R708 records the last annotation-dependent statement as the parser builds
each module, function and class suite. This removes a later syntax walk
and a temporary body-to-boundary map. Branches still export their complete
binding state. Scripts using regex keep the complete binding analysis.

The reference is `e20604ef4` and the private prototype is `00c4b3a2c`.
Windows .NET 11 and Ubuntu .NET 10 each passed 725 public and 39 white-box
focused Release checks. Two new checks cover annotated returns and dead
branches in one-line suites. Both also pass on the frozen reference.
The initial out-of-tree test runs used the wrong fixture locations or
subprocess host. The archived reruns use matching fixtures, worker hashes
and the pinned runtime. Both complete 32-file images matched five CPython
goldens and restored the borrowed budget in the warm allocation check.
The local compile allocation fell 0.75% for annotated rules and 0.54%
for CSV helpers, with changes of at most 0.06% on the other cases.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 19,
2026 UTC. Compilation includes compiled-owner disposal. Warm includes
mediated RAM-backed I/O, output and file checks and prior-result disposal.
Compile-to-result combines both boundaries. Process CPU covers the full
batch. Ranges compare all four pairs of duplicate process medians.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Annotated rules | Compilation | −2.41…−0.43% | −1.79…+0.43% |
| Generated validator | Compilation | +0.14…+6.94% | +1.23…+6.98% |
| Configuration manifest | Compilation | −1.15…+1.48% | −1.81…+1.54% |
| CSV helpers | Compilation | −0.35…+0.76% | −0.31…+3.05% |
| CSV invoice | Warm | −0.65…+0.34% | −1.96…+2.00% |
| Annotated rules | Compile-to-result | +0.88…+4.46% | +0.34…+4.58% |

Keep the prototype private. The allocation reduction is small, and
annotated compile-to-result uses more CPU and takes longer. Validator
duplicate candidate medians spread by 5.12%, so the size of its slowdown
is uncertain. A future design needs to remove the annotation walk without
adding bookkeeping to every parsed statement. The experiment identifies
no causal fraction for the observed changes.

The first local preflight referenced the wrong replica receipt and did
not stage a run. The corrected comparison completed all 120 batches.
Both quiet gates passed, workers joined, fixtures were removed, and the
persistent archive was verified under the lease. The reference core is the
[B1135 measured core](../2026-11-19-native-integer-transport/measured-core.dll.gz).

[Summary](summary.json) · [Observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Source and checks](checks-and-procedures.tar.xz).
