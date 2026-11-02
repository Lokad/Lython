# B127 refinement boundary

Initial b7e66c78 data remains adverse: range elapsed +5.92/+4.66%, CPU +5.18/+2.98%,
controls elapsed +0.37/+0.47%. Text/search track controls, CSV flat. Initial complete
allocation: text -1.4176%, search -0.0188%, CSV +0.0001%; original raw counts kept.

Refinement replaces Cursor._userIterator (PyUserIterator) with the resolved
IPyIteratorValue _iterator in the same slot. Construction chooses the user wrapper
or native iterator once. Sync pulls no longer classify _value on each movement.
PyUserIterator now implements existing IPyAsyncIteratorValue and forwards its
existing async iteration through the shared helper. Async cursor movement shares
that interface rather than a second user-wrapper branch. Async iterable fallback
still loses to a synchronous iterator, and acquired async enumeration still wins.
No new slot, pool, owner, options, backend or call-control records. Actual subject
and lazy enumerable remain funded through mixed cleanup; terminal marker remains.

One refined bounded whole-script comparison with the original unchanged baseline
and its independent control, plus focused semantics/cancellation/lifetimes and
short complete-job allocation validation. No old adverse data is replaced. Retain
or withdraw with actual regression debt; this does not complete the objective.
