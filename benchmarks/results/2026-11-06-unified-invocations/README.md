# Unified invocations — 2026-11-06

Retained structural simplification and closed-method call correction. **A broad
whole-script speedup is not established.** [Design](contract.md),
[refinement](refinement-contract.md), [validation](validation.json) and
[original failures/check logs](correctness-history.tgz) preserve the decisions.

The lexical context now holds the existing executable state and loop. This removes
the separate interpreter object/backpointer, stored active-frame reference and
generator's duplicate handle. The existing caller/callee chain, native operations,
lowered consumers and managed admission remain. No arena, saved-call stack, new
dispatcher, pool, budget, token policy or backend was added.

After actual users and awaited cleanup finish, completed contexts detach working
slots, caches, handlers and callers. Retained names/cells remain usable without
retaining refunded workspace. Generators keep state through suspension. Internal
frame views are invocation borrows, rather than post-completion snapshots. Tests
preserve unchanged-target observations through the surviving namespace, retain
live-input/physical-tail/argument-buffer checks, and require detached slots/code
when storage is gone. New lifetime checks use the existing nonparallel collection.
The existing conservative context estimate covers the larger shell/namespace;
no allowance was increased. Public API and Python behavior are unchanged.

The refinement removes the blanket OwnerType guard for closed native returns.
The existing pure-body, closure, class-cell, annotation, comprehension and handler
proofs still apply. Receiver/default/keyword binding and source/error, cancellation,
stack and ownership checks remain. Guest callbacks use ordinary executable calls.

Full local Release passes **4,210 internal and 16,078 public tests per actual
.NET 11/.NET 10 runtime**; focused selections pass 1,059/3,406. Full combined
F02/F03 Windows/Ubuntu, original stress and installed-package checks are pending.
K01's cached terminal 42145148043 does not qualify this source.

## Comparisons and limitations

Quiet Azure F4as_v7, Ubuntu 24.04.4, .NET 11 RC3, CPython 3.13.16; normal GC and
tiering, unchanged b94 worker/protocol. Full goldens, quiet endpoints, final funding,
joined workers and removed fixtures pass. Exact sources/hashes and every sample
are inside the archives. The method control has an independent CPython golden.

| Block | Source pair | Result |
| --- | --- | --- |
| B312, 166.83 seconds / six jobs | Initial c31 vs F02/core50b6 | CSV warm +0.05% / -1.40%; fresh +0.22% / +1.44%. Async +4.59% / +0.11% with 4.28% identical-current spread; text mixed. Method +0.91% / +9.05% is adverse, with 8.06% current spread. |
| B313, 38 seconds / one job | Initial c31 vs F02, three isolated images sharing CoreLib/process hashes | CSV process allocation -1.77% / -1.79%; elapsed -0.44% / +0.92%, inconclusive. |
| B314, 139.79 seconds / five jobs | Refined 5339 vs initial c31, not F02 | Method control -20.12% / -20.65%. CSV warm -0.79% / -1.42%, fresh +1.42% / +0.12%, async -3.07% / -0.99%; text mixed. |

Two percentages compare the candidate/control roles with the reference; allocation
uses candidate versus two original roles. Do not add these deltas together. The
same initial method binary measures differently across blocks, limiting the precise
magnitude. The method control remains about 3.8x CPython. CSV elapsed improvements
are not substantial or established by these observations. Keep K01's warm CSV/text
regression debt visible. The final refined source was not remeasured for allocation.

The worker clocks include invocation, mediated RAM-file I/O, complete validation
and prior-result cleanup; fresh adds compilation/program disposal. Startup, fixture
staging/reset, transport and final teardown are excluded. B313 instead includes
current-result cleanup and reports process allocation including fixture/reset,
reflection and background work. It supplies no CPython ratio or guest-only CPU
claim. No result establishes RSS, startup or embedding-process latency.

[Initial raw evidence](initial/observations.tgz), [allocation evidence](allocation/observations.tgz)
and [refinement evidence](refinement/observations.tgz) remain separate. Publication
is postponed; the complete-script performance objective remains active.
