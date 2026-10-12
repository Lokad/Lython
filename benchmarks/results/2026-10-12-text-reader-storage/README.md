# Text-reader storage consolidation

At `3c7a1124`, 500 complete text-summary jobs at a 262,144-byte cap finish with
explicit close but fail with MemoryError after dropping unclosed readers. The
matched retained-reader control rejects as expected. Original source/binaries
and the failed receipt are preserved. These are correctness controls, not timings.

Runtime producer **b031caaee0da57db59ece2daa2b36b3fd7269067** gives each actual
read state one mutable storage snapshot in the existing shared owner pool:
192-byte conservative shell, raw carry and current decoded window. Existing
mutation notifications keep that snapshot exact as buffers change. Emitted
values retain independent payload ownership in the same pool.

The private per-reader reclamation pool, its emission sweep counter and its
abandonment registration are removed. All three complete-script controls now
pass their expected outcomes at the unchanged cap. Dropped unclosed storage
reclaims; retained readers/iterators/methods stay funded. Close refunds buffers
once and refreshes the snapshot; failed unpublished construction refunds storage
and its entry immediately. Shared pool capacity remains funded for the run.

The production change adds 27 lines and removes 17. It creates no new ownership
tier, scalar identity map, refcount, finalizer, switch or unsafe/native runtime
code. Conservative state-shell funding may reject very small caps earlier;
SPEC.md records this contract. Allocation preflights, cancellation, host mediation,
managed stack/thread policy and forced exhaustion relief remain enabled.

Committed Release validation passes **148 checks** at b031caae: 51 white-box and
97 public cases covering exact reclamation, escaped values, idempotent close,
failed admission, priming cancellation, codecs/alignment, complete scripts,
publication and failed-flush retry. Both comparison workers match CPython 3.13.2
on all **12 representative cases**, including warm/compile-run/compile smokes.
These groups take 3.157 / 8.427 / 4.170 seconds including cleanup, each with a
30-second external cap; none qualifies performance or embedding latency.

The first local full Debug gate hit its separate ten-minute limit and remains
**incomplete**. Its preserved partial log shows three codec tests assuming the
old constructor funding and a 20-second timeout in an unrelated set test. The
set cancellation/failure pair passes in isolation; this does not erase its timeout.
Test-only **fd741bd51ff81758121502b59cdb8bd2cc676350** preserves each codec test's
exact original post-construction headroom with a funded pressure reservation.
All **2,140 white-box Debug tests** and the **26 codec/ownership Release checks**
then pass. Production is identical between these producers. No full public Debug
or single-head full Debug pass is claimed from partial evidence.

Initial raw-text fixture/shared-capacity assumptions and their corrected results
are retained separately. [evidence.json](evidence.json) identifies producers,
binaries, scopes and receipts. Raw data stays under ignored
`.git/agent-notes/text-reader-storage-20261012/`.

Forced-GC independence, parser/lease closure beyond these paths, host-impact
qualification and representative comparative timing remain open. Reusing the
shared pool fixes storage lifetime; it does not establish better latency.
