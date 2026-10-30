# Embedding and host contract

Lython runs a Python subset in fully managed C# on .NET 11 and .NET 10. It exposes no guest
CLR reflection, native loading or ambient filesystem, network or process access.
The application, host implementations and selected regex provider are trusted.
Containment is cooperative; process isolation remains an application concern.

## Execution API

[`LythonEngine`](../src/Lokad.Lython/Public/LythonEngine.cs) compiles source with
`Compile(source, memoryBudget, cancellationToken)`. Shorter overloads omit the
budget or token. Compilation returns a disposable
[`LythonCompiledScript`](../src/Lokad.Lython/Public/LythonCompiledScript.cs) with
`IsValid` and `Diagnostics`, and performs no host I/O. `Run` and `RunAsync` execute
against `ILythonHost`; engine convenience methods combine compilation/execution.
Reuse compiled programs for repeated inputs when useful.

Hosts can throw [`LythonHostException`](../src/Lokad.Lython/Host/LythonHostException.cs)
with a contained diagnostic safe for guest code. Its failure kinds map missing,
permission, wrong-kind, existing, unsupported, broken-pipe and transport outcomes
to the corresponding Python errors. `BrokenPipeError` inherits `OSError`.
Unknown CLR exceptions keep generic redacted diagnostics; their private messages
are not exposed. Direct subprocess launch failures use these errors, while a
returned process result carries its actual exit status, including nonzero shell exits.

After refused child admission, finish native users/cleanup and throw the same
typed exception with `MemoryAdmission` and the first requested-byte estimate.
Lython records that terminal origin at its host boundary; handlers, suppression,
finally returns and later host failures cannot replace it. No external sticky
error is needed. Cancellation uses `OperationCanceledException` separately.

Run requires host capabilities advertising synchronous completion. If such a host
returns pending work anyway, Lython waits for that operation to settle before
reporting the contract failure and releasing its borrowed storage. Text/binary
file cleanup closes the failed handle without retrying that write. A host that
never settles or honors cancellation can delay failure cleanup; hosts remain
responsible for their capability promises.

Text and binary `close()` retire the handle and relinquish staged buffers even
when flushing reports an error. A later close is inert and does not retry that
publication. An explicit `flush()` failure before host effects can leave the
writer open for a deliberate retry; refused memory admission remains terminal.

Text and binary `open()` use `ILythonHost.OpenFileAsync` and own their `ILythonByteFile`
through close or execution cleanup. Hosts receive the original path and explicit
contained Cwd in `LythonPathRequest`; they resolve dot segments and trailing slashes
and enforce containment. Reads fill admitted caller buffers directly. The opened
identity survives path rename/replacement, and short reads continue until zero.
Guest update/seek modes remain unsupported. Python text decoding, BOMs, newline
translation and supported positioning remain in Lython. Unchanged UTF-8 output
borrows admitted writer storage; other encodings fund their replacement overlap
through actual publication. Path helpers, local imports and archive input use the
same owned-file capability. Whole-file reads grow admitted storage in bounded
windows and fund final-copy overlap before publishing the value. Text helpers use
the shared whole-buffer decoder, retaining input funding until the result is charged.
A funded one-byte EOF probe avoids growing a full buffer solely to discover EOF.
Size hints guide windows; they do not authorize allocation. Earlier conservative
denial is possible.

Byte-file writes report an accepted prefix. A known zero-byte failure permits an
explicit retry; partial or unknown acceptance closes the handle to prevent replay.
Once bytes are accepted, a later backend flush failure never resends them. The
buffer borrow lasts through actual completion, including falsely synchronous work.
Cleanup awaits uncancelled file disposal before refunding private storage. Hosts
fund acquisition scratch through the borrowed allowance and keep persistent host
storage under a surviving host owner. Missing owned-file capability fails explicitly;
there is no legacy whole-file or path-reopening transport fallback.

If that completed operation reports memory refusal, its terminal origin takes
precedence over the synchronous-contract diagnostic. Settling outstanding users
still precedes returning/refunding their storage.

`RunAsync` awaits host effects and supported guest callbacks. It does not enable
Python `async`/`await`. Both APIs use the executable engine and preserve evaluation
order, closures, generators and exception cleanup.

[`LythonRunOptions`](../src/Lokad.Lython/Public/LythonRunOptions.cs) supplies:

| Option | Meaning |
| --- | --- |
| `Globals` | Initial guest bindings, normalized into admitted storage |
| `Args` | Complete `sys.argv`, including any program name; omission gives `[]` |
| `Environment` | Explicit environment visible to the guest |
| `SourcePath` | Diagnostic origin and local-import context |
| `CancellationToken` | Current execution-time authority |
| `AllowedBuiltinModules` | Null allows the supported inventory; empty denies builtin imports |
| `AllowedLocalModules`, `DisableLocalModuleImports` | Local-import authorization |
| `MaxRecursionDepth` (`int?`) | Python call-depth override within managed stack protection; null uses 1,000, zero rejects guest calls, negative values are invalid |
| `MemoryBudget`, `ResultMemoryBudget` | Borrowed execution and surviving-result parents |

Builtin grants use case-sensitive canonical names and include registered dotted
ancestors. They do not supply host capabilities. Allowed local imports resolve
relative to the importing source path, cache within the execution, and use the
same regex provider, token and memory owners.

`sys.modules` is the execution's live import registry, containing loading and loaded modules
rather than the builtin inventory. Aliases observe subsequent imports; deletion,
cached values and `None` blockers affect later imports. Builtin grants are checked
before consulting cached values. Repeated registry reads reuse the same storage.
Existing native values retain their defining type independently of registry edits.
Local modules publish their funded identity before the body runs, so self imports
and cycles can observe initialized attributes. Failed imports remove their registry
entry; successful imports honor a replacement made by the body. Escaped aliases
and code remain funded for the execution even when an import fails.

