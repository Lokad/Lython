# B1073 direct literal record construction

Private `6e3f2d2e5` proves literal string keys are distinct during
compilation. It fills an admitted record directly, including the first row.
Duplicate literals use the ordinary dictionary operation. This removes the
literal path's first-row key transfer and per-value key checks. The reference
is `12fabffb1`.

Windows .NET 11 and WSL .NET 10 passed 58 focused Release cases each. The
extended duplicate-key check passed both execution APIs, with suspended
value evaluation and unchanged key order. Both complete 32-file images
passed four CPython goldens. Warm JSON allocation and accounted peaks
were effectively equal. Repeated local public compilation allocation rose
0.17% on JSON and 0.24% on manifest. Both images returned the budget to baseline.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-19 UTC.
Ranges cover four combinations of duplicate process medians. Negative
values favor the prototype.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| JSON record transform | Warm | −1.64…+0.29% | −1.15…+1.59% |
| JSON record transform | Compile-to-result | −3.43…−0.07% | −3.97…+1.18% |
| Configuration manifest | Compilation | −0.60…+2.92% | −1.28…+4.90% |
| Configuration manifest | Compile-to-result | +0.21…+1.77% | −0.68…+2.52% |
| Expanded forwarding | Warm | −1.51…+1.69% | −1.57…+1.59% |
| CSV invoice | Warm | −0.32…+0.09% | −1.33…+0.64% |

JSON compile-to-result elapsed improves, with mixed CPU and a 2.86%
same-source reference spread. The manifest compile-to-result pairs all
slow down, with mixed CPU. Keep the prototype private. The new compiler
proof and the duplicate-literal route have costs. Reopen this work if a
layout can eliminate the first-row transfer and avoid the proof on the
compile path.

Both quiet gates and all 120 timed goldens passed in 277.35 seconds.
Workers joined, guest fixtures were removed and the archive verified
under the lease. Warm includes mediated I/O, goldens and previous-result
cleanup. Compile-to-result adds compilation and compiled-owner disposal.
Process CPU observes a wider boundary.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Checks and procedures](checks-and-procedures.tar.xz) ·
[Local compilation allocation](compile-allocation.json).
