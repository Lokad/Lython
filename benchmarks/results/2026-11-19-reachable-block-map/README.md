# B1080 compact reachable block map

The compiler retains reachable blocks in original order within the builder's
existing list. One map serves reachability, target indexes and range endpoints.
This removes the ordered-index array, the normalized list and range binary
searches. Control-flow traversal, operand preparation and final tape
linearization keep their existing roles.

The measured pair is `aa03ef5bc` versus private `adbbc2396`. The two compiler
files match retained `188cdf6ff`, which also includes the separately measured
factory change. Final instruction fields, nested exception regions and slot
capacities match for the four workload scripts and a nested protected-control
probe. Local compile allocation falls 0.18–0.52% and all owners refund.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-19 UTC.
Ranges cover four pairs of duplicate process medians. Negative values
favor the candidate.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Configuration manifest | Compilation | −1.96…+0.40% | −3.55…−0.30% |
| Configuration manifest | Compile-to-result | −0.93…−0.48% | −1.75…0.00% |
| Generated validator | Compilation | −1.27…+0.93% | −3.03…+2.00% |
| Generated validator | Compile-to-result | −2.80…−0.46% | −2.69…+1.09% |
| Shipment format selectors | Warm | −1.14…+0.77% | −3.82…+1.55% |

The candidate stays in the compiler because it removes staging storage and
range searches, preserves the emitted tape, and improves the two
compile-to-result elapsed lanes in this diagnostic pair. Matching
manifest compilation uses less CPU. Validator CPU and compile-only elapsed
are mixed. The already archived B1068 comparison combined distinct passes
and showed adverse validator elapsed and CPU. B1080 retains that evidence
and isolates the block map. [B1068](../2026-11-19-combined-operand-preparation/README.md).

Windows .NET 11 and Ubuntu .NET 10 each passed 363 focused Release cases.
The original Windows subprocess log and the rerun using the matching probe
and pinned SDK are included. Both complete worker images matched four
CPython goldens. Both quiet gates and all 100 timed batches passed in
231.98 seconds. Workers joined, fixtures were removed and the archive
verified under the lease.

The combined interpreter and compiler at `188cdf6ff` passed 371 focused
Release cases on each target. Its complete image matched 11 CPython
goldens. The archive includes those distinct combined checks.

Compilation includes compiled-owner disposal. Compile-to-result includes
compile, execution, mediated I/O, goldens and owner cleanup. Warm excludes
compilation and includes prior-result cleanup. Process CPU covers a wider
boundary.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Checks and procedures](checks-and-procedures.tar.xz).