Authorized initialized packages take precedence over same-name `.py` files.
Local and builtin modules expose `__package__`, `__spec__` and the contained `__loader__`;
packages also expose `__path__`, sharing the spec's search-location list.
`importlib.util.find_spec` reads the live registry and current `__spec__`, including
cached replacements and `None`. Cold dotted discovery initializes authorized
parents and reads their live paths without executing the target child. Builtin
metadata belongs to the invocation; guest edits never enter shared definitions.
Spec `parent` is derived and readonly; `cached`/`_cached` and
`has_location`/`_set_fileattr` share their backing values. Public location assignment
uses Python truth. Metadata edits do not authorize a different loader.
Loaded package `__path__` controls absolute child lookup: lists/tuples of strings
are supported, including edits to their shared search-location list. Every candidate
still requires a host-local grant; an empty path admits no new children. Other
path iterables fail explicitly. Spec/loader fields describe the contained loader;
changing them does not authorize arbitrary execution. Relative-import syntax, namespace packages
and arbitrary loader execution remain unsupported.

Host capabilities are checked when the actual operation or restricted import is
reached. Unused branches, short-circuited expressions and shadowed names require
no speculative host grant. A reached missing capability returns an attributed
runtime failure; preceding guest effects can already have occurred.

## Execution internals

Compilation tokenizes/parses source, validates the subset, computes scope facts,
lowers constructs, and builds executable instructions. Lexical scope preparation
follows parsing/name rewriting; analysis, lowering and class creation reuse those
immutable function/class/lambda facts. They belong to the compiled program,
without a cross-script cache. Exception header expressions
execute through those instructions; bare handlers need no second type-name resolver.
The executable engine runs both public APIs and local imports, using local/closure
slots, operand storage and resumable generator frames. Lowered evaluation remains
for actual class-body, annotation and expression/opcode consumers; there is no
whole-program alternate backend. Imports retain canonical module namespaces.

Ordinary positional calls to executable functions with slot-local storage advance
an explicit caller/callee chain in the same interpreter. Each invocation owns its
bound local array until its body and awaited cleanup finish; suspended callers
retain their operands. Return values and failures resume the existing instruction
and exception routing. Simple-return shortcuts remain active. General binding and
native/protocol callbacks retain their existing invocation routes and managed stack
checks; this change does not replace the large-stack workers or recursion bounds.

Reusable workers provide the managed stack for the synchronous prefix. Actual
asynchronous suspension releases that worker instead of parking it through I/O.
Per-run services retain host capabilities, caches, stream capture, exception state,
cancellation and memory admission. The core's only runtime package dependency is
Lokad.Parsing; the optional regex adapter owns its backend dependency.

Implicit numeric methods bind from the type, bypassing instance shadows and
ordinary `__getattribute__`. Binary operators and `divmod` share reflected-method
precedence; RunAsync awaits supported descriptor and method callbacks. Sequence
repetition uses the same type-index conversion for expressions, in-place updates,
`operator.mul` and direct native methods, preserving callback order and aliases.
Custom key hashing still requires synchronous completion, including under RunAsync.

`str.format` and `format_map` scan field structure and nested specifications as
borrowed UTF-8. Names, suffixes and the final specification decode only at the
existing resolver/formatter boundary. Temporary metadata and nested working
capacity borrow the active execution allowance through suspended callbacks;
compiled templates retain their separate owner. Private expanded specifications
refund after use, and final strings remain funded. Structural scans and resumed
callbacks observe the current cancellation token.

Exact integer math uses cooperative cancellation and shared memory admission,
without an iteration quota. Cancellation is checked between arithmetic operations;
it cannot interrupt an individual CLR `BigInteger` operation. Integer true division
rounds large ratios from integer quotient/remainder, including subnormal results;
temporary alignment/division storage is admitted before allocation and refunded
before continuation. Conservative workspace estimates can cause earlier denial.
Constant integer power/shift results are resolved before magnitude admission or
count narrowing. Growing results retain allocation preflights and checked native
count/storage bounds. Floating power overflow raises OverflowError; nonfinite
inputs retain their Python behavior.
Dictionary updates
drain unknown-length pairs without storing their contents, preserving iterator
effects and exact length errors; infinite pairs require cancellation. XML parsing
retains its 1,024-level depth protection.

Spreadsheet cells own their values and metadata independently of their current
worksheet address. Native cell/sheet capacity and style registration use shared
admission; clearing values or removing entries does not refund retained capacity.
Detached aliases stay funded conservatively until the execution ends. Worksheet
iterators admit one result row/column at a time, and structural edits admit
replacement storage before changing cells. XLSX loading still materializes its
native model; use bounded host-driven executions for long processing workflows.
Save output remains funded through asynchronous host publication and cleanup.

`math.nextafter` computes the requested adjacent floating-point value directly,
including very large step counts, without iterating once per step.

## Results and lifetimes

[`LythonExecutionResult`](../src/Lokad.Lython/Public/LythonExecutionResult.cs) is
disposable. `State` distinguishes `SucceededState`, `CompilationFailedState` and
`RuntimeFailedState`. `Success`, `ReturnValue`, `Diagnostics`, `Failure`,
`FailureAttribution` and `ExitCode` are convenience accessors. Failures carry
Python-shaped exceptions and available source/stack information; `SystemExit`
preserves its exit-code semantics.

Exceptions compare and hash by identity, including cyclic cause/context chains.
A guest exception has one current args tuple; replacement preserves its aliases.
Its mutable chaining slots survive catches/finally, and `SystemExit.code` is
independent of later args edits. Assigning an exception dictionary replaces its
reference and preserves aliases to both old and new dictionaries.

Subprocess exceptions keep their original positional args independently of their
mutable field dictionary. `stdout` aliases `output`; str reads the live fields,
while repr uses args. Construction stores metadata without rendering it. Native
messages and supported metadata callbacks share governed, awaited rendering.

Exception construction retains arguments without rendering or duplicating string
messages. Requested str/repr, args iteration and final diagnostics support awaited
guest callbacks under `RunAsync`. General nested container rendering may still
require synchronous callbacks. Diagnostic UTF-8 and CLR conversion overlap is
admitted before conversion; final projection funds the surviving message before
the execution owner ends, including conservative overlap estimates. Native cleanup
can carry an unmaterialized exception even after admission refusal.

