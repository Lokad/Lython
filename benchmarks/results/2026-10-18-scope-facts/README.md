# Reused function scope facts, 2026-10-18 UTC

Lowering creates the existing ScopeDirectiveFacts once per function definition.
Generator code, executable function code and both runtime PyFunction constructors
reuse those facts. Repeated definition-time traversals, temporary parameter arrays
and hash sets disappear. No new cache/registry, analyzer, layout, lookup or owner.
Only syntactic names and the prepared class-cell requirement are shared. Defaults,
cells, types, hosts and contexts remain private to their execution.

Baseline: `241dd16717348162856c71de671e20cb569b6668`; candidate: that revision plus the
[frozen five-file patch](candidate.patch). The [source screen](source-screen.json),
[preparation check](preparation-check.json) and [measurement scope](measurement-scope.json)
precede runtime edits/capture. Frontend preparation finishes before lowering;
suspended-definition copies preserve binding names while capturing header values.
The sets are read-only to consumers. No production/test change after capture.

| Native warm, 64 creations/calls | Baseline | Candidate | Status |
| --- | ---: | ---: | --- |
| Class-method definitions, sync | 789,400 B | 680,856 B | 108,544 B less per invocation |
| Class-method definitions, async | 789,400 B | 680,856 B | 108,544 B less per invocation |
| Ordinary executable control, sync | 19,560 B | 19,560 B | Identical arrays |
| Ordinary executable control, async | 19,600 B | 19,600 B | Identical arrays |

One pair uses two warmups/eight samples/eight complete invocations per route.
Every allocation array is stable; all native step arrays are identical, 1,744 for
the class job and 1,243 for the ordinary control. All invocations return 2,080 and
release frames, temporary storage and the shared allowance to baseline/zero.
Existing rough runtime admission estimates remain unchanged.

Elapsed results are **inconclusive**. Native class sync 1.107â†’1.073ms and
async 1.304â†’1.206ms have candidate IQR 11.57%/11.97%; the ordinary native sync
control moves 0.116â†’0.161ms with 47.38% candidate IQR. Public fresh class
compile-to-result moves 1.476â†’1.507ms sync and 1.652â†’1.589ms async; ordinary
fresh controls get 15.76%/12.55% slower. No elapsed improvement, normalized
speedup or CPython gain is claimed; the capture is not repeated.

Native warm excludes compilation/context setup/scheduling/projection/startup and
uses explicit 1M-step accounting; pure ValueTasks complete synchronously. Public
fresh includes compilation/run/projection/disposal/checks in a warmed process,
with the default no-fuel ceiling; startup and host I/O are excluded. These
boundaries differ. [Environment](environment.json) reuses the original Windows
x64/i7-14700KF/.NET 10.0.13 configuration timestamp. No quiet-machine, startup,
compile-only or complete representative-job performance claim.

The compiled definition keeps an extra facts reference. Never-executed functions
can now retain their facts up front instead of constructing them on demand during
execution; this cost is not isolated by these noisy fresh timings. The existing
coarse source-sized compilation allowance/owner covers the metadata. Cells,
defaults and actual class objects keep existing owners. [MEMORY](../../../MEMORY.md)
documents this tradeoff. No guest-heap tracing or new accounting machinery.

All 423 distinct focused Release checks pass, including 58 complete CSV/text/
JSON/file jobs. Existing class-cell barriers, mutable defaults/live captures,
generic/generator/suspending definitions, actual host cancellation, work/stack
limits, refusal and retained/shared-budget owners remain covered. No new tests
that enforce the metadata architecture; existing regressions are reused,
including [CPython goldens](../2026-10-18-generic-functions/goldens.json).
[Validation](validation.json) records filters, receipts and source hashes.

[Diagnostic](diagnostic.cs), [raw baseline](baseline.trx), [raw candidate](candidate.trx),
process receipts and [comparison](comparison.json) preserve evidence. Temporary
diagnostic removed. Original Stress scales/assertions/timeouts unchanged;
Release/platform qualification is recorded in PLAN. Regex stays planning only.
