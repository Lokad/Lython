## Priority1 — B151 bulk UTF8 text operations

SOURCE GATE COMPLETE; baseline39061f17 freshly built/frozen before mutation.
B150 is retained and delivery CI runs independently. Its source, two timing blocks,
allocation/peak diagnostics and all adverse outcomes remain in the evidence above;
no more confirmation or unchanged qualification replay. B149 sharing stays absent.

Hypothesis: casing/splitting still spend substantial time on per-character ASCII
work even though CLR character conversions are already removed. B146 sampled
casing11.98% and whitespace split10.60% inclusive guest CPU. Overlap/useful work
prevent a promised summed speedup. Target complete text-summary/format/file scripts.

1. Use managed BCL ASCII case conversion in the existing admitted write window
   for lower/upper. Prefix detection is ALREADY a managed bulk search; do not
   reimplement it. Remove redundant per-byte classification/mapping for those
   operations. Keep scalar/context/expansion rules and title/capitalize/swap paths
   with real semantic callers. No global string fields, sharing, caches or new API.
2. Scan ordinary ASCII whitespace/word runs with managed span search in both
   directions, falling back to existing scalar handling for Unicode. Use the same
   29-scalar Python inventory; fixed search metadata is library-owned. Preserve
   original-subject byte/scalar positions, maxsplit and range admission. Replace
   displaced per-rune ASCII work, not a parallel split backend. Keep chunk checks
   <=4096 bytes; no execution-fuel/checkpoint counters.
3. Acceptance: all Unicode casing/final sigma/expansions, ASCII controls, all Python
   whitespace scalars, split/rsplit/maxsplit/empty edges and mixed Unicode chunk
   boundaries. Preserve preflights, existing output buffers/owners, replacement
   overlap, aliases and actual cancellation/refusal. Native search must not trigger
   speculative oversized storage growth. Fully managed span APIs only.
4. Completion: focused Release on both targets, one frozen complete-text warm+
   compile-run comparison, ASCII/Unicode/long-input controls and unchanged CSV/JSON/
   report controls. Record allocated bytes and any peak change. Bounded evidence-led
   refinement/confirmation allowed; retain failures and all adverse controls. Decide
   retain/refine/withdraw on coherent removed work and actual whole-script costs.
   No new framework, policy switch, second VM or universal string rewrite.

Refresh representative CPython at this combined milestone after B151 retain/withdraw,
using existing command-v2 tooling and separating compilation/warm/I/O/cleanup. If
B151 is withdrawn, refresh retained B150 source. This avoids another full comparison
for each intermediate implementation. No producer publication blocks this work.

Secondary source gate: static operand addressing from existing block stack facts.
Runtime Count also controls iterator disposal/active call borrows/exception clearing.
Require one clear cleanup invariant before code; do not force it with a parallel
metadata/dispatch plane or an in-flight phase framework. B147 lessons remain.

