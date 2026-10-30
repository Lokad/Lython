# Class-method execution and recursive protocol unwind

Measured on **2026-10-30** using the unchanged owned stack-guard witnesses.
This is a bounded stack-safety/latency comparison, not a representative workload
benchmark or a CPython comparison. It does not update the primary performance
table in [docs/BENCHMARKS.md](../../../docs/BENCHMARKS.md).

Control: **0411ee184822a064533cdc30587aea124d317a19**. Candidate: that source plus
`candidate.patch` inside [frame results](class-method-frame-results.tar.gz).
The candidate prepares ordinary class-defined methods with the existing
executable compiler and schedules eligible type-subscription/getter calls through
the existing caller/callee chain. It removes duplicate call/body admission;
class suites and genuinely dynamic callbacks keep their existing evaluators.

## Fixed final comparison

| Unchanged witness | Control seconds, pairs 1 / 2 | Candidate seconds, pairs 1 / 2 |
| --- | ---: | ---: |
| Recursive class-subscription descriptor | 8.10 / 8.14 | 0.48 / 0.47 |
| Recursive class-subscription method | 8.50 / 8.36 | 0.46 / 0.47 |
| Recursive function with finally | 0.46 / 0.45 | 0.46 / 0.46 |

GNU time measures a fresh child process through exit, including CLR startup,
compilation, real asynchronous file I/O, default-depth rejection, unwind,
subsequent successful calls and cleanup. These are whole-witness elapsed times,
not warm execution or compilation-only measurements. Every witness returns the
original expected value. Both fixed pairs pass the predeclared descriptor
criterion of at least a twofold reduction. The approximately seventeenfold
descriptor reduction describes only this pathological recursive witness.

The dedicated VM is Azure Standard_F4as_v7, Ubuntu 24.04.4 x86-64, four AMD EPYC
9V45 vCPUs and 16 GiB RAM. Both sides use .NET 11 RC3
11.0.0-rc.3.26522.148. The shared VM lease and before/after quiet-machine gates
pass. Each job runs in an owned systemd service with the original **25-second**
deadline and whole-process-group cleanup. Two pairs use fixed alternating order;
there are no favorable retries or ten-minute lanes. Raw CPU, elapsed, RSS,
machine-pressure and available throttling observations are included. The child
service's unavailable throttling counter stays explicitly unavailable.

The witness source is unchanged: Git blob
`a0ef7a7e221ca6c9d5079acc9f3bacebba8c2148`.
Core SHA-256 identities:

- Control: `3e7c37bde96793b3ce6346001d7bbd0157e946489e83b6c9a3c6244100cce36e`
- Candidate: `644fbfd68bc94b777ba74abbe7a0ac307837bb94242d52723dda77d24a3d32e2`
- Candidate patch: `0d01111b58cdb30d597fddb57a03edfc37ea9c0bc98cbc007b68c29a62df4284`

The archive includes collector, exact patch, identities, all twelve observations,
quiet gates and a separate managed depth diagnostic. The candidate fixture was
rebuilt against the new core; its stack-guard source and expected results match
the control. This source-plus-patch identity is the measured candidate, rather
than a claim about subsequent HEAD or release binaries.

## Depth and semantic acceptance

Separate genuinely resumed diagnostics borrow 16 MiB from an explicit host
allowance so that stack rejection can be identified independently of memory
admission. The control reaches 935 method / 879 descriptor guest frames before
the managed physical-stack guard. The final candidate reaches the existing
**1,000-call** recursion limit in both cases, with structured attribution and
complete tracebacks. No stack bound or witness deadline was lowered. The final
timed witnesses keep their original memory allowance and source.

Four focused managed tests compare CLR frame growth at guest depths 1 and 64
for real class-defined methods/getters through both APIs; growth stays within
the existing twelve-frame tolerance. Separate finally/traceback controls and
public 300-call positional/keyword regressions preserve guest frames. Public
finite method/descriptor recursion at depth 600 matches CPython 3.13.2 through
both APIs. Existing delayed descriptor/callable, cancellation, refused admission,
retained-result and budget-reuse controls exercise the actual public paths.
Full platform/package qualification is recorded separately in PLAN.md.

## Preserved intermediate evidence

The first preparation-only [raw capture](raw-results.tar.gz), collected by
[collect.py](collect.py), appeared faster but is **invalid as speedup evidence**:
it added logical body admission and rejected recursive protocols at 512 frames.
The [admission audit](class-method-admission-audit.tar.gz) preserves that finding,
including an earlier private-driver dependency-resolution failure before guest
execution. That tooling failure is not a Lython runtime result.

After consolidating admission, the preparation-only candidate reaches 949/890
frames, but its two fixed descriptor pairs remain around eight seconds. This
[corrected preparation-only capture](class-method-prepared-only-corrected.tar.gz)
does not meet the latency criterion. Only then was the bounded existing-frame
continuation added and measured in the final capture above. No dormant alternate
backend, general protocol scheduler, timing policy or runtime stack telemetry is
introduced. These intermediate archives remain historical rather than being
relabeled as the final implementation.
