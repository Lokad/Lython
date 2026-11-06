# Builtin class patterns — 2026-11-06

Correct the builtin class-pattern boundary, with no performance claim. The class
expression's actual value uses existing type/instance helpers; aliases no longer
depend on source spelling. Python's supported match-self builtins capture the
whole subject and permit one position, including empty containers. Extra
positions raise TypeError after the subject-type check. Non-type shadows fail
explicitly. Add range to the existing builtin-type inventory; its existing
instance predicate and constructor already existed.

Remove LoweredClassPattern.ClassName, synthetic Sequence preparation/workspace,
recursive class-name construction and the duplicate subject-type switch. Existing
sequence patterns keep their own working storage. No new matcher, owner, policy,
cache or public option. list(a,b) was an incorrect extension; use list([a,b]).

Six independent CPython goldens cover all nine supported match-self types,
whole-subject identity/mutation, empty/nested subjects, exact excess-position
errors, failed-type short circuit, aliases/shadows, runtime keyword patterns,
range/no-position and non-class callable rejection, plus class-body execution.
New public controls run both APIs, retaining funding through results and returning
to compilation baseline and then zero. Focused3,056 public/320 internal checks
pass per actual.NET11RC3/.NET10.0.8, including pattern/guard, terminal refusal,
cancellation/cleanup and affected R03 whitespace families. Build has zero warnings
or errors. [Contract](contract.md), [identity](identity.json), [check logs/source](focused-checks.tgz).

Original probes retain four confirmed disagreements. R05 fixes the three builtin
cases; user-class extraction still interleaves subpatterns before later attributes.
Value-pattern custom comparison and user-class match-args/duplicate handling
remain separate compatibility work. No general class-pattern/async protocol
completion claim. No new timing campaign for this semantic correction.

Combined R03/R05 source51db4209 is fully qualified; the exact terminal receipt
is linked below and excludes later Q02. Publication/tagging postponed;
whole-script performance objective remains active.

Combined R03/R05 source51db4209 is committed/pushed. Full local.NET11 passes
4,215 internal/16,112 public; both actual.NET11/10 installed Release consumers
pass. [Exact validation scope](../2026-11-06-builtin-class-patterns/validation.json).
Cached terminal CI42174873217 is GREEN/all eight Windows/Ubuntu, both-runtime,
originalStress and installed-package gates on that source.
[Exact receipt](../2026-11-06-builtin-class-patterns/delivery-ci.json).
Never query the closed run again. This excludes the later Q02 prototype.
