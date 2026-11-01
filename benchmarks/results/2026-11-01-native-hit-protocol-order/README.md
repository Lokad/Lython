# Unfixed native-hit/custom-key callback ordering (B40)

Four isolated probes on 2026-11-01 demonstrate a remaining mismatch after B38.
An earlier custom key hashes like native `'x'` but initially compares false, so
both entries exist. Its equality callback is then armed to raise ValueError.

| Operation | Lython | CPython 3.13.2 |
| --- | --- | --- |
| `data['x']` | Returns 2, no callback | Calls equality, raises |
| `data.get('x')` | Returns 2, no callback | Calls equality, raises |
| `data['x'] = 3` | Mutates value, no callback | Calls equality, raises before mutation |
| `data.setdefault('x', 3)` | Returns 2, no callback | Calls equality, raises |

[Inputs](native-hit-controls.json), [raw results](native-hit-results.jsonl) and
[identity/boundary](summary.json) are preserved. The probe contains B38's complete
runtime body, unchanged at 1c369000; it was built before the source commit and
therefore carries c57f9752 metadata. Exact core hash is recorded. CPython uses
isolated UTF-8 mode. No performance, physical-memory or finite-budget result is
inferred; B38's independent ownership/refusal checks remain separate.

**Unfixed, high priority in PLAN.md.** B38 corrects native misses; an existing
native hit still bypasses earlier custom collisions. Simply scanning every
protocol key first would introduce the converse ordering error when a native key
was inserted earlier. A coherent correction needs shared ordered resolution,
reentrant mutation/error semantics, cancellation, conservative admission and
native-only fast paths. No new ledger, tracing machinery or speculative patch is
introduced here. This evidence is distinct from rejected performance prototypes.

Source review confirms that string-specific asynchronous lookup can genuinely
await custom equality. A correction must preserve that capability with explicit
context through suspension, rather than generalizing write/hash restrictions or
holding thread-local comparison provenance across an await.