Top-level `return` projects CLR-friendly values, including `byte[]` for bytes.
Projection detects cycles and admits substantial storage. Stdout/stderr are
retained in `StandardOutput`/`StandardError`; `Consumption` reports accepted
UTF-8 capture sizes. Memory properties report the accounting estimates described below.

Successful projection checks the active cancellation token during managed
container traversal and before publishing the result. Atomic CLR conversions
and copies are not preemptible. Cancellation diagnostics and native cleanup
remain available; partial projection stays conservatively charged until result
disposal and is included in projection accounting.

Initial globals normalize CLR Single to the existing double-backed guest float
and CLR decimal to the contained Decimal value. Returning a supplied Single
therefore produces Double; integral CLR widths and decimal preserve their public
round-trip types. Nested input containers retain shared aliases within an execution.
DecimalTuple values project through the admitted sequence boundary as CLR object
arrays, including a nested array for digits.

Known CLR representation limits return an attributed `ProjectionRejected` failure:
`None` dictionary keys, distinct dictionary keys or set members that collide under
CLR equality (such as separate NaN values), timedeltas outside `TimeSpan`, and aware values whose offset
or UTC instant cannot fit `DateTimeOffset`. Guest temporal ranges are unchanged.
Aware times use January 1 of year 1 where representable; a positive offset that
would underflow that anchor uses January 2 instead.

`statistics.LinearRegression` uses the ordinary namedtuple contract, including
arbitrary supported fields, tuple protocols and canonical field metadata. Its
mutable type metadata is isolated by execution. Namedtuple results project through
the admitted sequence boundary as CLR arrays.

Consume values and relinquish aliases before disposing a result. Await all runs
and relinquish compiled diagnostic aliases before disposing a script. Disposal
does not cancel work. Borrowed budgets and providers must outlive their users.

Copying uses one Python memo keyed by stable semantic ids. Hook insertions,
replacements and deletions are visible to subsequent lookups. Private memo backing
is relinquished after copying; a memo exposed to a hook or supplied by the guest
keeps its owner and charge. Shallow and atomic copies need no deep-graph memo.
Shallow `__copy__` resolves on the type; `__deepcopy__` uses ordinary instance lookup.
Missing or `None` hooks fall back to supported native/state copying. Dict and Counter
copy values before keys; defaultdict retains its reconstruction order. Reduction
protocols and `copy.dispatch_table` are unsupported and fail explicitly.

Mutation or inspection of a list during `list.sort` is undefined, as in Python.
Normal key evaluation, stable ordering and `sorted()` input materialization remain
part of the supported behavior.

## Shared memory ownership

Substantial storage uses a borrowed
[`ILythonMemoryBudget`](../src/Lokad.Lython/Public/ILythonMemoryBudget.cs).
Executions, scratch, caches, compiled programs and results share its allowance.
[`LythonMemoryBudget`](../src/Lokad.Lython/Public/LythonMemoryBudget.cs) is the
standalone implementation; a null limit is unbounded. No supplied budget means
no hidden allowance. Runs inherit the compilation-supplied parent unless
`MemoryBudget` overrides it. Choose a longer-lived `ResultMemoryBudget` before
projection when results must outlive the invocation parent.

### Admission

`CreateScope()` reserves nothing and adds no cap. `TryReserve` admits bytes
immediately; `TryResize` adjusts a reservation. Refusal preserves the old balance.
Admit capacity, metadata, major working storage and old/new replacement overlap
before allocation or observable mutation. Failed construction returns unused
admission. Independent execution/projection/string/collection/read/output caps
do not exist; checked representation constraints and bounded chunks still apply.

Dynamic `**kwargs` names admit their CLR representation before decoding. Native
callees can retain these labels, so their allowance conservatively remains with
the execution after argument scratch is released.

Parser options borrow admitted immutable text. Namespace dictionaries and help
output use the ordinary governed storage/builders; output capture is not an
allocation preflight. FileType's decoded paths remain conservatively execution-owned,
and pending file opening/output work retains its funding until the awaited users
and cleanup finish.

Derived byte buffers, byte slices/representations and text transformations use
the active execution owner, including operations on compiled literals. Construction
admits builder capacity and final-copy overlap. Byte joining keeps its admitted
input storage until the result is assembled; RunAsync awaits input iteration.
Byte and string tab expansion check cancellation between bounded output chunks;
casing, replacement, translation and hexadecimal rendering also check while scanning.
Byte searches use managed bulk operations over bounded candidate ranges; owned
predicate and splitting loops also check cancellation while scanning. Individual
BCL calls remain atomic, so cancellation provides no hard deadline.
Admission failures identify the allocating operation rather than the receiver's
creation span.

Integer-to-bytes conversion admits its final backing and writes it directly;
sign padding checks cancellation between bounded chunks. Decimal text/tuple parsing
admits conversion scratch before allocation, and digit iteration admits capacity
as it grows. RunAsync awaits their supported guest conversion and iteration hooks.
Byte buffers, decoded text, struct and URL results transfer admitted backing into
the final owner without a second charge or an unfunded refund/readmission interval.
UTF-8 URL quoting borrows immutable input. Public byte projection admits its real
copy through the result owner, preserving source/copy overlap without charging
that copy to the execution owner as well.
`str.encode` and `bytes(text, encoding)` transfer the encoding reservation directly
to the resulting byte buffer. Native codec errors retain an admitted source
snapshot or the original string, plus their argument/field storage. Insufficient
allowance can refuse that error storage before a Unicode exception is published;
the first admission refusal remains terminal through guest handlers and finally.

Random byte and integer generation admits capacity before drawing; generation,
seed folding, shuffle and rejection loops poll cancellation, including distributions
whose non-finite input can otherwise keep rejecting indefinitely. Wide randrange
arithmetic holds conservative temporary magnitude storage and transfers funding
for the result; limited allowance can reject it before drawing. Error text is
rendered only for an actual error, through governed Python conversion. Actual
managed buffer and Python length representation bounds apply, without an independent
bit-width quota. Native virtual populations stay indexed for choice/choices and
small samples; counted samples index cumulative bounds without expanding the
multiset. Custom guest populations are explicitly unsupported. RunAsync awaits
supported random count/index/multiplication hooks, including randrange.

