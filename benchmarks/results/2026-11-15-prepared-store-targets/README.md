# Prepared store targets

R413, 2026-11-15. Retained **55ba4c8a** versus qualified **1b82cd46 / 0edc0c1c**.
[Full delivery qualification passes](delivery/README.md). No public API or ownership contract changes.

One prepared target tree carries actual lowered reads and unpacking layouts.
Executable construction and sync/async lowered stores share it, including augmented
assignment. Removes syntax-keyed dictionaries/structural hashing, eager lowering
in syntax constructors, repeated compiler lowering, per-store nested wrappers,
the duplicate augmented-target family/factory, raw augmented resolver, obsolete
compiler loop dispatcher and unused async raw-loop route. Real raw-expression
consumers remain. No new cache, arena, owner, option or execution engine.

| Complete script / phase | Elapsed change | Wider process CPU change | Reference / candidate spread |
| --- | --- | --- | --- |
| Six-helper manifest / compilation | +0.38..+2.86% | −0.84..+2.01% | 1.29 / 1.17% |
| Six-helper manifest / compile-to-result | −1.74..−0.30% | −1.52..−0.03% | 0.72 / 0.74% |
| Generated 96-field validator / compilation | −1.26..+1.59% | −1.40..+2.33% | 2.02 / 0.86% |
| Generated validator / compile-to-result | −0.69..+0.52% | −2.04..+2.67% | 1.01 / 0.21% |
| CSV invoice / warm control | −2.20..+1.23% | −2.67..+0.76% | 0.89 / 2.59% |

Local natural-GC allocation diagnostics: compilation **−0.42% / −0.32%** and
compile-to-result **−0.68% / −0.32%** for manifest/validator; warm CSV flat.
They include one held compilation control and fixture reset, unlike command-v2
elapsed clocks. Do not combine phase medians or infer CPU from allocation.

Retain for concrete simplification and modest allocation reduction, **without a
demonstrated CPU improvement**. Accept possible manifest compilation regression
and mixed controls as explicit debt. The initial partial B805 compilation result
had the opposite sign but lacks the final quiet gate; it cannot establish recovery
or neutrality. No unchanged retiming is needed for this retention decision. The
broader call/frame/operand/collection performance objective remains open.

Focused Release checks: **103 internal / 324 public on each actual .NET 11/10**;
clean nonincremental solution build passes. Coverage retains evaluation/partial-store
order, nested/starred headers, class-cell lambdas, annotations, actual suspending
setters, cancellation, refusal, funded aliases and joined cleanup. Original nested
loop diagnostic column regression (2 became 28) is fixed by preserving loop origin;
the original assertion is unchanged. Ordinary local 11 passes 4,781 internal and
17,841 public; 33 subprocess failures from missing pinned SDK PATH are preserved,
and all are covered by the corrected 69-case passing selection. This split check
is not relabeled a single green full run.

**B806 succeeds in 193.700s**, quiet F4as_v7 / Ubuntu24.04.4 / four EPYC9V45vCPUs,
16GiB/no swap, .NET11RC3 normal tiering/workstation GC. Two independent processes
per source, 8s compilation / 5s execution full-request warmup, five rotated ~0.5s
batches. Existing workers/catalog/goldens; complete images verified. Compilation
includes prior compiled-owner cleanup; compile-to-result includes compilation,
mediated RAM I/O, full output/file checks and per-invocation compilation cleanup;
warm excludes compilation. Fixture reset/protocol and final cache teardown are
outside execution clocks; wider CPU includes them. No startup, pending-I/O, RSS,
contention or embedding-tail claim. Generated validator is a compilation diagnostic,
not a replacement for representative agent scripts. No CPython refresh inferred.

Original staging-floor refusal occurred before mutation. Exact committed archived
R411 remote duplicates were retired only after fresh lease, UID/path/hash/link and
all-alias verification. B805 then completed one compilation job before the collector
used unsupported `fresh` instead of `compile-run`. Its journal, partial responses,
stderr, original scripts and binaries remain; workers joined and empty RAM storage
was archived/verified before unmount. B806 corrects only that protocol label and
uses identical images/catalog. Both runs are terminal-cached. No VM users or RAM
fixtures remain; evidence was archived and verified persistently under the lease.

[Medians and original partial](summary.json), [allocations](allocation-summary.json),
[identity/full source hashes](identity.json), [checks](checks.json),
[raw B806](observations.tgz), [failed B805](failed-B805-observations.tgz),
[binaries/source/checks/procedures/original refused payload](source-checks-and-procedure.tar.xz).
