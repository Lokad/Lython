# D34 private CSV records — source gate before code

2026-11-05, parent7efe581b/coreb6046395. D32 current complete CSV profile found
CSV construction materially larger than ordinary activation. D33 list capacity
policy retained; full delivery correction CI41974563982 independent and active.

Hypothesis: DictReader does not expose its current data-row list. Creating a
PyList shell, its separate temporary reservation and Add/EnsureCapacity per field
is displaced work before constructing the actual dictionary. Use one final array
for queued records; transfer it to a Python list only when csv.reader or fieldnames
exposes it. No parse-time dictionary fusion: preserve feed/pull/error/callback order.

Delete/replace:
- Parser List<PyList> becomes List<object[]>; one existing payload reservation
  admits field payloads plus PyList.EstimateCapacityBytes(count) before array
  allocation. Fill direct final slots, enqueue, commit array funding. Remove
  per-record PyList construction/reservation and per-field Add/EnsureCapacity.
- One source cursor path returns raw arrays; existing list-returning operations
  only adopt that funded array via one narrow internal PyList transfer factory.
  No copies, alternative parser/backend, ownership ledger or per-row owner object.
- DictReader consumes raw arrays. Its outer finally returns private capacity only
  after header protocols, key operations and actual awaited cursor cleanup. Empty
  skipped rows release before the next pull. Escaping dictionaries, rest lists,
  header lists and independent string/numeric payload owners retain funding.
- Queued completed arrays remain charged through EOF and failed feeds. Parser
  scratch release must not refund queued arrays. Native cleanup stays possible
  after terminal refusal. Transfer and refund are mutually exclusive per record.

Source gate: only reader and DictReader consume source records; .reader shares the
next cursor position and cannot expose the already-dequeued current row. Reentrant
headers can pull future rows; no current-row guest alias. Preserve lazy fieldnames,
mutable/custom headers/restkey/restval, repeated fieldnames reads, iterator cleanup,
line_num, duplicate names, short/extra/blank rows, QUOTE_NONNUMERIC, shared-reader
interleaving, recoverable syntax errors and queued prefixes. Stop/refine if another
current-row escape or per-record lifecycle framework is required.

Contract: coarse private array charge deliberately includes possible list wrapper
metadata and scalar headroom, reusing the existing estimate. Public adoption keeps
the entire charge. Private release does not release value payloads; dictionaries
and rest lists admit their own capacities before retaining scalar references.
Earlier/later budget refusal points may change; no identical tiny thresholds.
No public API/caps/defaults, tracing, forced collection, retry or hidden allowance.

Focused acceptance: existing CSV public goldens/headers/shared-reader/errors plus
native admission/ownership/cancellation/lifetime checks on actual11/10. Add direct
adoption identity/funding and DictReader actual suspended header cleanup/failure
funding only where existing checks leave gaps. Full-budget/public alias tests stay.

Bounded comparison after correctness: existing three-image/two unchanged-role
driver, native and ordinary-helper complete CSV plus an actual csv.reader control;
separate warm/fresh if material, <=180s owned. Full RAM-mediated I/O/goldens/result
cleanup included; fixture reset/startup/hash excluded, normal runtime, quiet,
identities/balances/joins. Preserve adverse/failed conditions. Decide retain/refine/
withdraw, no rigid percentage veto. Do not infer CPU or CPython ratios. Coherent
platform/Stress/installed qualification follows retained checkpoint, no publication.