Guest context managers capture type-level __enter__/__exit__ slots in Python
order, binding descriptors asynchronously when needed. Instance shadows and
__getattribute__ do not change those special slots. Only exceptional exit tests
the result's Python truth protocol; ordinary exits ignore it. Lowered class bodies
and executable code preserve the active exception during cleanup and restore the
outer exception afterward. Native managers use the same async manager interface.

SequenceMatcher borrows native indexed inputs; its supported general-iterable
extension materializes governed storage. Same-object setters preserve caches.
Input mutations do not automatically invalidate previously computed results.
The exposed index dictionaries/sets and cached block/opcode lists are the state
used by matching. Extracted aliases remain funded after replacement; published
storage can stay owned until the execution batch ends. Grouping adjusts cached
opcode boundaries as in Python, and its yielded lists retain their own storage.
Failed reindexing keeps the previous sequence/index; CPython can expose partially
replaced state after that error.

RunAsync awaits equality/truth callbacks in find_longest_match, ratio,
get_matching_blocks and get_opcodes, as well as predicate/option truth during
construction and reindexing. Other difflib producer and custom sequence/key
protocols can require synchronous completion. Lazy producers defer source work
until demand; they do not promise constant memory throughout an unbounded run.

JSON indentation has no fixed width ceiling. Encoding prepares admitted indentation
storage, which can remain charged for the execution; constructors retain the option
without materializing unused padding. Checked string-representation bounds apply.

JSON decoders evaluate a guest `strict` option once when preparing the scanner,
including asynchronous callbacks. The public attribute retains the supplied
object; replacing it later does not alter the scanner's captured setting.
Valid JSON string tokens unescape directly into UTF-8. Larger escaped tokens use
admitted managed scratch rather than a CLR string; the result owns an independent
copy, including across callbacks and source-buffer reuse.

Any refused admission fails the command, including cache admission. Guest code
cannot catch, suppress or return over it. There is no retry, waiting, algorithm
fallback or optional-storage recovery. Guest-raised `MemoryError` stays catchable.
Compilation refusal gives diagnostic `LA0008`. Native cleanup/refunds remain
available, and later commands can reuse returned allowance.

### Ownership lifetimes

| Owner | When storage can be released |
| --- | --- |
| Compiled program | On disposal after all runs/diagnostic aliases end; includes code, metadata and retained compilation caches |
| Execution/batch | After execution, pending host/protocol users and cleanup end |
| Scratch | When construction/operation relinquishes it, including failure/cancellation |
| Retained result | On disposal after consumers relinquish projected values/aliases |
| Cache or pool | At its owner's end or ordinary funded eviction, after users finish |

Statistical operations retain numeric/frequency scratch through the consuming
reduction and result construction. Capacity is admitted before allocation,
including converted buffers and overlapping replacement arrays.

`Counter.elements()` is a lazy, single-pass iterator. It retains the mapping,
reads each count when reached and awaits supported count index hooks in RunAsync.
Repeated keys are borrowed rather than materialized into an intermediate list.

Combinatoric iterators retain private governed input pools. Capacity, numeric
headroom and overlapping replacements are admitted while consuming input; there
is no intermediate ungoverned pool followed by a final copy. Product with zero
repetitions does not acquire its inputs. Repeated-table overflow reports a
catchable Python error before input effects; actual managed array bounds apply.
Result tuples admit their backing and scalar headroom before filling. Zip-longest
keeps that reservation through suspended input reads and funds its retained cursor
tables before construction. Cancellation is checked inside owned result/index loops.

Tee admits queue graphs and fanout capacity before source advancement. Retained
backing stays charged after dequeue; pending fanout stays funded across input
suspension. Reentrant source advancement raises RuntimeError, while buffered peers
can still consume their cached values.
Tee's size hook can suspend; its source acquisition still requires synchronous
completion.

Map admits retained input cursors and per-call scratch, acquiring each input
once. Chain retains funded source references and opens one input at a time.
Supported lazy adapters await actual source acquisition and truth/equality/addition
callbacks under RunAsync. They reuse ordinary Python operation rules; unused
callbacks are checked only when called. Islice advances skipped input on demand,
and dropwhile stops calling its predicate after the first false result. Terminal
states follow each adapter's Python behavior rather than a universal cursor latch.
These adapters retain the module context, actual sources and callback closures;
they do not pin an unrelated function's creation frame. Coarse retained graph
charges can remain owned by the execution batch after an iterator is exhausted.

Iterator size/index and batched strict-truth hooks await supported callbacks in
RunAsync. Constructor-specific effect order follows Python: combinations converts
its size before pooling; permutations accepts native integers after pooling.
Batched accepts signed 64-bit requested sizes while growing only with consumed
input, subject to actual managed result-array capacity.

`functools.total_ordering` preserves inherited roots and methods, raw comparison
results, `NotImplemented` and Python's short-circuit order. Class descriptor,
root, truth and equality callbacks can suspend in RunAsync. A class without any
ordering root raises ValueError; an explicit equality method is not required.
`cmp_to_key` compares a comparator's raw result with zero using the requested
operator. Floats and guest comparison results are accepted; sorting and membership
truth-test those results and await supported comparison/truth callbacks in RunAsync.

Partial arguments and keywords have stable identities; keyword and wrapper
`__dict__` edits are live. Partial's `func`, `args` and `keywords` attributes are
read-only. Replacing metadata does not refund dictionaries retained by aliases.
State remains charged to its existing owner until that ownership scope ends.
Calls reserve argument/keyword working storage before construction and retain it
through suspended targets. Partial rendering uses governed UTF-8 growth and the
existing recursion guard. These estimates can reject earlier than older releases.
Metadata lookup keys exposed to guest key protocols are conservatively retained
by the execution owner; ordinary string-only lookup scratch is released directly.

