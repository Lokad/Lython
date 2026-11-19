# B1056 enclosing compiler bindings

The private R625 prototype borrows existing name membership from the
active compiler parents. It removes flattened ancestor sets and their
copy pipelines. The source pair is `68a7fc465` versus `a3f2b44d7`.
The complete 32-file images and nine-workload catalog verified with
CPython goldens. Windows .NET 11 and WSL .NET 10 each passed 901 focused
Release checks.

Public compilation allocations fell 1.13–3.27% on the four rule scripts,
with allowance returning to baseline after every compiled-owner disposal.
Configuration and report controls moved by 0.14% or less. Warm allocation
and accounted peaks stayed effectively equal.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-19 UTC.
Ranges cover four pairs of process medians. Negative changes favor the
prototype.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Captured rules | Compilation | −5.59…−4.31% | −4.70…−3.52% |
| Captured rules | Compile-to-result | −3.81…−1.38% | −4.08…−1.37% |
| Ordinary rules | Compilation | −3.13…−0.20% | −3.59…−0.79% |
| Mutable counters | Compile-to-result | −1.69…+3.02% | −0.04…+3.65% |
| Generated validator | Compile-to-result | −1.12…+1.21% | −0.15…+0.55% |
| Configuration edit | Compile-to-result | −0.86…−0.28% | −2.48…+0.60% |
| Shipment selector report | Warm | +1.41…+2.37% | +3.54…+4.72% |

The prototype remains private. The captured-rules gains justify refining it,
and the stable adverse warm control needs confirmation. Mutable counters'
reference processes differ by 4.07% in elapsed. The initial design also scans
generic type-parameter names on each ancestor query. Reuse the frontend's
prepared type-parameter membership before judging the next candidate.

Both quiet gates and all 140 golden batches passed. Workers joined and guest
fixtures were removed. The post-archive free-space check failed after the
complete archive was written. A separate idle lease compared the archive to
the persisted observations, verified both worker images and checked prior
teardown. The original assertion and the archive recovery are retained.
Compilation includes compiled-owner disposal. Compile-to-result includes
execution, mediated I/O and cleanup. Warm includes mediated I/O, golden
checks and previous-result cleanup. CPU has a wider measurement boundary.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Original terminal state](original-terminal-status.json) ·
[Archive recovery](archive-recovery.json) ·
[Checks and procedures](checks-and-procedures.tar.xz).
