# Governed memory

Lython admits conservative storage estimates against a host-supplied
`ILythonMemoryBudget`. Executions, compiled programs and returned results use
explicit owners sharing that allowance. The standalone `LythonMemoryBudget`
provides the same contract without a host adapter.

```csharp
var budget = new LythonMemoryBudget(16 * 1024 * 1024);
using var compiled = new LythonEngine().Compile(source, budget);
foreach (ILythonHost batchHost in batchHosts)
{
    using var result = await compiled.RunAsync(batchHost);
    // Consume the result and relinquish its aliases before the iteration ends.
    Consume(result);
}
```

`Compile(source, budget)` reserves a rough estimate before compilation and keeps
the compiled representation and its bounded diagnostic cache charged until the
script is disposed. Refusal produces an invalid script with diagnostic `LA0008`.
Local-module compilation also admits a coarse estimate before parsing/lowering.
Once prepared code can be exposed to guest callbacks, its estimate stays charged
for the batch even if import execution fails; parsing/lowering failures return
their unused admission. This preserves escaping code aliases without tracing them.
Runs use that borrowed budget unless `LythonRunOptions.MemoryBudget` supplies
another. One-shot `LythonEngine.Run`/`RunAsync` disposes its temporary compilation
after the invocation and its cleanup finish.

Each invocation owns its working storage. Projection allocates under a result
owner established before execution; this owner is a sibling of the invocation
owner and survives execution cleanup. Disposing `LythonExecutionResult` clears
its payload references and releases its estimate. Results, including failure
diagnostics, retain their admitted estimates. A small resource-refusal outcome
can be returned without payload admission when no allowance remains; its wrapper,
fixed diagnostic and borrowed original source are part of host/control headroom.
Refused compilation still preserves the borrowed budget association. Payload accessors reject use
after disposal.

Extracting `ReturnValue`, stdout or diagnostics does not create another owner.
Keep the result alive until **all extracted aliases and asynchronous users have
finished**. Likewise, await all runs before disposing a compiled script, and
relinquish extracted diagnostic aliases first. Disposal does not cancel work.
Serialize disposal against every user of the owner; concurrent admission does
not authorize concurrent disposal of live storage.

The supplied budget is borrowed. Lython disposes only scopes it creates. A host
may provide `ResultMemoryBudget` when an invocation's parent will end before
its output; that longer-lived owner must be chosen before running. Ancestor
disposal invalidates descendants and refunds their reservations, so descendants
and their storage must never outlive an ancestor. There is no automatic ownership
transfer or refund triggered by CLR collection.

Eligible executable function calls bind parameters into one invocation-owned
local-capacity buffer. Unpooled buffers admit conservative capacity and metadata
before allocation or binding, and return that allowance after binding/frame-entry
failure or body completion, including awaited callbacks and cleanup. Small cleared
buffers use the existing bounded control-storage pools. Cells, snapshots and
returned values retain their separate funding after the buffer is cleared.
This preflight can refuse before an argument TypeError or an effectful default
lookup that previously happened first. Funded calls preserve Python binding and
execution behavior. Modules and functions requiring namespace mirroring retain
their separate local-storage route; this is not guest-reference tracing.

String-builder materialization reserves its owned UTF-8 result before allocating
the copy, while the working buffer remains charged. A failed conversion retains
the builder and its funding. A successful consuming conversion releases only
working storage; the result and its aliases remain funded under their owner.

Accumulated text-file reads use the same governed UTF-8 working builder. Growth
admits replacement capacity while the old buffer is still charged; materializing
the returned string admits its copy before releasing that buffer. Each read
releases working storage in finally after completion or awaited failure/cancellation.
No duplicate retained UTF-16 window or ungoverned CLR accumulator is maintained.
The bounded input codec/newline/carry paths retain their existing implementation.
A refused append does not advance its cursor; earlier consumed windows remain
consumed and refusal still fails the command. This is not transactional rewind.
Closing a reader does not refund returned strings or their retained aliases.
Capacity overlap can require more unused headroom and reject earlier. Long streams
still require bounded host-driven execution batches; dropping guest references
does not release arbitrary output ownership.