Single dispatch keeps one governed registration dictionary and a stable live,
read-only registry view, including the default object entry. Classes use actual
identity and their supported hierarchy; value dispatch observes Python's
`__class__` lookup. Dispatch methods and partialmethod use ordinary descriptor
binding for instance and class access, including guest, static and class methods.
Their targets and supported descriptor/class callbacks await under RunAsync.

Native sys.version_info and functools CacheInfo types keep mutable metadata in
their execution owner. Repeated reads share metadata within that execution;
independent executions cannot alter each other's maps. Retained results keep
their owning allowance charged until cleanup.

Typed caches distinguish canonical guest types, including distinct namedtuple
types represented by one CLR implementation. The typed option evaluates Python
truth when the function is wrapped; its original value remains visible through
cache_parameters. Key working capacity is admitted before construction.
Cached-property func, attrname and __dict__ share canonical governed state.
Class creation initializes its name; a later assignment requires an explicit
__set_name__ call. Name initialization accepts string names. Computed values live
in the instance's dictionary and retain its owner; changing func affects uncached
instances. Normal descriptor precedence and original getter errors are preserved.

Wrapper metadata updates consume names on demand, use ordinary attributes and
the actual update method, and set __wrapped__ last. Supported getter, setter,
iterator and update callbacks can suspend. Decoded metadata labels are
conservatively retained by the execution owner because hooks/stores can keep them.
Recursive-repr guards survive awaited targets and unwind on every exit; retained
guard capacity stays charged. Fill values follow Python's deferred return rules.
These operations preserve cooperative cancellation and terminal admission refusal.

Environment mutations admit native string and table growth before publication.
Initial host strings remain borrowed; guest replacements and retained request aliases
stay execution-owned, with conservative capacity/value retention until owner cleanup.
Subprocess requests retain admitted decoded argv/environment storage through host
completion and awaited cleanup. Binary results own admitted byte storage; text
results use the governed decoder. Hosts return one fresh disposable
[`LythonSubprocessResult`](../src/Lokad.Lython/Host/LythonSubprocessResult.cs),
transferring a scope funded before output allocation; its bytes are borrowed until
disposal. `CompleteBufferedAsync` admits capture capacity before inherited writes
and copies captured bytes into an independent owner. Acquisition buffers remain
host-funded through actual users. Direct run/check consumers dispose completion
storage after copying results; Popen retains it until execution resource cleanup.
Escaping guest/public copies keep their own funding. Pipelines forward the retained
raw bytes rather than re-encoding decoded output. The host honors requested
operation timeouts; the active cancellation token controls execution. Async
preparation and completion use the same request path with awaited guest protocols.
The 0.11 host contract replaces the bare result record: custom runners must transfer
an owner or use the buffered helper. Record cloning/deconstruction is removed.
Combined host/runtime estimates appear in the shared root balance/peak; the result's
execution peak measures the runtime governor alone.

The host's supplied environment is isolated. Keys, values and items expose live
views over the same table; explicit lists/copies/projection fund their materialized
values. Invalid names and NUL-containing values reject before mutation.

Subprocess request decoding and native table capacity are admitted before allocation.
Request storage remains funded through host completion and awaited cleanup; buffered
Popen requests retain that storage for the execution owner. Python-facing args retain
the original guest value instead of a re-encoded copy. Command-line quoting writes
UTF-8 directly to admitted storage, including construction overlap and retained output.

Argument parsing admits metadata construction and major working storage before
allocation, retaining scratch through suspended callbacks. Supplied namespaces
share their real bindings; defaults and constants keep their existing owners.
Append result copies are admitted while old aliases remain funded. Native buffers
and scratch return allowance when their actual use ends; arbitrary callback-retained
values can remain conservatively charged until execution/result cleanup.

Instance and class namespace views share their actual attribute storage; instance
attribute deletion keeps reusable table capacity funded. Exposed key labels are
cached conservatively until the owner ends. Read-only class/native-module views
and their public projections use the same admitted storage and result owners as
other dictionaries. Function custom attributes use a canonical governed dictionary,
separate from intrinsic metadata such as `__name__` and argument defaults.

Deque repetition admits retained growth before mutation, reuses original nodes
and computes only the retained suffix for bounded deques. In-place extension
streams input in both APIs; self-extension holds a funded snapshot while used.
Deque slices raise TypeError, as in Python; integer indexing remains supported.

List search and comparison recheck live lengths after guest equality/truth
callbacks. Search can observe appended values; shrinking cannot expose vacated
storage slots. Deque removal validates mutation before removing an element and
raises its Python IndexError, independently of count/index behavior.
List/tuple/deque search bounds and list/deque insertion use implicit type-level
index hooks, including actual suspension under RunAsync. Both search bounds
convert before normalizing negative positions against the current length.

Slice metadata arithmetic preserves arbitrary integer lengths and strides.
Real container indices narrow only after normalization; an oversized stride can
still select, replace or delete one element. Slice equality uses contextual bound
comparison with identity shortcuts and awaited callbacks. Slice repr builds
admitted UTF-8 output; nested rendering retains its documented synchronous boundary.
Sort reverse flags use Python truth, with awaited hooks under RunAsync. Stable
key/comparison behavior and the existing sort engine remain in use.

`ChainMap.maps` is its live, replaceable mapping sequence. Its retained layer
capacity is admitted once; repeated attribute reads preserve identity. Key merges
use funded dictionary storage and existing key protocols, including stored hashes.
Counting and native mapping consumers release private scratch when finished;
guest iteration snapshots remain batch-owned while users can retain them.

Counter construction and updates copy initial mapping counts and use Python's
operand order for later arithmetic. RunAsync awaits those count operators,
including keyword updates. Other Counter arithmetic retains its existing native
real/Decimal subset; this does not promise every operation on arbitrary counts.

Counter ranking admits sort working storage and final list capacity before
allocation. `most_common` evaluates limit/comparison protocols in both APIs,
skips ranking for nonpositive limits and uses a direct maximum for one result.
General limits use stable sorting, so comparison counts/order can differ from
heap selection. Counter repr shares ranking and follows the existing synchronous
native rendering path, with an insertion-order fallback for ordering TypeError.

