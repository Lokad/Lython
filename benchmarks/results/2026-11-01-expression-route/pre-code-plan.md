finish the executable-expression route (implementation selected BEFORE code).**
Source trace confirms all frontend expression forms have concrete lowering;
LoweredOtherExpression is only the unknown-node wrapper and already throws in
both evaluators. Replace that wrapper with an immediate compiler-defect exception.
Captured expressions are created only in prepared function/class headers after
body code has already been compiled; retain their real lowered evaluation. The
only remaining supported compile fallback is UnpackedTypeExpressionSyntax,
constructed for starred variadic parameter annotations.

Sixteen independent CPython goldens:13 new public regressions fail on the baseline
(three pass). The first eight probe snippets had six mismatches. Python unpacks exactly one
item, including ordinary lists/tuples/custom iterables; existing Lython restricts
it to TypeVarTuple/GenericAlias and gives wrong type/arity errors. Both native
pack/alias types already implement one-item iteration. Reuse existing
ExecutableUnpackValues(UnpackingLayout(1,-1)) after compiling the annotation
operand. Keep genuine lowered annotation consumers consistent through the same
bounded materializers and arity wording; explicitly preserve sync/async effect
mode. Do not add a new opcode, dispatch framework, policy or memory mechanism.

Delete: EvaluateFallbackExpression opcode/accessor/factory, expression-fallback
builder/code-object arrays and constructor argument, interning/yield-rejection
helper, sync/async detours and mirroring trigger; LoweredOtherExpression wrapper
and its throwing dispatch branches; tests' obsolete empty-array checks only.
Keep independent opcode/semantic/effect assertions, lowered class/annotation/
import consumers, statement fallback, captured headers and required mirroring.
Unknown compiler nodes fail explicitly; unsupported source retains frontend Python
shaped diagnostics. No supported syntax is removed.

Acceptance BEFORE code: CPython golden metadata/arity/type errors for packs,
ordinary iterables, custom iter/next, nested/class/generator and postponed scopes;
one evaluation in order, descriptor effects and propagation. Real async iterator
host suspension, cancellation and terminal refusal before later effects, funded
pending scratch/retained annotation aliases, cleanup/shared allowance reuse.
Bounded materialization reads at most two elements and awaits users/cleanup.
Extend the existing lowered effect interface by one unpack effect so synchronous
host promises remain synchronous; share one small mode-aware header helper using
existing materializers. No new dispatch framework. Iteration introduces bounded
scratch admission for previously special-cased packs; conservative earlier denial
is acceptable and must keep the first refused origin.
Existing focused Release compiler/type/closure/generator/provider suites on both
actual targets; final full delivery at a coherent retained-source boundary.

Architectural deletion/correctness, not a dominant-cost or speedup claim. One
unchanged complete CSV before/candidate/identical-before cost check, existing
4warmups/7batches of128, <=40s collection plus owned cleanup; fresh nonincremental
source/binary identities and byte-identical auxiliary binaries, ordinary tiering,
quiet/goldens/budget/exits, <=5% CPU/0.2% allocation baseline drift. Preserve adverse
or inconclusive observations, no tuning/retiming or performance target for the
correctness fix. Stop/remove if a real residual consumer requires a broader
redesign. No regex producer dependency.