Array-backed list growth reserves full replacement capacity against both the
execution limit and shared host allowance before allocation. The old backing
remains charged until the copy finishes. Failed admission leaves its contents
and funding unchanged; failed replacement refunds its reservation. Successful
growth retains the existing final capacity estimate and returns temporary
overlap funding. Budgets that cover only the final size can therefore refuse
earlier. `csv.DictReader` fills excess-column lists directly into their admitted
capacity, retaining the original cells without an intermediate reference array.

Bounded empty-pattern string replacement admits a checked capacity for the
required insertion prefix and copies the untouched UTF-8 suffix in bulk. It
retains source, replacement, builder and result funding during overlap without
constructing scalar strings for the suffix. Oversized bounded results now use
the same checked string-size `RuntimeError` guard as unlimited replacement,
before partial construction. Admission refusals still fail the command.

Short decimal text/bytes that parse completely into a signed 64-bit value use
the existing bounded scalar control headroom. At most 20 UTF-8 bytes are examined
without decoding or allocating major scratch; terminal admission state is still
checked. Smaller real working storage can fit narrower allowances. The result
remains a Python integer, with ordinary stored-slot/result ownership. Other forms,
bases and overflow use the general path below; failed admission never selects an
alternate parser or recovery path.

General integer parsing from text or bytes admits decoded UTF-16 capacity, result magnitude
and computation overlap before decoding. Whitespace, sign and prefix processing
uses spans rather than substring copies. A major result commits its portion of
the existing reservation before scratch is refunded; there is no second admission
after computation. Raw input capacity is deliberately conservative, so padded or
prefix-heavy inputs can reject earlier even when their integer is small. The
input and retained result keep their existing owners. Small bounded magnitudes
retain the control-headroom policy.

JSON numeric tokens use the existing UTF-8 span conversions for ordinary integers
and floats. These do not require a token-sized temporary reservation. Numeric
hooks receive text admitted and committed before invocation; that text stays
funded through suspension and escaping aliases. The allocating large-integer
text fallback retains a four-times-token working reservation before decoding,
overlapping its existing magnitude preflight and result ownership. Removing unused
scratch lets some commands fit smaller allowances; magnitude admission now
precedes fallback scratch admission. Identical tiny-budget thresholds and the
previous ordering of those failures are not guaranteed. Any refused admission
still fails the command without retry.

Float parsing from text or bytes admits decoded UTF-16 capacity and a possible
normalization buffer before decoding. One buffer normalizes Unicode decimal
digits/whitespace in text and removes valid underscores; bytes retain ASCII
decoding rules. Trimming and nonfinite comparisons use
spans. Successful conversion returns that scratch immediately; the input keeps
its owner and the fixed-size result uses existing scalar headroom. Invalid input
also admits repr/message overlap before constructing the error. Its message can
escape, so a conservative estimate remains charged until the execution/result
owner ends, even if Python catches the error. Long inputs and repeated invalid
conversions can therefore reject earlier under tight allowances. Refused error
construction is terminal and refunds its scratch; it never substitutes a shorter
message or retries admission.

`csv.reader` and `DictReader` reuse that parser for nonempty unquoted fields in
`QUOTE_NONNUMERIC`. Their UTF-8 field payloads remain conservatively batch-owned
after conversion; numeric row/container slots keep their ordinary funding.
Conversion scratch ends immediately. Earlier rejection under narrow budgets is
possible; bounded host batches remain the ownership boundary for long streams.

With explicit field names, `DictReader` admits its source/parser and copied field
names at construction without reading or retaining a first data row. Data-row
admission and host I/O occur on pull. Returned rows and fields keep their batch
owner after EOF; disposing or exhausting a reader does not refund live aliases.
Inferred field names still require the existing header read during construction.

Numeric conversion hooks share the existing type-slot/descriptor invocation
paths. `RunAsync` keeps their owners alive until suspended callbacks and cleanup
finish, including cancellation or terminal admission refusal. Converting an
existing float, or returning one through `__float__`, preserves that object and
its existing storage ownership rather than copying it. No new owner or independent
allowance is introduced for protocol dispatch.

Short escaped JSON strings can decode into bounded managed working storage
before their governed UTF-8 result copy. The conservative token reservation is
unchanged, and the surviving result still acquires its own allowance before
allocation. Larger tokens retain the existing decoder; this optimization does
not introduce a pool, new owner or different tight-budget contract.