`NormalDist` checks sigma before converting mu and sigma to floats, using ordinary
Python comparison/conversion protocols; `RunAsync` awaits their callbacks. Samples
use the execution's random state unless an explicit seed selects an independent
state. Sample count conversion can also suspend. Model shells and sample/quantile
capacity are admitted before construction; failed or cancelled construction drops
unpublished backing before returning its allowance. Seeded values follow Lython's
random implementation, without a CPython sequence guarantee.

Variance, standard deviation and `NormalDist.from_samples` share exact integer/
binary moments at a common exact scale and round only at the result boundary. Standard deviation takes
the square root of the ratio directly, preserving representable results whose
variance exceeds binary64. Major arithmetic workspace is admitted before use.

`math.erf`/`erfc` use managed series and continued-fraction kernels; normal inverse
CDF uses AS241. These are binary64 approximations. CDF tails can round to zero or
one, and results need not match every platform's native math bit for bit. The
[independent accuracy controls](../tests/Lokad.Lython.PublicApi.Tests/StatisticsAccuracyTests.cs)
state the qualified ULP, relative-tail and absolute-probability tolerances.

Unweighted `statistics.fmean` streams its input with funded floating partials;
intermediate overflow stops before consuming the tail. Weighted calls prepare
weights before data and check their denominator after the paired pass. Correlation
scales its square-root product to avoid intermediate overflow/underflow.

Shell quoting and tokenization borrow existing UTF-8 input and admit output
capacity through governed builders. Lexer character sets retain their supplied
strings; source snapshots and pushed tokens remain charged while retained.

Lexer `posix`/`whitespace_split` and regression `proportional` retain supplied
objects and evaluate their truth protocols when used, including repeated checks
and suspending callbacks in `RunAsync`. Lexer construction/source reads remain
host mediated, with their working storage funded across suspension.

Paired statistics materialize accepted numeric iterables. Lython also accepts
one-shot inputs where CPython expects sized sequences; stateful custom iterables
can therefore have different replay behavior.

Function caches tolerate key equality that clears or reenters the same cache.
Evicted/cleared entries stay funded until active contextual lookups finish using
them. Key hashing/equality still require synchronous completion; cached function
bodies can suspend through `RunAsync`. Infrastructure estimates deliberately
include bucket, recency and replacement headroom and may refuse earlier.

`operator` truth, rich comparison, containment and sequence-search helpers use
the expression engine's contextual protocols. Rich comparisons preserve arbitrary
guest return values; those hooks and length hints can suspend through `RunAsync`.
Length-hint defaults are validated before lookup and may be negative fallbacks.

Scope disposal refunds and invalidates its subtree; it does not cancel work or
dispose runtime objects. Await users and serialize disposal with subtree operations.
Lython disposes its own scopes, never a borrowed root. Retained aliases, borrowed
buffers and suspended work stay funded. Refund is independent of CLR collection;
reference deletion or container clearing does not promise immediate recovery.
There is no guest-heap tracing, per-object ledger, weak reclamation or forced GC.

Hash originals and copies remain execution-owned through awaited host cleanup and
diagnostic projection, then their incremental state is disposed. Failed construction
and copying unwind their reservations/resources. Conservative batch charges can
remain until execution ownership ends; physical collection does not drive cleanup.

### Streaming and approximation

Shlex reads its exposed text stream on demand and shares that stream's cursor.
String-backed lexers borrow immutable UTF-8 input; a first write admits the full
writable backing before changing it. Truncating a borrowed stream can retain its
original input until that owner's lifetime ends. Token output remains admitted,
and stdin's partially consumed line is shared with ordinary stdin reads.

CSV writers render each row into admitted UTF-8 storage before invoking the sink
or publishing buffered history. Field conversion uses Python string protocols;
RunAsync awaits guest conversions and sink callbacks. Native escaping checks
cancellation. Buffered writers retain rendered rows and their capacity until the
execution owner ends; getvalue admits an independent result and omits the final
terminator. Decoded reader lines stay admitted through parser consumption; native
parsing checks cancellation in bounded chunks. Readers acquire one source iterator
at construction in the selected execution mode. Dictionary readers infer headers
on fieldnames/first-next access and retain that row, or the supplied fieldnames
alias; iterator fieldnames are materialized once. Dictionary writers share the
ordinary rendering path and admit writeheader storage before allocation. Header
mutations remain visible; no duplicate string-only header arrays are maintained.

Path common-prefix comparison scans immutable UTF-8 at whole-character boundaries.
Directory walking reads the canonical mutable dirnames list between children, so
pruning and list mutations remain visible without repeated decoded snapshots.

Long CSV/text/JSON/file workloads use bounded host-driven executions:

```csharp
var budget = new LythonMemoryBudget(16 * 1024 * 1024);
using var compiled = new LythonEngine().Compile(source, budget);
foreach (ILythonHost batchHost in batchHosts)
{
    using var result = await compiled.RunAsync(batchHost);
    Consume(result); // Relinquish values and aliases before this scope ends.
}
```

Scratch can end within an operation; retained guest data can stay funded for the
batch. Arbitrary uninterrupted scans are not promised row-count-independent
accounting. Completed batches do not accumulate allocation charges.

Byte construction, hexadecimal parsing, splitting and variable-size date/time
rendering admit working capacity before allocation. Timezone names retain their
governed string aliases; new repr output uses Python string escaping and governed
UTF-8 builders, including nested date/time representations. ISO parsing scans the
existing UTF-8 with cancellation during long fractions, without whole-input
decoding or regex captures. Native wrappers and bounded fixed-size formatting use
conservative small-value estimates. Temporary conversion buffers end with the
operation; retained results remain funded by their owners. `strptime` uses a
managed directive parser with cancellation during scans and alternative selection,
without a regex provider. Its conservative input/format working estimates can
reject earlier; scratch funding returns when parsing ends. Duplicate directives
raise `re.PatternError`, including when no regex provider is supplied.

