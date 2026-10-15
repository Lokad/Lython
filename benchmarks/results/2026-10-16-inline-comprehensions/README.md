# Eager comprehensions execute in the caller frame

List, set and dictionary comprehension loops, filters and item expressions now
emit ordinary executable instructions under the existing inlined binding scope.
The outer iterable is evaluated before entering that scope. Iteration-bound names
use its existing lookup/assignment rules; other names keep caller slots/cells.
Per-item helper calls use the delivered direct positional transport, rather than
lowered deferred argument construction. No new invocation frame, owner, callable
ABI or scope-analysis model is introduced.

Existing display operations accept an internal iterator depth to address the
result below active iterators. Scope temporaries restore state explicitly on
normal exit and unwind in reverse nesting order on failure; existing disposal
failure tests retain original live entries for propagation. Clause emission is
iterative, including a 512-clause execution/value/restoration control.

The duplicate generator expression-family dispatcher is deleted: ordinary
lowering already handles lambda defaults, formatted strings, expanded calls and
displays containing yields. Eager comprehensions with an outer yield use that
same route. The remaining prepared adapter is explicitly a lazy generator
expression, with its obsolete eager capture branches removed.

Lambdas created inside active comprehension scopes retain the established
lowered-body/dictionary capture route. Their Python behavior remains supported;
that fast-body gap, lazy generator bodies and required lowered/class-suite paths
remain open. Iteration bindings still use the existing dictionary namespace.

| Warm per-item helper list comprehension, 2,048 items | Earlier Lython | Candidate Lython | CPython 3.13.16 |
| --- | ---: | ---: | ---: |
| Apply `convert(value)`, retain list, print its length/sum | 0.6107 ms | 0.5851 ms | 0.1253 ms |

One [fixed diagnostic](warm.json) observed a **4.2% lower Lython median**;
IQR/median 1.1% / 0.9% / **38.3%**, respectively. This is a modest observation,
not a qualified speedup or external multiplier. CPython timing is inconclusive.
The owned command took 7.808 s including cleanup; no repeat or retuning. The
retired arithmetic producers were not run or reused.

Baseline 3bc463a8dfefdba4c05da603682f1d70bf6316d2 contains the same new per-item
helper fixture in both roles. This case exercises argument-packaging removal;
the older `comprehensions.helper-list` case performs arithmetic inside a helper,
not per-item helper calls, and is unchanged. Timed candidate production differs only in
the nine frozen files in [the declaration](comparison-declaration.json).
[The case](catalog-micro.json), timing machinery and CPython worker are identical.

Measured 2026-10-16 on Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, four AMD EPYC
9V45 vCPUs / 16 GiB RAM, SDK 10.0.401/runtime 10.0.12 and CPython 3.13.16 with
normal GC/GIL, `-I -S` and explicit UTF-8 protocol. Normal Lython public policies,
cooperative allowance, GC/tiering and managed stack admission remain. CPython has
no equivalent containment. Warm synchronous Run timing includes fresh state,
execution/output/projection and previous-result disposal, excluding compilation,
startup, transport and full hashing. Real suspension is checked separately.

[Baseline](baseline-correctness.json) and [candidate](candidate-correctness.json)
match independent output at all three core sizes. [Cleanup/source proof](cleanup-source-proof.json)
verifies all frozen changes, identical tooling, seven exited workers, inactive
services and available VM lease. No file fixtures are used for this diagnostic.

[Local validation](local-validation.json): 2,341 ordinary white-box cases,
400 affected comprehension/generator/scope public cases, 13 focused public cases
and 284 containment/representative/file-worker cases pass (groups overlap;
each under 30 s including cleanup). Existing and new controls cover lexical/
class/locals/super/walrus behavior, nested-error scope restoration, retained
closures/defaults, caller-variable shadowing, eager/lazy outer yields, actual
iteration/truth/helper suspension and cancellation without replay, admission
before target publication, and results funded across scope end/two executions.
Nine [independent CPython 3.13.2 goldens](cpython-goldens.json) match, including
the retained-generator control added during the cleanup review below.

Initial prototype scope-exit failures and the obsolete FIFO assertions are kept
in private receipts; a test's over-specific block-count assumption was replaced
with execution of all 512 clauses. No timeout or allowance was relaxed. Exact
Release package/platform qualification is recorded at delivery; original long
Stress and full local public suites were not rerun.

Existing prepared checkpoints can change explicit fuel/admission thresholds.
Partial storage can remain charged until its finite execution/result owner ends;
pending callbacks and retained aliases remain funded. No forced GC, reference
tracing or cumulative-allocation substitute is added. SPEC/MEMORY record the
contracts. This closes ordinary eager routing, not all comprehension performance.

Final resource review found that native iterator operands were not marked for
cleanup on body failure, and `PopTop` discarded them on early break. Six focused
sync/async controls first failed; marking the existing checked enumerator/cursor
as executable temporaries fixed error unwinding, and using the existing tail
cleanup for discarded operands fixed break. Retained Python generators stay
open. No new owner, cleanup registry or asynchronous unwinding framework was added.

These **three additional source changes are unmeasured**; the nine timed files
remain unchanged. The diagnostic describes its frozen candidate, not final HEAD.
[Post-cleanup validation](post-cleanup-validation.json) passes 2,347 white-box,
402 affected public and 284 governed/representative public cases (overlap), all
under 30 s including cleanup. Original failing receipts remain private. No timing
repeat, timeout relaxation or allowance increase was used.
