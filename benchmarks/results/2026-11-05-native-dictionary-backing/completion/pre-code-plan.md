# D40 ordered removal completion

2026-11-05, after both controlled source variants; no further construction tuning.
Refined3384dd91 B264160s/all gates: CSVwarm−4.2–8.5%,ordinaryfresh−5.6–7.1%,
JSON−4.1–4.3%/.19%control. AllocationCSV−3.3–5.2%,JSON−6.9%,report−4.49%.
Report/text elapsed magnitudes control-limited. Initial4b61 report+5.13% preserved.
Full focused4111internal/4655public per actual11/10 pass on refined source.

Demonstrated source issue before retention: NativeRemove currently shifts then
clears/rehashes the entire capacity, even for the last entry. That adds an O(table
capacity+count) deletion cliff where the BCL can unlink the last entry directly.
The six construction scripts do not establish last-removal performance.

Complete ordered-storage invariant: append inserts at the end, chains descend
through insertion-order indices. Unlink the removed entry. Shift only the suffix's
payload fields, retaining each physical slot's bucket head. Update a moved head
once, and decrement its Next reference only when its target moved. Prefix links
cannot refer forward. Clear only the last payload, preserving that slot's bucket.
This removes the full capacity sweep/rehash and keeps last deletion a single
chain unlink. No allocation, callback, second backing or new owner is introduced.
Collision lookup/unlink scans retain cooperative checks; payload shifting is the
same atomic structural mutation contract. Preserve key identity, order, native
protocol side tables, versions, retained capacity and terminal refusal.

Extend the existing independent colliding-NaN mutation regression to drain the
copied dictionary in reverse, checking surviving-key lookup/count at every step.
Do not test layout, fuel or an exact number of cancellation checks.
Rebuild cleanly and repeat affected semantic/ownership/cancellation checks.

One <=75s two-job affected comparison versus original retainedc022: unchanged
nativeCSV and a CSV script prefixed with constructing/draining an8192-key cache,
with identical full stdout/file goldens. Reuse the existing driver/controls and
gates; synthetic cache-drain is a diagnostic extension, not a CPython comparison.
Keep frozen B263/B264 timings labeled as preceding this removal completion; do
not imply a new exact final-binary ratio from unchanged construction source.
Then retain/withdraw and qualify the complete milestone, rather than start
another local tuning sequence. Publication remains postponed.