Parsing keeps time fields separate from datetime construction: `time.strptime`
can retain seconds 60/61 and February 29 with default year 1900. The shared
strftime formatter accepts these time records; datetime constructors and parsing
still require a valid calendar date and second 0–59.
CPython's deprecation warning for parsing a day without a year is not emitted.

Date/time constructors, calendar/timestamp factories and replacement methods await
user index protocols under `RunAsync`, including mediated I/O. Fixed-offset time
comparison uses signed adjusted clocks; crossing midnight does not equate distinct
times. Calendar range failures remain catchable Python errors. ISO calendar
results share tuple equality, hashing and methods, with readonly named fields.

Gzip factories admit known filename working storage and compressor capacity before
opening an owned write destination. Binary and text modes use the same incremental
compression and native cleanup. Text conversion scratch lasts through its actual
consumer; retained read results stay funded after close. Read-open defers payload
validation and binary-read capability checks until consumption. Ordinary callback
or encoding failures preserve Python's earlier file-open/write effects. Refused
admission remains terminal and never initiates fallback or a retry.
Bulk gzip operations borrow the existing zlib scratch estimates for the native
compressor/inflater lifetime; small allowances can therefore reject earlier.
Final-output failure closes the wrapper and releases its native state; closing
again does not retry publication.

Gzip streams are sequential: random access and text position cookies are not
provided. Independent buffered writers targeting one path require an additional
host contract. `gzip.compress` follows Python 3.13: omitted/None mtime uses the
host clock; explicit zero produces deterministic timestamps. Incomplete members
raise EOFError; invalid headers and checksums retain their gzip errors.

Estimates cover major capacities/peaks, not exact object sizes or CLR heap/RSS.
Allow headroom for small uninstrumented storage, framework buffers, pools, JIT and
host work. Conservative retention may reject early; exact tiny-budget thresholds
are not promised. Hosts/providers must admit substantial storage through supplied
owners. A memory allowance does not control CPU time. Root `ReservedBytes` and
`PeakBytes`, result execution/projection peaks and denied-reservation reporting
are diagnostics, not separate allowances. See [memory controls](../tests/Lokad.Lython.PublicApi.Tests/GovernedMemoryContractTests.cs).

## Regex providers

Supply a borrowed [`ILythonRegexProvider`](../src/Lokad.Lython/Host/ILythonRegexProvider.cs)
to `LythonEngine`. One provider handles frontend validation/capture analysis,
runtime `re` and imports, and must outlive its users. Core exposes no backend
types or Utf8Regex dependency. Missing functionality fails explicitly, without
silently choosing another dialect. Native `fnmatch`/`glob` need no regex provider.

Providers receive borrowed UTF-8 data and shared memory owners. Immutable,
concurrent-safe metadata includes effective Python flags (inline flags and implicit
Unicode included), capture slots including zero and named-group indexes. Frontend
analysis uses the same effective flags. Prepared subjects provide match/search/
fullmatch, finditer progression, template substitution and captured splitting;
Lython supplies callable replacements and public results. Captures expose byte
ranges and Unicode-scalar offsets. Replacement validation, unmatched groups,
empty matches and Python errors must agree with compilation.

The per-execution 512-entry pattern cache compares immutable UTF-8 content and
complete compilation options, including the separate parsing-time verbose bit.
Lookup does not decode pattern text. Equal text reuses pattern identity; eviction
and `re.purge()` release cache slots without invalidating retained patterns or
their existing storage ownership.
Purge relinquishes the cache's table capacity before refunding its funding.
Compiled programs themselves are retained conservatively until execution cleanup,
including programs evicted from the cache; many distinct patterns can reject early.
Runtime group names are encoded once under the pattern owner and shared by named
lookup, `groupdict`, `groupindex` and `lastgroup`; capture order stays unchanged.
`match.expand` parses templates and emits owned UTF-8 through the governed byte
builder, including Python numeric/named references, octal escapes and template
errors. Its parsing and output processing check the active cancellation token.

Preparation receives the original UTF-8 subject and a `LythonRegexWindow` with
independently clamped scalar `Pos`/`EndPos`. Captures use absolute scalar/byte
coordinates, including lookbehind captures before `Pos`; slicing at `Pos` would
change anchors and lookbehind. Frontend constant analysis uses the same window;
unknown bounds remain uncertain. Substitution/splitting extensions return the
rewritten/split window, preserving the original assertion context. Reversed
substitution bounds raise `ValueError` before invoking replacement callbacks.

Compilation, preparation, matching, enumeration, replacement, splitting and
result processing receive the current token. Cached patterns save no prior token.
Entry/exit checks do not prove in-flight interruption: providers must cooperate
or report the limitation, without private timeout/work quotas or abandoned work.
Retained patterns/caches and working/replacement peaks use supplied owners.
Each successful `Compile` transfers a fresh disposable pattern handle. Frontend
analysis closes its handles when analysis finishes; execution closes its handles
after active cursors, subjects and callbacks finish, including failure/cancellation.
Dispose cursors before subjects and subjects before patterns. A provider may share
underlying programs through a separately funded longer-lived cache; closing a
handle must not refund storage that cache still retains. Metadata borrows also end
before pattern disposal. Pattern disposal is idempotent and refuses new work.
Finish users before refunds, and propagate
`MemoryAdmission` refusal without retry. Disposal is synchronous/nonthrowing;
borrowed results obey the interface's operation lifetime.

### Optional Utf8Regex adapter

`new LythonEngine(Lokad.Lython.Utf8Regex.Utf8RegexProvider.Instance)` selects the
optional `Lokad.Lython.Utf8Regex` adapter over pinned PythonRe 0.4.0. The adapter
is a repository project, not a published NuGet package. Its separate qualification
does not certify backend safety or couple core releases to it.

