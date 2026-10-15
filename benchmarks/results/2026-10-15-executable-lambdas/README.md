# Lambdas share function machinery and compile eligible bodies

24e6442a deletes the separate 162-line lambda callable and its special-case type
handling. Raw, lowered and prepared creation use existing PyFunctionBase-derived
ordinary/generator classes. Parameter/default lowering, return-body storage and
scope facts are prepared once for normal compiled scripts. Lambda-specific
binding labels remain. Shared metadata fixes basic class-qualified names and
enables custom attribute writes; arbitrary attribute deletion remains unsupported
by the shared function implementation. [Earlier probes](pre-refactor-metadata.json)
record the metadata gaps and unchanged error-wording difference on d1d175bf.

The executable-body change reuses the named-function compiler, closure cells and
function invocation. It removes the whole-lambda fallback and mirroring required
solely by it. Prepared creation builds defaults directly from existing operand
storage; captured-expression wrappers, cloned parameter/definition records and
their retained charges are removed. Closure-array admission now precedes array
allocation, with rollback and funding marks published only after complete capture.
No callable ABI, owner, iterator or scope-analysis framework is added.

Full Windows/Ubuntu qualification on 0e0c53e1 caught one regression: the shared
immutable lambda-name label had become per-function storage. The follow-up
restores that fixed label in PyFunctionBase; the duplicate callable stays deleted.
[24 focused regressions](post-label-validation.json) pass, including the existing
cross-lambda identity control. This fix follows the frozen timing candidate and
is unmeasured; the diagnostic does not describe the final qualified HEAD.

Eligible lambda bodies execute instructions. Unsupported child expressions still
use their own fallbacks; class suites/required lowered bodies remain lowered.
Comprehensions are not made executable by this change. The existing dictionary
retention helper remains for those bodies and dynamic namespace consumers.

| Existing warm stable-sort script, 2,048 tuples and lambda keys | Earlier Lython | Candidate Lython | CPython 3.13.16 |
| --- | ---: | ---: | ---: |
| Build reversed records, stable-sort by first field, print all records | 1.778 ms | 1.595 ms | 0.3020 ms |

One [fixed diagnostic](warm.json) observed a **10.2% lower Lython median**;
IQR/median 2.4% / 3.5% / 0.7%, respectively. No qualified multiplier, confidence
interval, whole-project or complete file-workload speedup is established. The
owned diagnostic took 7.706 s including cleanup; no retry, retuning or new fixture.

Both roles already contain the shared-function refactor at baseline
24e6442a7bfbbdae98684b90c96527d5f0ca7fa6. The candidate changes the eleven frozen
production files in [the declaration](comparison-declaration.json). The class
deletion itself is unmeasured. [The existing case](catalog-micro.json), timing
machinery and CPython worker are identical. Timing includes fresh run state,
execution/output/projection and previous-result disposal; compilation, startup,
transport and full hashing are excluded. Suspended host callbacks are tested
separately. The list comprehension remains on its existing route in both roles.

Measured 2026-10-15 on Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, four AMD EPYC
9V45 vCPUs / 16 GiB RAM; SDK 10.0.401/runtime 10.0.12 and CPython 3.13.16 with
normal GC/GIL, `-I -S` and explicit UTF-8 protocol. Lython's normal public policies,
cooperative allowance, GC/tiering and managed stack admission remain. CPython has
no equivalent containment. This is an in-memory script, not a file benchmark.

[Baseline](baseline-correctness.json) and [candidate](candidate-correctness.json)
match independent output at all three core sizes. [Cleanup/source proof](cleanup-source-proof.json)
verifies all frozen production changes, identical tooling, seven exited workers,
inactive services and available VM lease. Preparation's local observer was lost;
the existing remote unit was recovered as inactive/success with both correctness
receipts Completed. Preparation was not restarted. No file fixtures are used.

[Local validation](local-validation.json): the runtime-only refactor first passes
2,330 white-box, 287 affected public and 14 new public controls. Executable bodies
then pass 2,335 ordinary white-box, four focused source controls, 303 affected
function/generator/metadata/scope cases, 284 containment/representative/file-worker
cases and 21 final lambda/isolated-stack cases (groups overlap; each under 30 s
including cleanup). The full white group includes capture denial without marks
and a successfully funded retry. Controls cover defaults/keyword metadata,
transitive and late-binding closures, locals/walrus, class scope/super/descriptors,
generator bodies/default yields, real body I/O/cancellation, retained aliases and
denial before publication. Twelve [CPython 3.13.2 goldens](cpython-goldens.json)
match independently. No timeout, allowance or stack policy was relaxed.

[48-level control](stack-admitted-control.json) completes in an isolated probe;
the [192-level control](stack-control.json) is rejected by the existing parser
64-level admission guard. The first diagnostic script expected acceptance too
broadly; its failed assertion/receipt is preserved. Managed stack policy is
unchanged. Original long Stress/full local public suites were not rerun; exact
Release package and cross-platform qualification are recorded at delivery.

Shared metadata and capture storage can reject earlier under small allowances;
explicit return/creation checkpoints can change fuel thresholds. SPEC/MEMORY
record those conservative contracts. Pending users and retained aliases remain
funded independently of physical CLR collection.
