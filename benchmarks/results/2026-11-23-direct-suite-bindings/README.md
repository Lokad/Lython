# B1427 Collect dynamic suite bindings in their final set

Module and class suites collect membership directly into the set
used by lexical validation. Their stages no longer grow a binding
list, compact it or preserve an ordered copy. Executable slot
assignment continues to follow emission in these scopes. Functions
and generators keep ordered lexical bindings.

Compared `9e3b826df` and `2aa3c25e8` on the quiet Azure F4as_v7,
Ubuntu 24.04.4 and .NET 11 RC3 on November 23, 2026 UTC. The reference
matches the production and test source of `60572c961`. Only the
core differs across the two 32-file images. `3b69a7a1c` retains the
exact measured frontend patch.

| Complete script | Boundary | Elapsed change | Wider process CPU change |
| --- | --- | ---: | ---: |
| Inherited dataclass configuration | Compile | −0.18 to +0.03% | −0.68 to +1.66% |
| Small configuration edit | Compile | −1.77 to +0.76% | −2.05 to +0.79% |
| Sparse generated validator | Compile | −1.20 to +1.25% | −2.04 to +1.86% |
| Dense generated read-only branches | Compile | −2.57 to +0.62% | −2.94 to +1.28% |
| Inherited dataclass configuration | Compile to result | −0.39 to +2.26% | +0.28 to +1.42% |
| Sparse generated validator | Compile to result | −1.42 to +1.62% | −2.48 to −0.49% |

Ranges span pairings of medians from two independent workers per
source. Workers warm for eight seconds and complete seven rotated
batches. Compilation includes code-owner disposal. Time to result
adds execution, applicable mediated RAM-backed I/O, verification
and previous-result disposal. Wider CPU includes resets and protocol.
Startup and final teardown stay outside elapsed.

Retain the removed storage and compaction pass. The dataclass
allocates 1.41% less in the local compilation diagnostic. Most
other allocation changes are small. Sparse validator allocation
varies across samples and its last sample is 0.18% higher. The
quiet elapsed ranges all span zero. Carry the opposing dataclass
and sparse validator first-result CPU changes. The two local
dataclass time-to-result directions disagree. The first local run
stopped at the short configuration warmup gate, and the rerun
allowed more warmup rotations with the same required duration.
Both conditions are preserved.

Windows .NET 11 and Ubuntu .NET 10 each pass 40 white-box and 615
public focused Release checks. Twelve program inspections match
the executable tape, operand layout and source locations. All
240 diagnostic and retained-balance comparisons agree. The initial
two images verify seven CPython goldens. Both exact quiet catalogs
verify the four timed scripts separately. Compiled owners return
their allowance.

All 168 timed batches and both quiet gates pass in 326 seconds.
Workers join, fixtures are removed and the persistent archive
verifies under the lease. Staging retires only the previous factory
working cores whose exact bytes and observations are already
archived. No CPython elapsed ratio was collected.

[Sources and images](source.json) · [Timings](summary.json) ·
[Local allocation](local-allocation.json) · [Observations](observations.tgz) ·
[Checks and procedures](procedures.tar.xz) · [Archive receipt](archive-receipt.json) ·
[Reference core](../2026-11-22-csv-literal-field-scan/candidate-core.dll.gz).
