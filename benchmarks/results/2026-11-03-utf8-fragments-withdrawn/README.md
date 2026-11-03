# Immutable UTF-8 fragment storage — B149 withdrawn

2026-11-03. Baseline d7b715d3, exact runtime44dad50f. Both prototypes are withdrawn;
no string-window representation, payload-sharing flags or private-copy hooks remain
in production. The fully qualified class-namespace correction remains intact.
This decision follows two implementations and complete-script evidence, not a
fixed improvement threshold or a permanent ban on shared immutable storage.

The initial prototype used ReadOnlyMemory storage, shared immutable same-owner
fragments and retained their payload/array metadata through unpublished-source
rollback. Private decoded file windows still copied before returning guest values.
The refinement used explicit array ranges with the same ownership contract. Both
passed Unicode/CSV/regex, alias, cancellation, refusal and ownership checks. The
refined source passed638 internal/2,838 public cases per actual.NET11/.NET10 target.
All code and checks are recorded in the source patches and focused-log archive.

| Complete workload | Refined elapsed Δ | Unchanged control Δ | Allocation Δ / control |
| --- | ---: | ---: | ---: |
| CSV helpers, warm |+4.82%|+1.54%|+2.42% / about0%|
| Text helpers, warm |−0.31%|−2.94%|−2.95% / about0%|
| Report, warm |+2.49%|−1.03%|+0.80% / about0%|
| CSV helpers, compile-run |−0.83%|−0.69%|Not retimed|
| Text helpers, compile-run |about0%|−1.95%|Not retimed|
| Report, compile-run |+2.92%|−1.47%|Not retimed|
| ASCII filename pipeline, warm |−3.89%|−0.39%|Not measured|
| Unique JSON keys, warm |+3.75%|+4.80%|+1.25% / about0%|
| Long text fragments, warm |+4.99%|+4.16%|Not measured|

These are diagnostics, not portable CPU improvements. Each row compares the
candidate and a second unchanged baseline process with the first baseline process.
The refinement also includes the exact initial prototype in every timing job.
External process CPU includes fixture reset, transport, GC and JIT; several controls
drift by15–27%, so those CPU percentages cannot establish a guest-only improvement.
Allocation is a separate, repeatable whole-job observation with stable controls.
The initial representation showed the same allocation tradeoff and mixed/adverse
elapsed results. The refinement did not remove the general tradeoff.

The specific ASCII gain does not establish a useful general whole-script benefit.
Text saves about3% allocation, while CSV/report/unique strings allocate more and
representative elapsed observations remain flat/adverse against controls. Added
representation fields, sharing/rollback state, copy frontiers and retained-large-
backing tradeoffs do not justify this implementation. No exact cause or universal
rejection of the architecture is inferred from noisy timing.

The first owned job completed eight comparisons, then verification of the new long
fixture failed: local CPython stdout had Windows CRLF, while the Linux worker used
LF. [Original failure and correction](setup-correction.json), original catalogue,
journal and all completed data remain. Its final quiet gate was not reached; that
block is explicitly an interrupted diagnostic. Only the missing job and allocation
diagnostics were continued after correcting the golden. The long fixture was also
verified under isolated VM CPython. The refined nine-job comparison is complete,
with before/after quiet gates, exact goldens and joined workers/fixtures. No completed
rows were discarded or replayed to hide the failure.

Initial service179.454s; missing-case/allocation continuation94.542s; refined
timing182.963s and complete service207.635s. All services are terminal, observations
retrieved, owned users joined and the shared VM lease free. Natural GC/tiering,
ordinary.NET11RC3 on the same quiet F4as_v7. Command-v2 timing excludes fixture
reset, includes actual guest I/O/full goldens/prior-result cleanup; compile-run
also compiles/disposes. Allocation diagnostics deliberately include reset and use
isolated images sharing CoreLib/process GC. No new CPython ratio is claimed.

Exact [initial identity](identity.json), [refined identity](refined/identity.json),
[initial assessment](assessment.json), [refined assessment](refined/assessment.json),
[initial allocation](allocation-assessment.json) and [refined allocation](refined/allocation-assessment.json).
[Initial source](source.patch.gz), [refined source](refined/source.patch.gz), original/
corrected catalogues and raw archives preserve all variants. Identical helper and
adapter bits are frozen across compared core images; normalization is recorded.
No backend package changes. Source/raw identity is separate from restored outputs.

Production was restored from HEAD and rebuilt nonincrementally. Only independent
cross-owner and private-reader lifetime regressions remain; shared-representation
architecture assertions are removed. Original whole-output refusal coverage is
restored, including enough allowance for one fragment but not the full result.
Restoration results are recorded separately. The broader performance objective
continues; next source gate targets CSV record construction/admission without
changing the global string representation.