Ordinary integer addition and sums admit a conservative major-result estimate before the
CLR computes it, including carry/sign headroom and computation overlap. The result
commits its portion of that existing reservation without a second admission;
remaining scratch releases after the operation. Retained major values keep their
execution/result owner. This covers ordinary addition and generic sums as well as
mixed CLR integer inputs; the byte-sum kernel shares that result path. Small
bounded values keep the control-headroom policy.
Counter count addition uses the same path: updates reserve against the counter's
owner before replacing a count, binary addition retains the result's existing
owner, and totals reserve against the current execution. Newly constructed major
counts remain funded even when a container later removes its reference, until
their finite owner ends. Ungoverned internal containers retain their existing
behavior; public executions establish owners before exposing containers.
Major operations can reject earlier under tight budgets, including opposite-sign
operands that would cancel, because admission uses an upper bound and accounts
replacement/scratch overlap. No scalar identities, new scope or reclamation
mechanism is introduced.

Executable starred-tuple expansion uses an owned mutable working list before
constructing its immutable result. Working and result storage are funded together
during conversion. Their estimates may remain charged until the execution/result
owner ends, so narrow allowances can reject earlier than with the former lowered
temporary-list path. Empty results reuse the existing immutable empty tuple;
no new cache or identity tracking is introduced.
The same finite-owner policy applies to incremental set/dictionary display
construction: unpublished partial storage can remain charged after a caught
construction error until its execution/result owner ends. Admission can therefore
refuse earlier than a lowered path that retires private constructor storage.
Allocation preflights and deterministic owner cleanup remain; retaining a result
or pending asynchronous user keeps the owner funded.
Executable f-strings keep their formatted parts funded until the final join,
admitting output capacity while those parts still exist. Intermediate estimates
can remain with the finite execution/result owner after a caught formatting or
join failure. Earlier denial than the incremental lowered builder is possible;
pending protocol callbacks and escaping text retain the same ownership protection.
Root `format` and default `object.__format__` keep the receiver, specification and
callback result funded through suspension. A guest formatter's existing string
can be returned directly with its owner; conversion does not require decoding
and re-encoding that storage. Retained results keep their surviving owners until
all users release them. String-method formatting callbacks retain their existing
synchronous boundary; root formatting and f-strings can await host I/O.
Executable lambdas retain shared closure cells rather than forcing complete local
namespace mirroring. Cell-array capacity and newly retained cells are admitted
before allocating the captured array; denied capture publishes no funding marks.
Eligible lambdas in eager comprehensions use cells backed by the existing live
binding namespace. Capture preflights cover the array and new cells together;
the namespace's live-store state is published after allocation. Ending its
binding lifetime restores lookup without refunding retained cells or values.
Cell metadata can be conservatively charged per capture even when bindings are
shared; this can change narrow admission thresholds. No registry or extra owner
is introduced, and the caller's full namespace need not be mirrored for these
bodies. Ordinary dynamic/generator routes can still require that mirroring.

