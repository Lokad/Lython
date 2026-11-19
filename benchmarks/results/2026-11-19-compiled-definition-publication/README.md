# B1082 compiled definition publication

Prepared definitions now produce a value for the executable's normal
binding instruction. The compiler selects function-scope local, global
and nonlocal stores once. The module store keeps the live namespace and
its slots in sync. Both decorator completion and synchronous construction
publish through the same path. The exact tape probe shows a forwarded
local store for every ordinary, annotated and captured rule definition.

The measured pair is `896e0c40a` versus private `83200c620`, retained as
`44fe04178` with identical runtime and test sources. Pinned local warm
allocation and accounted peaks are effectively equal. Local compilation
allocates 0.04–1.14% more across these workloads. The archive preserves
the allocation counts and both versions of the relevant tape shapes.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-19 UTC.
Ranges cover four pairs of duplicate process medians. Negative values
favor the candidate.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary rules | Warm | −0.76…+0.36% | −3.48…+0.52% |
| Ordinary rules | Compile-to-result | −1.84…+0.11% | −2.36…−0.52% |
| Annotated rules | Warm | −6.34…+0.18% | −5.24…+0.35% |
| Annotated rules | Compile-to-result | −3.53…−1.57% | −1.90…−0.83% |
| Captured rules | Warm | −5.51…−1.00% | −5.95…−1.82% |
| Retained nonlocal siblings | Warm | −5.71…−0.69% | −7.31…+0.71% |
| Large positional calls | Warm | −6.73…+0.05% | −7.62…+1.58% |
| Registration report | Warm | −3.69…−0.52% | −4.58…−0.84% |
| Expanded forwarding | Compile-to-result | +0.14…+1.34% | −1.96…+2.68% |

The compiled store removes name classification from definition completion.
The annotated compile-to-result, captured warm and registration-report
lanes improve with supporting CPU. Annotated warm reference spread is
5.35% and the positional reference spread is 5.62%. Treat those elapsed
estimates as uncertain. Forwarding is slower in all four elapsed pairs with
mixed CPU. Its unrelated runtime path and slightly greater compilation
allocation leave the cause open.

Windows .NET 11 and Ubuntu .NET 10 each passed 925 focused Release cases,
including old global and nonlocal names read by a decorator during real
host I/O. Eight additional source probes match CPython through both APIs.
Both complete 32-file images match seven CPython goldens and release
their allowances. Both quiet gates and all 180 timed batches passed in
410.91 seconds. Workers joined, fixtures were removed and the persistent
archive verified under the lease.

Warm includes mediated I/O, goldens and previous-result cleanup.
Compile-to-result adds compilation and compiled-owner disposal. Process
CPU measures a wider boundary. CPython is the correctness reference for
this diagnostic source pair.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Checks and procedures](checks-and-procedures.tar.xz).
