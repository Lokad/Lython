# Shared-slot delivery qualification

Runtime `cf754dc7` / frozen `95798eb9`, with fixture correction at `b4e98b91`,
passes [43662618745](https://github.com/Lokad/Lython/actions/runs/43662618745).
Windows/Ubuntu actual .NET 11/10 each pass 4,774 internal/17,849 public ordinary
and original 13 internal/406 public stress cases; installed Release package
consumers pass. All 585 frozen production inputs match the committed runtime
with explicit newline normalization. Independent rebuilt binary identity is not
inferred. Full logs, source proof and 16 summaries are cached; never requery.

Qualifies shared slots/compiled returns, exact integer and Python alias semantics,
real suspension, cancellation, terminal refusal, retained funding and cleanup.
The initial [43 fixture-cast failures](failed-fixtures/README.md) and original logs
remain separate. Direct access now checks actual retained backing without stale
reflection; no lifetime assertion or timeout was relaxed. Full local internal
checks also pass on both actual frameworks after that fixture-only correction.

The source pair still records accepted observed-helper regression and possible
fulfillment fresh regression. Qualification establishes no CPU improvement by
itself. A later overlapping-binding compiler correction is outside this exact
runtime qualification and receives its own focused checks. Publication remains held.
