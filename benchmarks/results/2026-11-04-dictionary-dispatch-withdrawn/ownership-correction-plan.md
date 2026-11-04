# Live-view rollback ordering correction

Source audit after original prototype freeze found RefundAbortedConstruction
calls governor.Release before the live storage's Release operation rejects it.
This pre-exists D14; moving rejection into one helper alone preserves that bug.
Never refund a retained live namespace. Add two independent budget/alias controls
(attribute/module views), demonstrate RED, then release/check private storage
before refunding allowance. Preserve the refusal/cancellation contract; private
misuse still throws InvalidOperationException. No replacement owner/ledger.

Retain this correctness fix even if the dispatch prototype is withdrawn. Original
ten-case comparison measures a6adcfe9, before this defensive failure-path ordering
correction; preserve exact source/hash/raw results. Qualify the final code freshly,
and distinguish prototype timing from final qualification. No normal-path replay
or silently repaired historical measurement. Bounded final-source confirmation is
allowed if needed after assessing the original observations.
