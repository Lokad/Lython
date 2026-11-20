# B1199 Class-cell capture during lexical preparation

Compares `a4d11c189` with `537b4b3dd` on the quiet Azure F4as_v7,
Ubuntu 24.04.4 and .NET 11 RC3, on 2026-11-20 UTC. Each source used
two independent processes with at least eight seconds of warmup. Five
rotated rounds targeted 0.4-second requests. Ranges cover the four
pairings of process medians. Negative values favor the candidate.

| Workload | Boundary | Elapsed change | Process CPU change | Duplicate elapsed spreads, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Inherited dataclass configuration | Compilation | −10.61% to −6.92% | −10.64% to −7.75% | 1.98% / 2.11% |
| Inherited dataclass configuration | Compile to result | −4.99% to −0.09% | −2.81% to +1.40% | 3.03% / 2.06% |
| Generated validator | Compile to result | −5.88% to +0.55% | −4.83% to +0.51% | 2.18% / 4.55% |
| Configuration manifest | Compile to result | −0.23% to +0.56% | −1.13% to +1.27% | 0.28% / 0.51% |
| Eager annotated rules | Warm control | −5.20% to +0.07% | −7.02% to −0.72% | 3.67% / 1.82% |

Compilation includes compiled-owner disposal. Compile to result adds
execution, mediated RAM-backed I/O where used, goldens, and prior-result
cleanup. Warm measures the latter boundary. Fixture reset, process startup
and final teardown are outside guest elapsed timing. Process CPU covers
the request loop. CPython supplies output and semantic checks here.

The lexical visit records cell demand while seeing reads and nonlocal
directives. It distinguishes a class's own method cell from its suite's
enclosing cell. This removes separate body and expression scans, their
fallback in scope fact lookup, and the read iterator that linked the scans.
Postponed function annotations keep their source and prepared text
without lowering unevaluated syntax.

Local compile allocation falls 11.10% for dataclasses, 3.37% for the
validator, 2.15% for eager rules, 2.12% for invoice and 1.84% for the
manifest. Dataclass compilation now allocates 597,952 bytes, or 6.20%
less than the 637,504-byte pre-class-engine reference from B1195.
Warm allocation and accounted peaks match in all five cases. Cleanup
returns the shared allowance to baseline.

Retain the consolidated walk. Dataclass compile elapsed and CPU both
fall. Its compile-to-result elapsed favors the candidate in this
comparison, with mixed CPU. Validator and manifest ranges span zero.
The unchanged eager warm workload uses less CPU in the sampled
processes but its elapsed range spans zero. Its movement limits
attribution of small effects elsewhere.

Windows .NET 11 and Ubuntu .NET 10 each passed 4,889 white-box and 2,126
focused public Release checks. Both complete 32-file worker images
passed five workload goldens. The candidate matched 23 CPython probes
in both modes on Windows. Six differ on the reference, covering lazy
aliases, a lazy type bound, two comprehension boundaries and a match
pattern. The archive includes the first probe result before pattern
coverage and the Windows selection that omitted copied fixtures.
Both quiet gates and 100 timed batches passed. All workers joined and the
persistent archive verified under the VM lease.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Observations](observations.tgz) · [Source, traces and checks](checks-and-procedures.tar.xz) ·
[Class-engine comparison](../2026-11-20-executable-class-suites/refinement/README.md).
