# Ownership representation assessment

This assessment widens R01 beyond ordinary operand transfers. **No collector or
logical refunds are enabled, and forced GC remains open.** The existing native
and lowered execution paths retain guest values in helper locals, compiler
state, delegates and unowned bridge objects. A visitor limited to owned values
or current namespaces cannot establish the required complete invariant.

The finite audit inspects the immutable Release DLL from `f57003a8` (production
`8a6cc16d`), assessed at source HEAD `220762c7` on 2026-10-13. It reads static
type, field and method metadata and decodes IL call sites; it does not execute
guest code, inspect live heaps or propose runtime reflection. Source and tool
identities, all 521 production C# file hashes and the exact Release package
SHA512 are recorded in [inventory-summary.json](inventory-summary.json).

| Static inventory | Count |
| --- | ---: |
| Production assembly types / methods | 2,758 / 24,045 |
| Runtime / frontend types | 2,170 / 507 |
| Concrete snapshot owners / finalizable owners | 34 / 1 |
| Instance fields containing references | 8,079 |
| Instance delegate fields | 99 |
| Compiler-generated types | 1,270 |
| Methods directly calling admission | 422 |
| Methods transitively reaching admission | 8,914 |
| Conservative admission/interface/delegate reachability | 9,752 |
| Unresolved IL method tokens / interface maps | 0 / 0 |

These are metadata counts and conservative call-graph results, **not live-root
counts or a requirement to rewrite every method**. Immutable compilation data,
host capabilities and accounting back-pointers need explicit exclusion rules;
they do not automatically become guest-storage children. External libraries are
not analyzed transitively. A local signature cannot establish which C# values
remain live at a particular pressure point.

The first audit omitted dependency DLLs and failed before resolving types. After
supplying the already-pinned dependencies, a runtime-only audit completed but
missed frontend capture bridges and async wrapper-to-state-machine edges. Its
`SumAsync` classification was false. The corrected full-assembly audit adds
explicit async/iterator entry edges; both `Sum` and `SumAsync`, plus native
`TrackCallResult`, now reach admission. Original artifacts and failed receipts
remain private; the corrected audit completes in 2.494 seconds, without unresolved
tokens/maps. All source hashes and these concrete witnesses are verified.

| Demonstrated obligation | Existing evidence | Replacement requirement |
| --- | --- | --- |
| Native evolving values | `Sum` local `total`; `<SumAsync>` state field `<total>5__2` | Preserve current helper state while the next generator item executes or suspends; original arguments alone are insufficient |
| Native result registration | Bound/receiver wrappers call `TrackCallResult` before returning to the executable handler | Publish result before registration admission, not only later observation |
| Delegate receivers and captures | `PyReversedIterator._getIndex` retains its list/string receiver; bound callables also store implementation delegates | Named receiver/state edges must expose all retained guest values; an attached receiver does not prove arbitrary capture completeness |
| Runtime-created frontend bridge | `LoweredCapturedExpression.Value` is an object reference | Include computed-expression/outer-iterable state; treating all frontend objects as immutable control leaves is unsound |
| Expanded calls and prepared operations | Existing accumulators, captured-read maps, builders and materialized values | Publish working storage at callback/admission/suspension and withdraw it after transfer; no universal retained extent |
| Cycles and persistent run state | Existing modules, namespaces, closures, exceptions, structural guards and caches | Traverse audited fields without getters, guest iteration, hash/equality callbacks or accounting/registry back-edges |
| Finalizable storage | Gzip owner snapshots include DeflateStream working state and buffers; long weak handles wait through finalization | Release unreachable resources through a guest-free abandonment path before refund; normal host-writing close is unsuitable at pressure |
| Exhausted-budget traversal | Owners/bridges and roots have different shapes; workspace has no completed funding rule | Fund bounded work/visited storage before exhaustion and derive capacity from admitted storage; never allocate uncharged scratch during reclamation |

The public root corpus gains reversed-list and generator-expression outer-input
cases, each with retained rejection and unconstrained completion through sync and
genuinely delayed async execution. All **36 cases pass**, including eight new
cases. The 28,000-byte cap and 120,000-byte controls remain fixed. These tests
protect two missing bridge families; they do not prove exhaustive root coverage
or demonstrate a GC-independent implementation. No timing comparison or host
latency campaign runs in this assessment.

The representation decision is to continue with explicit activation/operation
state and audited storage-child descriptions in the existing single ownership
inventory. Reuse frame operands and existing native working storage, expose
named callback receiver/state, and withdraw completed references at actual
transfer boundaries. Ownership records must represent actual storage, not scalar
identity coupons/refcounts. A replacement must unify roots, children, cycle-safe
funded traversal and resource abandonment **before any refunds are enabled**.
Only then can weak handles, forced-generation escalation and allocation-only
retry eligibility disappear. All managed stack, host mediation, cancellation,
preflight, streaming, alias and projection guarantees remain acceptance gates.

This stops the finite inventory assessment. The next implementation is the
combined representation and funding rule, not another sequence of creation-scope
or retry-flag patches. Stop a candidate on an implicit reference, unknown bridge,
guest callback during traversal, unfunded workspace or native-streaming regression.
Explicit activation state is a larger ownership redesign justified by these
counterexamples; the supported managed stack policy does not depend on it.

The complete raw inventory is private (its exact hash/size is recorded). To
reproduce the static assessment, build [Audit.csproj](Audit.csproj), supply the
immutable Lython DLL beside its pinned dependency DLLs, then invoke `Audit.dll`
with the DLL path, matching production source directory and a fresh JSON output
path. [Program.cs](Program.cs) is the frozen audit source;
[audit-process.receipt.json](audit-process.receipt.json) records its owned run.
The audit is an assessment artifact, not a runtime dependency or benchmark framework.
