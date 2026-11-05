# D44 source-gate correction from independent Python evidence

2026-11-05. CPython3.13.2, checked by cpython-check.py/log. Baseline a4ce tests
were built with BuildProjectReferences=false and run against unchanged core;
new eight native cases:6pass/2fail. The baseline accepts multiple records from
one item and returns the queued second row; Python rejects the item with csv.Error.
Baseline interrupted-read test preserves prefix and has18987/True/True; Python
resets the unfinished record and has18981/False/(tail quote present).

This disproves the selected contract's requirement to preserve pending decoded
state after a failed source pull. Revise to Python record lifetime: a failed
__next__ discards its unfinished record for source I/O/type/iteration errors.
Keep physical line counts for source items actually returned. Parse failures
already reset; native input failure now uses that same nonallocating cleanup.
No admission retry, hidden allowance or early release of pending read storage.

Python allows trailing CR/LF within one item, but not another unquoted record;
validate after field conversion and before row publication. This proves at most
one pending row per Feed. Remove List<object[]> ready queue, paging/compaction,
capacity admissions/refunds, EOF queue handling and parser-to-source backlink.
Keep one independently funded pending row and its existing adoption/refund paths.
No supported Python behavior is removed; these are explicit compatibility fixes.

Non-string source items raise csv.Error with Python's iterator-should-return-
strings message, not TypeError. Construction with a non-iterable remains TypeError.
Generic source probe and native interrupted-read probe complete with independent
Python goldens. Existing type-error assertions changed only that classification;
their ownership, side-effect and cleanup assertions remain. One obsolete two-case
test privately injected multi-record input then forced EOF to exercise the deleted
queue: removed it, keeping real returned-row/EOF funding and awaited cursor tests.

Separate observed EOF mismatch: non-strict quoted EOF should emit a final row,
strict quoted EOF errors. Current Finish rejects both. Logged as bounded follow-up,
not part of the timed transport source. Original source contracts remain above.

The complete D44 milestone now includes both record-lifetime corrections and
owned input transport. Preserve original contract/source-gate evidence rather
than silently relabel it. Focus all CSV/input/error/lifetime controls, actual
host suspension/cancellation/refusal and budgets, then one bounded comparison.
