# Q02 next migration boundary — source51db4209

Prepared after the R03/R05 safe delivery point. No production prototype for this
migration exists yet. Combined CI42174873217 is independent/running; source edits
for a later milestone must not be called qualified by that receipt.

Source decisions, not another inventory/profiling campaign:
- PyHashProtocols.PrepareKeyHash is the one ordered tuple/slice semantic walk.
  Never preclassify the complete key to choose async; that can inspect later
  elements/depth before an earlier callback and undo Q01's Python-order fix.
- GetInstanceHash synchronously binds then invokes __hash__. Existing
  BindForInstanceAsync/ICallable.InvokeAsync support actual descriptor/callback
  suspension. Use explicit StructuralTraversal for every continued tuple/slice
  guard; thread-static ambient state must not cross an await.
- PyDict.TryGetBuiltinKeyAsync has prepared native/contextual lookup and funded
  snapshots, but prepares hashes synchronously. Its no-protocol-table shortcut
  can synchronously hash a protocol query. Caller protocol-key detours also exist.
- Generated dataclass hashes already own an immutable defining-field plan and
  fund/read all attributes before hashing; ReadFieldAsync exists for other
  generated methods. Explicit slice.__hash__ is another synchronous semantic
  hash entry, not an independent hashing algorithm.
- Mutating dict/set constructors/writes, contextual tables and cache keys have
  broader synchronous call chains. Their current restrictions remain explicit;
  do not silently claim that async reads make every key operation suspendable.

Select a coherent read/hash milestone: public hash, tuple/slice nested hashes,
explicit slice hash, generated dataclass hash, and dictionary get/subscript/
membership (including existing namespace bridges) through one async-capable
preparation boundary. Extend set read membership only by reusing that same table
lookup, not a second resolver. Native keys complete without allocating a pending
continuation; the synchronous engine retains its measured direct path. A completed
prefix may use ValueTask; allocate/hold continuation state only on actual pending
work. Do not force every sync node through a generic async-mode dispatcher or
recreate slower call-control designs. Share pure normalization/combination facts;
preserve HashCode input order, -1 normalization and unhashable source origins.

Remove displaced public sync-only hash registration and protocol-query sync
detours in this stated boundary. Rename/adapt the existing builtin-key lookup
for general prepared keys rather than introducing a parallel query framework.
Generated dataclasses keep all attribute reads before all field hashes; pending
field snapshots remain admitted/borrowed until completion and cleanup. Stored
candidate snapshots and current-entry/sequence revalidation survive guest mutation
and real suspension. Do not transfer/refund pending inputs early.

Acceptance: immediate and gated hashes/descriptors; native/default object hashing;
nested tuple/slice/dataclass ordering; early error before later unhashability;
immediate and reversed/cross-thread cycle cleanup; same-hash equality mutation;
identity-hit and empty-table hashes still invoked. Actual in-flight cancellation
and refused admission are terminal, preserve first origin, suppress later guest
effects, await users, release pending scopes and return shared allowance to the
compilation baseline/zero. Include retained source/result aliases and reuse after
failure. Existing sync/async key goldens stay unchanged except newly supported
pending behavior. Writes/cache restrictions remain documented and honestly tested
only for their independent semantics, not frozen rejection expectations.

Completion: delete the identified detours, document the exact supported boundary,
focused checks on both actual runtimes; one short native completed-key complete
script comparison/control and a separate actual-pending diagnostic. Keep original
failures/adverse results and ordinary-GC/tiering/full goldens/ownership/joins.
No speedup predicted and no new benchmark framework, background execution,
watchdog/quota, tracing, memory subsystem or publication. A source-level mismatch
requiring broader mutation redesign is a stopping point for this boundary, with
a concrete contract decision recorded rather than silently expanding the project.
