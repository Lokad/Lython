# Module/call source gate: existing paths already consolidated

R478,2026-11-17UTC, d8611c85. **No runtime prototype or new performance claim.**
Public repeated attribute/getattr identity checks on all four JSON functions pass
both APIs and shared-budget cleanup; independent CPython3.13.2agrees.

PyModule.TryGetCachedMember already preserves immutable factory results. The
new expressions in JsonModule.TryGetMember are cache-miss factories, not wrapper
allocation per record. Explicit JSON fields behind that cache would add duplicate
machinery. Default loads/dumps already bypass unused option binding, default
options already share immutable metadata, and short general calls already use
stack-backed borrowed packets. No discarded internal architecture is enforced.

Local imports already compile ExecutableScript and execute its Code under both
APIs since840621405. The later import-consolidation task in PLAN was stale and
is removed; explicit mirroring for mutable module exports/escaping functions stays.
Public compilation already retains only real lowered consumers, not whole AST/
lowered snapshots. These are source corrections, not newly delivered optimizations.

The bounded gate ends here. Temporary two-case diagnostic source/build/logs are
archived, then removed from the test project. Existing module identity and script
mutation regressions remain. R477 qualification continues independently; no full
unchanged qualification, VM timing or alternative cache/registry introduced.
Publication held and broader design selection remains active.

[Source decision](source-gate.json),[source identity](source-inputs.json),
[independent Python program](cpython-golden.json),
[temporary diagnostic/build/logs](diagnostic-and-checks.tar.xz).