| Capability | Current adapter behavior |
| --- | --- |
| In-flight cancellation | Active tokens reach compile, matching, cursor advances, substitutions and splitting; isolated real-backend interruption tests cover these operations. No background matching/private quota. |
| UTF-8/offsets | Matching uses scalars, including supplementary characters; captures borrow original bytes and use an admitted scalar-offset map for non-ASCII input. |
| Windows | Original-prefix views preserve anchors/lookbehind and end bounds without a prefix copy. Python `match` with `pos > endpos` needs an unavailable backend capability and fails explicitly; reversed search/fullmatch/iteration are empty. |
| Capture metadata | Opening-order numbering and last-closed capture selection come from the backend. Global prefix flags include verbose trivia; scoped flags stay local. |
| Iteration/storage | Native lazy cursors fund current scratch/captures, including empty-match progression. Replacement-cache capacity stays charged to the pattern owner. Conservative estimates may reject early and require backend/framework headroom. |

See the [adapter README](../src/Lokad.Lython.Utf8Regex/README.md) and
[capability tests](../tests/Lokad.Lython.PublicApi.Tests/RegexAdapterCapabilityTests.cs).
[Cooperating-provider controls](../tests/Lokad.Lython.PublicApi.Tests/RegexProviderContractTests.cs)
qualify in-flight forwarding, refusal and funded unwind separately from backend
interruption capability.

## Host authority and I/O

[`ILythonHost`](../src/Lokad.Lython/Host/ILythonHost.cs) mediates working directory,
clock, text I/O, existence/stat, directory listing/mutation, copy and move.
Optional capabilities supply binary I/O, standard streams, subprocesses and
timing. Missing capabilities fail explicitly. Lexical paths grant no authority;
the host enforces paths, symlinks, races, commands and external-effect policy.
File unlink, empty-directory removal and replacement use the host's explicit
RemoveFileAsync, RemoveDirectoryAsync and ReplaceAsync operations. Their defaults
report missing capabilities before mutation. Generic RemoveAsync remains available
for internal cleanup. Hosts enforce the requested kind at mutation time; replacement
must preserve an existing destination when the source is missing or rejected.
Lython supplies no delete-before-move fallback. Existing hosts must implement these
operations to support the corresponding os/pathlib APIs.

Filesystem paths use type-based `__fspath__` conversion and accept text results.
`RunAsync` awaits that conversion in path construction, joining, common-path
iteration and asynchronous filesystem operations. Native path preparation reserves
decoded/component working storage before conversion and holds it through awaited
host use. Storage retained by handles and iterators remains conservatively funded
by the execution batch. Pure paths never grant filesystem capabilities.

All filesystem operations receive `LythonPathRequest`, with original operation
path text and an explicit contained working directory. Copy, move and replacement
receive one request for each operand. The host resolves actual
intermediate entries before applying dot segments; an empty path or trailing slash
must not redirect metadata to a different entry. Predicates return false for missing or non-directory traversal; os.path predicates
also suppress filesystem OSError, while pathlib preserves permission failures as
in Python 3.13. Admission, cancellation and capability failures still propagate.
Stat/size/time access raises Python-shaped errors. Hosts enforce containment and
actual component/kind rules at the operation; Lython does not use ambient process Cwd.
Recursive creation preserves prefix effects, including `new/../target`; lexical
formatting and already-validated identity/deduplication keys remain separate.

`CopyAsync` owns regular-file overwrite semantics. It validates original paths and
kinds, rejects same-file aliases and admits host replacement storage before effects.
Refusal or a rejected source preserves the old destination. Lython never deletes a
destination before copy. `shutil` admits its mandatory returned path before mutation,
and awaits asynchronous `__fspath__` preparation. ZIP path protocols also await
actual host work; default member-name normalization does not rewrite the file request.


Operations receive the active token. Write buffers remain valid until their
returned operation completes; consume or copy before completion. Storage retained
afterward belongs to the host. Pending borrows stay funded through actual users
and cleanup, including cancellation.

File iteration uses the owned byte-file capability and bounded caller buffers.
Whole-file/range/text read, write and append members have been removed from
`ILythonHost`. Hosts implement `OpenFileAsync` with efficient bounded reads and
accepted-prefix writes. `filecmp` compares two opened cursors. `Path.touch` uses
create/exclusive intent without rewriting existing data. `Path.write_text` opens
and truncates before encoding, matching Python even when encoding fails.
Writable ZIP archives open their target at construction: w creates/truncates and
append acquires one read/write identity. Renaming or replacing its path does not
redirect later publication. Append reads its snapshot through that same file and
requires `ILythonByteFile.RewindAndTruncateAsync` for a modified archive. This narrow
rewrite capability does not enable guest seek/update modes. The complete serialized
output and working storage are admitted before rewrite effects and remain owned
through actual writes. Unmodified existing append performs no rewrite.

Open ZIP resources join execution cleanup. Successful cleanup publishes completed
staging; cancellation, refusal or an unfinished member writer abandons publication
and awaits native retirement. A failed close retires the archive and a second close
is inert. Existing metadata access remains available, including read-mode cached
listings. Staged storage still retained by a closed handle stays conservatively
charged. Private input aliases clear before refund, after native cleanup. Some
intermediate serialization storage remains conservatively execution-owned; bounded
host-driven batches return all of that allowance when the owner ends.

## Cancellation and protection

Arrange cancellation or `CancelAfter` for finite execution, including
non-allocating infinite loops. Checks cover instruction execution, iteration,
long native loops and host calls. Some CLR conversion/parsing operations finish
before their next checkpoint. Deep recursive callbacks can also take appreciable
time to reject and unwind; cancellation does not preempt CLR exception unwinding.
Non-cooperating host/provider work cannot be
forcibly interrupted; see [regex limitations](#optional-utf8regex-adapter).

Cancellation and refused admission terminate the command through guest handlers
and suppression. Cleanup is awaited and funded; refusal retains its first
structured attribution. Completed effects are not transactionally rolled back.

Managed stack admission, Python recursion limits and conservative frontend
nesting bounds are independent protections. Delimiter nesting is at most 512;
unary nesting at most 256. Larger call-depth settings do not bypass stack safety.
There is no native stack probing, unsafe C# or P/Invoke.
