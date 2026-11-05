# D22 source decision / D23 final-word casing, before code

2026-11-05. Baseline committed f7fed42c; parser delivery41940441052 remains active.
D19/ab487dfa fully qualified41938239957, all platform/framework/stress/package
steps green, cached. Publication held. Previous goal turn is PROGRESS: retained
D19/D21 implementations, current compiler attribution and immutable observations.

Bounded D22 source assessment: admission/token checks are already inline; local
forwarding already proves bound, noncaptured locals inside plain function blocks.
Its extension to dominant module loops needs global-mutation/snapshot proof and
does not follow from D18 checked-read fusion. All maintained helper bodies already
have native roots, so a general call/frame rewrite is not selected from their
allocation slopes. Preserve arena/control/layout/fusion adverse evidence. No new
guard suppression, origin tags, frame mode or whole-heap design is justified.

Select a complete text pipeline instead. The maintained text script calls
words_from_line(line) -> line.lower().split(). Existing full-script profiles put
substantial useful work in lower/split (B220 lower4.45%leaf/whitespace3.78%; earlier
guarded/native shapes22.5% inclusive). Native evaluation materializes/funds an entire
cased line, then scans/copies it into final words. No observer can see that
intermediate in an already-proven, exact-string native body. Counts are not a
speedup forecast. Current ordinary calls/captured contexts keep real lifetimes.

Proposed simplification:
- Reuse the existing full-Unicode CaseOutputCapacity/WriteCaseUtf8 and the ONE
  whitespace collector/materializer to case directly into final word buffers.
- Cover native lower/upper followed by zero-argument whitespace split, not an
  ASCII-only path. Recognize it only within an already guarded native expression;
  no opcode, code-object field, descriptor, second evaluator or backend is added.
- Eliminate the intermediate full-line PyString/backing, its capacity admission
  and copying, and recursive execution of that redundant producer. Ordinary
  standalone casing/split implementations remain real consumers, not dead copies.
- Shared range scratch carries final capacity (three ints vs two); its existing
  conservative16B/entry admission covers12B, including replacement overlap.
  Admit total final-word capacities/metadata once, then transfer using existing
  FromOwnedUtf8(storage,byteLength). Lists/escaped aliases keep their owners.

Semantic proof: Python's explicit29 whitespace scalars are case-stable, are not
Case_Ignorable/Cased and break final-sigma context; lower/upper mappings introduce
no whitespace. Thus casing each complete whitespace-delimited word uses the same
context and boundaries, including Unicode expansion/final sigma. Do not fuse
custom separators/maxsplit, arbitrary receivers or unproved calls. Existing native
guards precede all work; custom protocols/real suspension retain the ordinary
engine. No fallback/retry after effects or admission. Preserve evaluated source,
exceptions, function traceback and active token throughout output construction.

Contract tradeoffs: no allocation for an unobservable cased intermediate, so
admission sizes/order and failure spans can change; actual first refused final-
storage request stays terminal and is never retried. Retained capacities, scratch
overlap, cancellation before allocation and awaited users/refunds remain funded.
No hidden allowance/tracing/GC or new memory machinery. Ordinary split scratch
elements grow8->12B but remain below the same conservative funded16B estimate.

Focused acceptance: independent CPython goldens for all29 whitespace scalars,
Greek sigma/Case_Ignorable neighbors, dotted-I and sharp-S/ligature expansions,
empty/no-word/large-word results, explicit separator/maxsplit controls, custom
method order/failure/actual suspension. Existing string/native-return/cancellation/
refusal/retained-result/shared-budget tests; add only genuine missing cases.
Confirm full capacity and actual byte length are independently retained; no
copy/shrink or refund of live storage. Both actual targets at this checkpoint.

One<=240s existing-tool whole-source pair: textwarm/async/fresh, JSONwarm and
reportasync controls, textcompile. Normal runtime/two unchanged baseline roles,
6s warmup,2s settle,9rotated200ms batches; exact source/binaries/CoreLib/goldens/
quiet/balance/fixture removal/owned joins. Separate allocation, elapsed, compiler
debt and uncertain controls; no new CPython ratios/RSS/CPU attribution. Stop with
retain/debt/withdraw; one refinement needs a concrete cause, not a local hint
series. A retained delivery needs focused final-source checks then batched full
platform/Stress/package gates. Whole-script objective remains open.