Generator-expression assignment targets use prepared global/nonlocal metadata
and the existing name-storage owners. Nonlocal target-map capacity is admitted
before constructing the retained iteration scope; failed construction returns
that estimate. Global-only expressions need no target map. These estimates remain
charged for the execution/result lifetime, so narrow allowances may refuse
earlier. No bindings are copied back at yield or during cleanup. Mirrored generator
namespaces stay authoritative for retained cells while suspended; only captured
slots are refreshed on resume. Existing frame admission funds those cells.
Eligible executable generator expressions reserve conservative frame/stack/cache,
namespace, cursor and nonlocal-map estimates before construction or effectful
outer acquisition. Existing closure capture/retention admission funds escaping
cells and their defining namespace; no new memory owner is introduced. The
retained frame starts with the already acquired outer cursor. Early close/throw
and entry refusal/cancellation await its native cleanup before returning; failed
construction refunds its frame estimate only after cleanup finishes. Failed outer
acquisition avoids the separate defining-namespace retention charge; successful
acquisition funds retention before publication. Surviving
cell/namespace charges can remain conservatively with the finite owner. Larger
frame estimates can cause earlier denial than the former lowered iterator.
Operand buffers admit their actual initial capacity, then reserve full new
capacity before replacement while the old buffer remains charged. Successful
replacement relinquishes the old buffer immediately; failed admission leaves it
unchanged. Terminal cleanup clears/releases the buffer after awaited native
disposal; yield retains it and generator ownership snapshots include live buffer
capacity. Compiled instruction storage is not charged again as an execution
buffer merely because a large deferred body exists. Unadvanced host-owned code
therefore need not fund its unused potential operand depth. Imported compilation
and retained guest data keep their existing charges; no deferred debt or collector
tier is involved. Growth failures retain the allocating instruction's source span.
Ordinary bodies no longer retain clauses through a creator overlay or mirror the
caller just to evaluate a lowered body. Required dynamic/lowered routes remain.
Lowered bodies and dynamic namespace consumers retain their existing dictionary
ownership. Lambda metadata now uses ordinary function storage, so small allowances
can reject at different thresholds; pending users and escaping aliases stay funded.
Deleting function metadata removes the reference without refunding table capacity
or escaped data. Reinsertions conservatively admit another slot. Reserved-field
resets that need a new metadata entry must admit it before publication.
Executable eager comprehensions reuse the existing binding lifetime in the caller
frame. Scope end restores lookup/capture state and does not refund collection or
element storage retained by a result. Failed/cancelled nested scopes unwind in
reverse order; unpublished partial storage can stay with the finite owner.
Compiled loop/comprehension targets use the same admitted unpack transfer and
starred-rest policy as ordinary groups. Comprehension stores keep their existing
lexical namespace owner; they introduce no additional scope, allowance or cache.
Native enumeration working storage is disposed on loop/comprehension failure or
early exit. This releases its temporary allowance without closing retained Python
iterator/generator values or refunding their surviving storage.
Async executable frames await removed native cursors before guest continuation or
terminal publication, including cancellation and admission failure. Existing stack
storage links pending disposals without new reservations or ownership scopes;
the execution remains funded while disposal is outstanding. Yielded generators
keep their active cursors alive. Cleanup failure still drains other abandoned
native cursors in inner-to-outer order.

Fixed-arity unpacking admits capacity for at most one excess value beyond its
target count, using the existing temporary lease and capacity-growth preflights.
The input's full length does not determine that scratch storage. Starred unpacking
and general materialization keep their complete-drain behavior. A next callback
can run before subsequent buffer growth is refused; target stores wait for arity
validation. Lease cleanup releases scratch capacity while values retained by
guest containers, iterators or public results keep their owners and funding.

Executable unpacking admits the starred remainder and operand-frame capacity
while the leased input buffer is live, then releases that buffer after transfer.
Values remain funded across child stores and suspension. This can refuse a rest
list before prefix effects, or allow setter work after a larger temporary buffer
has ended. Committed batch-owned allocations can still cause a later admission
to fail; they become reusable when that owner ends. No guest-reachability refund
or identical tiny-budget threshold is promised.

Executable member/subscript stores retain RHS/receiver and optional key under the existing
execution owner until the awaited mutation ends, then clear them even on failure.
They introduce no per-store read dictionary, scratch owner or reservation fee.
Stored aliases retain their existing funding; clearing operands does not refund
data still retained by containers or results.
Member augmented stores use the same two operands after the in-place operation;
the receiver also remains live through getter/RHS suspension. Their executable
path introduces no prepared-read dictionary, temporary owner or 1,024-byte working
fee. Setter failure preserves any earlier in-place mutation, as Python requires.
Instruction fuel and narrow admission thresholds can differ from lowered execution.
Ordinary slice stores follow the same policy with five existing operand slots:
RHS, receiver, start, stop and step. Native list replacement keeps its existing
temporary-capacity and destination-overlap preflights; user/mapping slice keys
keep their shell admission. No per-store owner or read dictionary is added.
Additional header instructions can change narrow memory/fuel failure thresholds.
Executable deletion similarly retains receiver, raw key and optional slice bounds
in the existing operand frame until the awaited deletion ends, then clears them
in finally. Name deletion uses existing frame-aware storage directly. It removes
the read-array/dictionary capture and its fixed working reservation; no new
storage, scope or ownership fee replaces them. Native deletion/key-construction
preflights remain, and deleting a binding does not refund retained aliases.
Instruction fuel, target spans and narrow admission thresholds can differ from
the lowered path; no arbitrary-reference-withdrawal reclamation is promised.
Native list-slice replacement uses the existing owned iteration lease through
destination mutation, including suspended iterator callbacks. It no longer keeps
an extra final array or duplicates the slice-specific capacity-growth accounting.
The lease and destination replacement remain funded together. Shared capacity
charges can admit previously denied narrow budgets; shared collection-count checks
can reject earlier. Neither change refunds retained aliases or depends on CLR GC.

