# D44 owned CSV input transport

2026-11-05, selected after D43 CPU/source gate; no implementation/prototype yet.
Baseline7dcc3b48/a4ce2b48 is fully qualified42014687629/all8 gates/cached.
Publication postponed; this is the next coherent pipeline replacement.

## Demonstrated source boundary and hypothesis

Exact native text-file iteration calls ReadLine, CopyWindowRange and
PyString.CopyFromUtf8 before CsvRecordSource feeds that string into a second,
already-owned parser record buffer. Guest code never receives that line object.
CSV fields/row storage are ALREADY admitted together in FinishRecord; do not
claim per-field reservations remain or reopen that completed construction work.
Current parser already bulk-scans UTF8. D43 sampled string-allocation/locking
callers, not a precise removable CPU share. Hypothesis: removing the private line
string/array/copy/admission/cursor transport helps complete CSV scripts; magnitude
requires one bounded comparison. Text lines actually exposed to Python still own
independent storage.

## Replacement and deletion

- Add internal complete-line append into an existing GovernedByteBuilder on the
  existing decoded UTF8 read state. Sync/async paths reuse newline/window/encoding/
  refill/Advance behavior and existing host I/O. Append/admit before cursor advance.
  No borrowed file window escapes the read call and no new lifetime/cache layer.
- Exact TextFileHandle CSV sources append the physical line directly to the
  parser's existing record builder. Keep original iterator acquisition/cleanup;
  other iterables retain their Python iteration/string validation semantics.
- Save decoded record-prefix length. After the complete native read, capture its
  appended span, Truncate to the prefix, and use the SAME Feed algorithm to compact
  in place. GovernedByteBuilder.Truncate already changes length without clearing
  backing. Decoding CSV syntax never expands output beyond consumed input, so
  capacity is already funded and Feed must not replace/release the aliased backing.
  FinishRecord truncates but does not release record capacity; field output remains
  independently allocated/admitted before the scratch is reused.
- Remove the native CSV detour through per-line PyString/byte[] construction,
  copy admission and generic next transport. Keep public ReadLine/file iteration
  and generic-source Feed helpers with real callers. No second parser, callback
  framework, new memory policy, quota, root-capacity pool or admission retry.

## Semantic/lifetime proof before code

The decoded window already applies encoding/BOM/error/newline translation; do not
feed raw host bytes or a different dialect. Complete a physical line before Feed
so failed-line consumption, line_num and recovery stay correct. On native read/I/O
failure, Truncate appended input back to its old decoded prefix without resetting
prior multiline parser state. On parser failure, use existing ResetRecord. Preserve
QUOTE_NONNUMERIC conversion/error timing and generic non-string-source behavior.

Prove in-place safety through multiple CSV records in one physical file line,
FinishRecord/reset, delimiters/quotes/escapes sharing UTF8 prefixes, Unicode,
CR/LF/CRLF modes and boundaries, multiline and very long records. No capacity
replacement/refund while Feed's source span is live. Existing decoded file windows,
parser scratch, final fields/rows, retained headers/aliases and pending I/O stay
funded through actual awaited cleanup. Scope borrowing, terminal refusal, checked
representation bounds and bounded host chunks remain unchanged.

Contract tradeoff: private buffer growth can include raw syntax bytes before
compaction, changing conservative peaks/denial points. Document material capacity
changes; never silently weaken admission or regain work after refusal. Borrowed
host allowance pays every capacity/replacement peak; no hidden default allowance.

## Bounded acceptance and decision

First source proof above; stop/refine if it needs another ownership layer or an
alternate parser. Focus existing Release CSV/stream/window/newline/codec tests,
real in-flight host cancellation/refusal/retained data/budget reuse. Add only real
new gaps: multiple records in one file-line with carriage returns; failure/reset
after an in-place line; interrupted native read rollback of a pending multiline
record, verified against baseline/CPython where applicable. Never relax timeouts.

One <=180s complete-script source pair with identical-source roles: native and
ordinary CSV warm/fresh/completed-host async, long-field CSV and unaffected text
as size permits. Reuse workers/goldens/quiet/joins/zero balances; no full lane.
Record actual eliminated work/storage, elapsed/alloc measurement limits and
retain/refine/withdraw/regression-debt decision. Full platform/original stress/
installed consumers at a coherent retained delivery. Broader performance and
independent regex work continue afterward.
