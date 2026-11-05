# D27: JSON string bulk runs, before code

2026-11-05. Baseline cf3dc9cd / frozen43fdf658; no active prototype or CI.
D26 source gate finds no argument arrays or removable frame in exact d.get RHS;
do not add a second guarded expression route for dispatch count alone.

Hypothesis: the existing single JSON string encoder scans ordinary printable
ASCII one byte at a time even though it copies complete runs. Replace that scan
with the BCL managed span search over a shared immutable printable-ASCII set.
Keep the same encoder, rune decoder, escape funnel and governed output builder.
No native code, new dependency, opcode, owner, cache or public option. Remove the
per-byte ordinary-ASCII branch/work, not Unicode validation or output admission.
This is an algorithm change; allocation should remain flat. Existing report
JSON-string6.13% leaf/inclusive31.5% includes useful work, not predicted savings.

All Unicode and malformed UTF8 retain strict Rune.DecodeFromUtf8, including
ensure_ascii=False. Preserve DEL/control escaping, quotes/backslash, lower-case
hex and surrogate pairs, dictionary keys and dumps/encode/iterencode. Search
windows stay <=4096 input bytes with active cancellation before every window;
one complete scalar may cross a boundary. Output growth/retained capacity,
replacement overlap, terminal first refusal and async hook cleanup remain on
the existing routes. No storage-contract change intended. Immutable search
metadata is small shared implementation metadata, not a guest result or budget.

Focused Release: JSON emission/roundtrip/escape/strictness, cancellation, malformed
UTF8 and admission/retained output/refunds. Add only missing escape or boundary
coverage, preserve useful existing tests/timeouts. Then both actual targets.

One bounded <=240s existing complete-job source pair: report warm/async/fresh,
JSON warm, text warm control and report compile control. Two unchanged baseline
roles, normal GC/tiering,6s warmup/2s settle/9rotated200ms batches. Exact binaries,
independent CPython goldens, quiet endpoints, zero balances and joined users.
Use existing quiet VM/tooling, retain adverse data, separate elapsed/allocation/
compilation/control variation. No CPU attribution, additive gains or new external
ratios. Stop with retain/debt/withdraw; bounded confirmation only for a material
uncertainty or concrete correction. Retention requires full delivery at a coherent
checkpoint; whole-script objective remains open and regex proceeds independently.
