# B1423 Construct immediate definitions in their caller frame

The immediate lexical named-function path now uses its already-admitted
interpreter body, like the lambda path. Both the inline native runner
and the borrowed executable runner do this after default expressions
have run. Observable annotation reads, capture allocation and retirement
of the private operands retain their existing order. The normal framed
definition path retains its separate admission for nested guest work,
including decorators and deferred annotations. Actual invocation,
callback, continuation and managed stack admission remain in place.

Compared the `3b45fdfc7` production source with private `94deffb1c`
on the quiet Azure F4as_v7 running Ubuntu 24.04.4 and .NET 11 RC3
on November 22, 2026 UTC. The 32-file images differ only in the
core library. The measured patch is retained at `52d69d213`.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed worker spread old / new |
| --- | --- | ---: | ---: | ---: |
| Postponed nested factories | Warm | −2.54 to +3.16% | −2.56 to +3.27% | 3.61 / 2.16% |
| Postponed nested factories | Compile to result | −4.37 to +3.99% | −4.05 to +3.64% | 0.22 / 8.50% |
| Eager nested factories | Warm | −2.43 to +1.93% | −3.30 to +3.11% | 1.11 / 3.32% |
| Mixed literal and dynamic defaults | Warm | −3.03 to +1.83% | −3.10 to +2.87% | 4.82 / 0.19% |
| Factory observing defaults | Warm | −1.11 to +0.79% | −1.17 to +0.54% | 0.82 / 1.10% |
| Fully supplied helper | Warm | −1.39 to +0.65% | −1.77 to +1.66% | 1.28 / 0.78% |
| Quote-heavy CSV invoice | Warm | −0.18 to +1.13% | −1.47 to +2.35% | 0.40 / 0.91% |

Ranges span every pair of process medians after eight seconds of
warmup and seven rotated batches from each of two workers per source.
Warm includes execution, applicable mediated RAM-backed fixture I/O,
output verification and prior-result disposal. Time to result adds
compilation and compiled-owner disposal. Wider CPU includes reset
and the surrounding worker request. Startup and final teardown use
separate boundaries.

Retain the removed transient frame for the shared named-definition and
lambda rule. All elapsed ranges span zero. Tight batches within each
postponed compile-to-result worker coexist with a large spread between
workers. The data support no speedup or stable adverse effect there.
Carry the possible quote control cost alongside the factory uncertainty.
Local CLR allocation is effectively flat and all admitted peaks match.
The paired recording allowance confirms the same counts on four
factory shapes and the fully supplied helper. All owners refund.

Windows .NET 11 and Ubuntu .NET 10 each pass 1,114 white-box and 3,764
public focused checks, including managed admission, callback suspension,
refusal before default or decorator effects, and retained captures.
An initial sparse checkout omitted the file-worker source linked by
the public project. The logs preserve that build failure and the
successful checks after restoring the linked sources. Both images
match six complete CPython goldens.

Both quiet gates and all 196 timed batches pass. Workers join, the
guest fixtures are removed, and the persistent archive verifies
under the lease. Staging reuses the exact B1422 image and retires
two old working DLLs after preserving and hashing each one against
its committed identity. Their original observations remain available.

[Images and revision](source.json) · [Timings](summary.json) ·
[Allocation](local-allocation.json) · [Admission](local-admission.json) ·
[Observations](observations.tgz) · [Checks and procedures](procedures.tar.xz) ·
[Archive receipt](archive-receipt.json) ·
[B1422 reference core](../2026-11-22-factory-text-attribution/README.md) ·
[Older call-arena core](../2026-11-01-call-arena-rejected/candidate-core.dll.gz) ·
[Older regex error core](../2026-11-01-pattern-error-metadata/candidate-core.dll.gz).
