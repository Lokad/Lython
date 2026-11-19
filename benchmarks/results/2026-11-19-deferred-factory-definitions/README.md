# B1079 factory definitions without invocation frames

The candidate constructs eligible local functions and lambdas using the
existing executable slots and the definition-owned cache. The prepared
dispatcher serves both borrowed and owning executions. Scope-dependent
definitions and suspended evaluation use the owning frame. The pair is
`aa03ef5bc` versus private `9d1538787`, retained as `1c9a8e6a1` with
matching runtime and test sources.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-19 UTC.
Ranges cover the four pairs of duplicate process medians. Negative values
favor the candidate.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary rules | Warm | −6.35…−2.91% | −6.25…−2.86% |
| Ordinary rules | Compile-to-result | −5.92…−3.79% | −5.91…−3.97% |
| Annotated rules | Warm | −9.84…−8.49% | −10.53…−7.60% |
| Annotated rules | Compile-to-result | −8.29…−7.11% | −8.82…−7.97% |
| Captured rules | Warm | −3.61…+1.71% | −1.80…+2.53% |
| Retained nonlocal siblings | Warm | −2.78…−2.57% | −3.10…−1.93% |
| Large positional calls | Warm | −0.85…+1.25% | −2.37…+1.58% |
| CSV invoice | Warm | −0.97…+2.03% | +0.64…+1.37% |
| Expanded forwarding | Compile-to-result | −1.70…+3.35% | −0.19…+4.72% |

Pinned local warm allocation falls by exactly 884,736 bytes on each rule
target, or 23.71% for ordinary rules and 33.36% for annotated rules. The
reduction matches 2,048 lexical contexts at 432 bytes each. Accounted peaks
match, and all seven workloads refund their shared allowance after cleanup.
Captured-rule reference spread is 3.56%. Forwarding candidate spread is
3.77%. Keep their elapsed and CPU observations open. CSV CPU increases
across all four pairs, with a small mixed elapsed result.

Windows .NET 11 and Ubuntu .NET 10 each passed 819 focused Release cases.
These include a held host write during default evaluation, cancellation
and refunds. The archive preserves the initial fixture staging failures
and the same subprocess checks after correction. Both frozen 32-file
images matched seven CPython output and file goldens. Both quiet gates and
all 180 timed batches passed in 411.14 seconds. All workers joined, guest
fixtures were removed and the archive verified under the lease.

Warm includes mediated I/O, goldens and previous-result cleanup.
Compile-to-result adds compilation and compiled-owner disposal. Process
CPU measures a wider boundary. CPython was used for correctness here.
[Earlier CPython timing checkpoint](../2026-11-19-retained-definitions-cpython/README.md).

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Checks and procedures](checks-and-procedures.tar.xz).
