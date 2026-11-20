# B1181 annotation bounds from lexical preparation

Private `97c7d766c` records the last annotation check's source position in
the module, function or class. The annotation analyzer uses that position
to select its required prefix. This removes the separate statement walk
and body-to-boundary dictionary. Branches still export complete bindings,
and regex imports still use conservative full analysis.

| Workload | Boundary | Windows compile allocation | Quiet elapsed change | Quiet process CPU change |
| --- | --- | ---: | ---: | ---: |
| Annotated rules | Compilation | −0.77% | −3.35% to −2.38% | −2.11% to −1.50% |
| Annotated rules | Compile-to-result | — | +0.03% to +1.28% | +0.30% to +3.65% |
| Generated validator | Compilation | +0.00% | −1.39% to +3.71% | −0.84% to +4.57% |
| Configuration manifest | Compilation | +0.04% | — | — |
| CSV invoice | Compilation | +0.01% | — | — |

The reference is frozen `289380214`, whose production, test and worker
sources match `c3684d4d1`. The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and
.NET 11 RC3 on November 20, 2026 UTC. Compilation includes disposal of
its owner. Compile-to-result adds guest execution, golden verification
and prior-result disposal. Process CPU covers the wider batch. Ranges
cross two independent worker medians per revision.

Keep this design private. The script compiles faster, while all
compile-to-result pairings use more CPU. The earlier parser variant
showed the same adverse direction. The validator's 3.21% reference spread
leaves the size of its CPU change uncertain. This experiment does not
isolate a cause for the end-to-end increase.

Both complete 32-file images matched four CPython goldens. Windows .NET 11
and Ubuntu .NET 10 each passed 682 focused Release checks. Thirteen
additional source-pair probes matched ordered diagnostics, failures and
output in synchronous and asynchronous execution.

The first preflight saw only 58,843,136 bytes available on the VM and
stopped before staging. The successful run retired 22 exact binary
replicas after verifying their contents against committed Git blobs and
checking every inode alias under the lease. The published artifacts and
original observations remain available. All 60 timed batches and both
quiet checks passed, users joined and the archive was verified.

[Summary](summary.json) · [Allocation](compile-allocation-summary.json) ·
[Observations](observations.tgz) · [Checks and procedures](checks-and-procedures.tar.xz) ·
[Earlier B1139](../2026-11-19-parser-annotation-bounds/README.md).
