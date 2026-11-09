# R138 closed nested helpers

Hypothesis: lexical placement alone currently forces eager invocation-context
construction and prevents existing native execution of closed helper bodies.
Compiler closure/captured-local facts already establish when storage is needed.
Remove two blanket parent-presence conditions; preserve existing class/annotation/
type-parameter/comprehension/handler exclusions and promotion before protocols.
No new dispatch, cache, owner, binder, scalar representation or public option.

Current complete CSV/text/report programs wrapped unchanged in ordinary main().
Golden fixtures/output unchanged; source/hash deliberately changes and is verified.
JSON/search ordinary scripts are controls. Both APIs use actual I/O validation,
result cleanup and shared-budget baseline checks. Local actual allocation is
separate from diagnostic desktop timing. One quiet VM source pair selected if
construction difference/correctness is demonstrated; no general speedup assumption.

Ordinary nested closure chains stay retained. Genuine captured cells and class
cells remain on their existing paths. Check current globals, unbound locals,
locals promotion, class-cell control, async rebound protocols, cancellation,
first refusal, pending/escaped aliases and managed-depth cleanup.
