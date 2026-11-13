# First word-buffer delivery failure

[43486845628](https://github.com/Lokad/Lython/actions/runs/43486845628),
runtime/tests557d18d4: each platform passes4691white-box and17602public tests on
.NET11; five cases of the old operand-disposal test fail. Later target, stress
and installed-package steps are skipped, not qualified.

The test expected a throwing disposer and already-disposed higher entries to stay
in their operand slots and be disposed again on retry. That conflicts with the
reentrant loan-lifetime correction: disposal can already have returned storage to
another borrower before a later callback throws. The corrected test keeps original
exception identity, lower unvisited references, LIFO side effects and exact single
retirement, without disabling the throw on retry. The separate reentrant borrower
regression still fails the old implementation. Production source is unchanged;
78focused tests pass on each actual .NET11/10 target. Original failed logs remain.