Host command requests carry the active budget in
`LythonSubprocessRequest.MemoryBudget`. A cooperating dispatcher borrows it,
creates any working scopes it needs, forwards it to nested Lython executions,
and awaits command users and cleanup. Output buffers that survive the callback
need an owner that also survives it, such as the borrowed parent. Cancellation
is a request to stop; allowance stays charged until outstanding work has ended.
Request payloads are borrowed until callback completion. A host retaining them
must create owned copies under a surviving owner, rather than retaining the
borrowed guest buffers after the invocation ends.

Scopes start empty, share one root limit and introduce no independent cap.
`TryReserve` and `TryResize` refuse immediately without changing accounting;
zero is valid and negative estimates throw. Failed resize preserves the previous
charge. Disposing a reservation refunds it; disposing a scope refunds its
subtree and invalidates it. Disposal is idempotent and nonthrowing. Admission and
resize through disposed owners throw `ObjectDisposedException`.

Major backing capacity, copies and replacement overlap are conservatively
charged. Internal execution/projection limits remain separate policy limits.
Exact immutable-byte sums with builtin integer starts keep a machine subtotal
and preflight one final exact-integer result, using existing temporary ownership
for major magnitudes. This avoids per-byte numeric storage while preserving
input/result overlap, empty-start aliases and native-operation ownership. The
conservative final preflight can refuse earlier at narrow allowances; identical
tiny-budget thresholds are not a contract.
Function parameter-name maps/layout arrays and live default containers own
capacity charges. Definition-time default maps use temporary admission during
construction, returning it once the Python containers own their storage; failed
construction refunds only new, unpublished metadata. Clearing/replacing published
defaults does not refund storage that can still have aliases or an active lookup.
Parameter-heavy definition batches can therefore refuse earlier than older
revisions that omitted layout capacity. Use bounded execution batches; immutable
layout sharing remains a performance opportunity, not an accounting exemption.
The fixed `len`, `repr`, `ascii`, `format`, `abs`, `pow`, `round`, `divmod`, `bin`,
`oct`, `hex`, `chr` and `ord` builtin definitions are shared trusted runtime
metadata covered by host/control headroom. They retain no execution context,
guest storage or host; their module is resolved through the current caller.
Builtin dictionaries, mutable type graphs and owned results remain per execution.
Default JSON load options likewise share only null callbacks and native True,
without retaining guest or execution state. Callback-bearing options and other
strict values remain separate; writable decoder attributes stay per instance.
Approximation deliberately leaves roughly 20% of minor objects, metadata and
short-lived temporaries uninstrumented; this is a design allowance, not a
measured coverage percentage. Keep additional host headroom for CLR/runtime
memory, native library buffers and delayed physical collection. Estimate counters
are neither live CLR heap size nor process RSS.

Guest values can share storage and escape through closures or containers.
Eligible compiled functions use captured cells under both `Run` and `RunAsync`;
selecting the asynchronous API does not by itself require retaining a mirrored
whole local namespace. Supported lowered fallbacks can still require mirroring.
Ordinary module variable annotations execute without a whole-statement fallback.
Scalar-only declarations need no namespace mirror. When annotated modules can
create functions, lambdas or deferred generators, Lython conservatively keeps the
module namespace so metadata can outlive its executable slots. Lambdas defined
directly in a module also retain its globals through the existing namespace
mechanism, once per execution; their bodies remain compiled. Comprehension
cells keep their existing ownership without forcing a caller namespace mirror.
Closure admission includes unmirrored
live slot capacity before namespace materialization, in addition to existing
entries. This can overcount overlap and reject earlier under narrow allowances.
Those estimates remain with the finite execution/result owner; no identity
inventory, tracing or immediate refund after withdrawing a reference is added.
Numeric container slots can each retain a conservative payload estimate even
when they refer to the same integer. Python identity is preserved; identity-based
charge deduplication is not promised. Alias-heavy collections can therefore be
refused earlier than their physical CLR size would suggest.
Lython therefore retains their estimates conservatively within a finite
invocation instead of tracing reference graphs. For long CSV/text/JSON/file
work, use bounded host-driven batches with persistent results under longer-lived
owners. Repeated batches return temporary accounting to baseline immediately.
One uninterrupted script is not promised a row-count-independent allowance;
arbitrary reference withdrawal does not promise immediate accounting recovery.

