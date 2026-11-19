# B1076 prepared definition dispatch confirmation

This run repeats the exact B1075 images, reference `773be8dc2` and candidate
`0cbe8f867`, with 12 seconds of warmup per process in place of 8. It
retains the ordinary, annotated and captured warm cases and the expanded
forwarding compile-to-result control. B1075 preserves the original adverse
ordinary and annotated observations and the remaining two lanes.
[Initial comparison](../2026-11-19-prepared-definition-dispatch/README.md).

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-19 UTC.
Ranges cover all four comparisons of duplicate process medians. Negative
values favor the candidate.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary rules | Warm | −2.87…−0.70% | −4.68…−3.06% |
| Annotated rules | Warm | −5.71…−0.28% | −4.94…−0.01% |
| Captured rules | Warm | −3.01…−0.16% | −3.12…+0.64% |
| Expanded forwarding | Compile-to-result | −1.97…+2.81% | −0.20…+3.75% |

Ordinary candidate spread fell from 5.03% to 0.80% and the reference spread
was 1.42%. Annotated reference spread was 3.96%, with a process IQR of up to
3.97%. This limits the annotated estimate. Captured warm elapsed fell in both
runs, with mixed CPU here. Two forwarding CPU pairs rose by more than 3.5%
with a 3.02% candidate elapsed spread. B1075 forwarding CPU spans zero.
Retained as `eb444b988` because the duplicate dispatch disappeared and the
more stable ordinary lane showed repeatable lower elapsed and CPU within the
confirmation. The annotated, captured and forwarding effects remain
diagnostic. Local allocation and accounted peaks matched in B1075.

Both quiet gates and all 80 timed goldens passed in 249.60 seconds. Workers
joined, fixtures were removed and the archive verified under the lease.
Warm includes mediated I/O, goldens and previous-result cleanup.
Compile-to-result adds compilation and compiled-owner disposal. Process CPU
observes a wider boundary. The exact B1075 source and tests match the retained
commit. B1075 contains the focused Windows .NET 11 and WSL .NET 10 checks and
five CPython goldens.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Checks and procedures](checks-and-procedures.tar.xz).
