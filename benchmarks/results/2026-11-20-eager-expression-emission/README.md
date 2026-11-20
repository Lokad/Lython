# B1197 Direct executable expression emission

Measures the class-suite delivery `53d3b45d5` against `f025da786` on the
quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-20 UTC.
Each role used two independent processes and at least 10 seconds of
warmup per process. Five rotated rounds targeted 0.5-second requests.
Ranges cover the four pairings of process medians. Negative values favor
the candidate.

| Workload | Phase | Elapsed change | Process CPU change | Duplicate elapsed spreads, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Inherited dataclass configuration | Compilation | −1.69% to +1.50% | −1.08% to +2.28% | 2.92% / 0.33% |
| Inherited dataclass configuration | Compile to result | −2.15% to +1.66% | −2.36% to −0.64% | 1.35% / 2.51% |
| Generated validator | Compile to result | −6.82% to −0.61% | −6.07% to +1.62% | 4.54% / 2.03% |
| Configuration manifest | Compile to result | −3.50% to −1.61% | −1.97% to −1.33% | 1.39% / 0.57% |
| Eager annotated rules | Warm | −2.06% to +3.40% | −3.68% to +6.25% | 2.97% / 2.53% |

Compilation includes compiled-owner disposal. Compile to result adds
execution, mediated RAM-backed I/O, output and file checks, and previous-result
cleanup. Warm includes execution and the same result boundary. Process CPU
covers the request loop. Fixture reset, startup and final teardown are outside
guest elapsed timing. CPython here verifies semantics and output goldens.

The executable compiler emits ordinary expressions, defaults and eager
annotations from validated syntax. It removes their intermediate lowered
tree, the discarded lowered lambda body, the generator clause array and
a repeat comprehension binding walk. Runtime-evaluated type scopes and lazy
metadata still use lowered expressions where needed.

Local compile allocation falls 2.33% for dataclasses, 3.09% for eager rules,
5.13% for the validator, 5.37% for the manifest and 4.60% for invoice.
Dataclass compile allocation falls from 688,720 to 672,640 bytes. It remains
5.51% above the original 637,504-byte pre-class checkpoint. Warm allocation
is effectively flat, accounted peaks match, and cleanup returns the shared
allowance to baseline.

Retain the simpler compiler. Manifest compile-to-result improves on both
clocks. Validator elapsed improves across the sampled pairings and CPU
spans zero. Dataclass compile-to-result uses lower CPU with mixed elapsed.
Dataclass compile-only and eager warm remain inconclusive. B1195 records
the class-suite regression against the original reference.

Windows .NET 11 and Ubuntu .NET 10 each passed 4,889 ordinary white-box
and 2,255 focused public Release checks. Both full 32-file images passed
five workload goldens and 11 CPython probes in both execution modes.
These include annotation and default order, lazy nested callables, scope
and cleanup. Both quiet gates and all 100 timed batches passed. Workers
joined and the persistent archive verified under the lease.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Observations](observations.tgz) · [Source, checks and procedure](checks-and-procedures.tar.xz) ·
[Earlier class comparison](../2026-11-20-executable-class-suites/refinement/README.md).