Refused admission fails the command. Compilation refusal produces `LA0008`;
execution refusal produces an unsuccessful result with a nonzero exit code and
the first admission's structured attribution. Projection refusal also prevents
a successful outcome, including `SystemExit(0)`. An already failed command keeps
its original nonzero failure when diagnostic projection cannot fit.

Assertion arguments reuse the existing governed tuple and transient exception
shell. Raising does not allocate a rendered copy of the message or call its
protocols. Retained arguments continue to use the execution/result owner.
Unhandled-message rendering runs before final output capture, under the existing
execution allowance and best-effort diagnostic policy; an admission refusal there
is terminal and preserves its first structured origin. This adds no budget,
scope, object inventory or separate rendering owner.
Existing failure-writer cleanup runs after diagnostic rendering too; RunAsync
awaits outstanding publication before returning the result or refunding its
execution scope. Cancellation continues to skip publication.

Generic exception constructors keep their existing owned argument tuple and shell,
using the existing authoritative args slot for lazy rendering. They no longer
allocate a formatted copy or invoke argument protocols during construction;
literal string text may remain as a best-effort CLR diagnostic fallback. Argument
packaging/admission and specialized native factories are unchanged. `.message`,
explicit rendering and unhandled diagnostics use the existing execution owner;
admission refusal remains terminal and diagnostic writers drain before refund.
No new rendering owner, budget, object layout or reference-withdrawal reclamation
is introduced. Replacing args can conservatively retain original construction
storage for the finite owner lifetime.

Immediate positional native-string `startswith` calls with optional bounds borrow
the existing operand memory through descriptor/index callbacks and host I/O.
The interpreter keeps the receiver and arguments until completion or awaited
cleanup, then uses its ordinary transfer/unwind. Exposed methods retain their
existing ownership; no extra reservation, argument array or owner is introduced.

Native string/bytes `float` calls with one positional argument reuse operand
storage and the existing parser's decoding, underscore-copy and error-preview
admission. Working reservations still end after conversion or error cleanup;
retained input aliases keep their existing owners. Removing argument packaging
adds no owner or cache and does not change the general protocol route.

Immediate three-positional-argument native string replacement forwards its raw
count from existing operand storage, sharing the two-argument operation and
replacement kernel. It removes immediate method/argument packaging for that form;
the receiver and arguments stay under their existing execution owner. Native
output-size/overlap preflights and result ownership remain authoritative. General
keyword, exposed-method and guest dispatch keep their existing funding; no new
cache, reservation class, budget or owner is introduced.

An execution's first refusal is terminal even if guest code catches `MemoryError`,
suppresses it in `__exit__`, or returns/raises from `finally`. Further guest work
and host calls stop. Guest cleanup is not an emergency allocation budget and can
be interrupted; rely on Lython's native ownership cleanup for contained resources.
Explicit guest `raise MemoryError(...)` remains an ordinary catchable Python error.
Cancellation and actual CLR memory exhaustion retain their separate meanings.

Admission precedes the tracked allocation or mutation; failed private construction
refunds unused admission. Native cleanup awaits outstanding users and relinquishes
its own storage without publishing buffered writes after admission refusal. Earlier
output, completed iterator effects and file changes remain; refusal grants neither
transaction rollback nor permission to replay a command. Lython does not recover
by skipping optional cache storage, evicting after refusal, switching to streaming
or disk, waiting, or retrying a smaller allocation. Normal bounded cache replacement
and host-selected streaming before any refusal remain supported.

Cleanup returns relinquished allowance independently of CLR collection. Retained
results and caches remain charged. The borrowed host root is still usable by later,
separate commands; a refused command cannot resume merely because pressure ends.
