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

Compilation observes its token during indentation checks, tokenization, ordinary
parsing, literal decoding, container/callback fact inspection and provider work,
and between later compiler phases.
Lexical-fact discovery also checks between statements.
Later diagnostics, annotation formatting, lowering and
instruction construction can finish their current pass before the next check; CLR copies/conversions are
also not preemptible. Cancellation returns an invalid script with `LA2011`;
dispose that script to release its compilation owner. Refused admission retains
priority over cancellation.

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

Text values retain their storage owner, without retaining a source-of-birth span.
Allocation failures use the requesting operation's explicit or current synchronous
provenance. Internal helpers used without an execution context may have no span;
the public execution boundary still reports its active Python instruction.

Short copied text can store UTF-8 inline; adopted arrays retain their full capacity.
Native operations read spans. APIs retaining memory receive a stable backing view,
including a tiny cache funded by the original metadata allowance when necessary.
Factories write into reserved final capacity and publish only completed immutable
values. Native data views remain available to cleanup after command refusal.

Terminal JSON serialization adopts its completed UTF-8 buffer. The full backing
capacity remains charged, including its unused tail; avoiding a final copy can
reduce construction overlap while retaining more spare capacity. Reused
`iterencode` chunk builders still produce independent copies. Returned values and
aliases keep their owner funded until their users relinquish it.

Unicode indexing caches use the active caller's cancellation context during
construction and remain unpublished until completion. Cancelled construction
returns its working allowance while retaining the input's charge.
The optional index shares metadata with the original storage-owner reference;
scalar length and UTF-8 payload access remain direct. The base string estimate
prepays the small metadata carrier; the rune table is admitted separately. Reentrant
construction reuses a completed cache and refunds only unpublished working storage.

Exact number and string operators share scalar dispatch. UTF-8 equality and
ordering primitives accept the current execution context for bounded cooperative
checks. String methods, contextual structural comparisons, native sorting and
contextual dictionary text-key probes use these primitives without retaining caller
tokens. Cached key hashes do not bypass cancellation during equality. Custom
comparison and truth protocols keep their ordinary synchronous/async behavior.
Context-free native comparers have no token and retain their whole-span operations;
cancellation is then observed by the enclosing execution.

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
translation and supported positioning remain in Lython. Text writers stage the
final encoded bytes with the same sizing/writing kernels used by guest byte
encoding. They validate each complete input before publishing it, including
single-byte codec errors and newline-translated error subjects and positions.
Small writes copy directly from UTF-8 spans; actual pending I/O retains stable,
funded input and output storage until the host operation and cleanup finish.

Text output publishes batches of at most 64 KiB when staging fills, including
under `write`, exposed write methods, `writelines`, CSV and JSON. One working-buffer
admission precedes the first batch of a write; later batches reuse that funded
capacity. Geometric headroom can retain up to 128 KiB in the open file. Explicit
`flush()` and close release staging. These sizes describe transport storage and
place no ceiling on a write or file. Publication can occur before explicit flush;
a failed write can leave an accepted prefix. BOM and payload share the same
partial-publication failure rule. Guest inputs and other values retain their own
owners; bounded output staging does not reclaim arbitrary guest references.

Path helpers, local imports and archive input use the same owned-file capability. Whole-file reads grow admitted storage in bounded
chunks and fund capacity and construction overlap before publication. Text helpers
use the shared decoder, retaining input funding until the result is charged.
UTF-8 and Latin-1 decoding produces admitted native text directly, including
UTF-8 error handling and newline translation. Text readers borrow unchanged valid
UTF-8 from the admitted raw prefix; transformed windows retain codec output funding.
Complete non-inline lines and read values can share those immutable windows.
Once shared, raw storage is replaced under overlap admission before another refill;
aliases remain valid after later reads and close. The execution/result owner keeps
the full backing capacity charged, including unused prefix/tail. Closing the file
or discarding one line does not refund shared backing; ownership ends with the
bounded command/result. This can deny earlier than independent copies when only
a small part of a window is retained. Small inline and accumulated reads keep
independent storage. Use bounded host-driven commands for long streams.
Shared byte-line scanning uses managed bulk searches, selecting newline mode once;
CRLF/window holdback and scalar read limits stay with the caller.
Readline, file iteration and native CSV input share the same window scanner.
Buffered asynchronous reads finish directly; a pending refill retains the private
builder in one iterative continuation until the read settles. Native destinations
remain owned by their caller throughout.
Requested reader cleanup keeps raw and decoded storage funded until the active
read finishes, including allocation callbacks and pending host operations. Async
reads await disposal without blocking their caller. Cleanup prevents another
refill; already buffered copying may finish. Reentrant reads fail explicitly.
Carry compaction and refill wait until the current window is relinquished. Returned
text always owns independent immutable storage before raw-buffer reuse. UTF-8 replacement, ignored
bytes and backslash escapes preserve Python malformed-unit grouping; newline
translation follows error handling. Output is sized before its allocation and
remains charged to its owner, without an intermediate UTF-16 string/builder.
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

Annotation diagnostics retain conservative branch facts, including unreachable
checks, independently of the main analyzer's refined flow. Shared lexical
preparation identifies annotation demand and regex imports; ordinary scripts
need no extra annotation-demand walk or binding setup. Non-regex lexical
scopes omit binding analysis after their final relevant annotation check. Constant
regex analysis retains its full conservative bindings across dead/deferred scopes.

Compilation tokenizes/parses source, validates the subset, computes scope facts,
lowers constructs, and builds executable instructions. String-literal decoding borrows
source spans; unescaped bodies need no decoding builder, while adjacent pieces
create concatenation storage only when needed. Already-parsed f-string text uses
the same body decoder directly. The complete validity walk
selects generator, modern-type and future-import validation only when the relevant
syntax occurs, including in dead bodies. Those validators retain their own scope
rules and diagnostic order. Comprehension and type-parameter validation allocate
name sets only when introducing bindings; unchanged scopes share their read-only
inherited set. Lexical scope preparation
follows parsing/name rewriting; analysis, lowering and class creation reuse those
immutable function/class/lambda facts. They belong to the compiled program,
without a cross-script cache. A function declaration also owns one flat parameter
name/slot table, role counts and generator classification, shared by analysis,
lowering and its created functions. Flow-dependent abstract bindings stay separate;
no per-call keyword-shape map or repeated generator-body scan is needed.
Ordinary call syntax also owns its positional/keyword expression partition, shared
by static contract checks and return analysis. Inferred values belong to the
current analysis state; `*` and `**` expansion is resolved again from that state.
Executable local slots use that same prepared parameter order,
then append body locals. Mutable defaults, keyword-name values, attributes and closures
remain function-owned. Native generated definitions admit their layout before
allocation and keep its construction overlap funded through default creation.
One scope walk collects directives, local bindings
and annotations, retaining explicit first-binding order. Function slots reuse these
facts after the parameter prefix. Module function, import, class and type-alias
declarations register slots when their statements are compiled; declarations after
an unconditional exit create no slots. Live namespace writes and deletion update
the same bindings. Module declarations also keep their backing namespace values
after the executable frame ends, independently of its relinquished slot capacity.
Module facts also pass through lowering into instruction construction. The lexical
preparation walk decides implicit `super` preloads after seeing later declarations;
module bindings never supply lexical class cells to child functions.
Module preparation keeps binding facts without computing a class-cell capture;
function and class scopes retain their own capture requirements.
Class creation reads its prepared own-cell requirement; it does not repeat lexical
syntax analysis at execution. Each created class still owns its fresh cell.
Optimized local snapshots use current frame/cell values before extra mirrored names.
When enclosing compilation supplies complete lexical bindings, function and generator
free names use the existing live global/builtin lookup after local and captured
names. Generator definitions use that same enclosing compilation; they do not
first build a separate code object without its capture facts. Independently
compiled class methods and namespace-backed class/type-scope lambdas
retain general lexical resolution. No global value is frozen or cached by this choice.
Lambda code is prepared once during compilation, including lambdas in lazy type
metadata; their values and defaults still evaluate at the Python definition point.
Executable sites reuse their enclosing builder's capture facts. Remaining lowered
consumers use their live namespace closure. Calls use the same executable/native
function machinery; there is no separate interpreted function backend or runtime
lambda compilation. Lowered class suites and annotations retain their evaluator.
Each abstract binding state caches resolutions by syntax-node identity and clears
them when its facts change; clones and branch joins have independent caches.
Container and callback queries inspect shared fact payloads
once per query with an iterative traversal; repeated tuple aliases do not expand
into a recursive tree. Temporary traversal state ends with the query and does not
trace guest storage. Shared syntax walks use value enumerators for
ordinary fields/lists and the existing nested walkers for recursive target/header
shapes; no retained child-edge cache is built. Exception header expressions
execute through those instructions; bare handlers need no second type-name resolver.
Instructions store an opcode, source span, opcode metadata and compiler-prepared
storage positions. The opcode determines each metadata operand's domain;
operand projections and control-flow retargeting keep explicit validity checks.
Ordinary jump chains resolve once with iterative cycle handling. Dense block IDs
drive reachability and target remapping; each cycle entry keeps its identity.
Protected abrupt jumps retain their original target so iterator and exception
cleanup run in the correct region. Loop transfers within the same protected
suite use ordinary jumps; only transfers across a suite boundary need runtime
unwind routing.
Assignment, augmented-target reads, deletion, unpacking, assertions and explicit
raises use ordinary instructions. Store metadata keeps its target span separate
from the instruction's guard/routing span. A shared synchronous prefix completes
ordinary work; incomplete protocols retain their operands until awaited completion
and retirement. Deletion keeps only its executable shape and name/bounds metadata.
Unpacking borrows exact builtin tuples' immutable storage, retaining the tuple
through its users. Mutable inputs and user iterators still take an owned snapshot
before assignment; starred remainder lists admit their own storage before target
effects. The same rule serves synchronous and asynchronous assignments, loops and
annotation unpacking. Cancellation and refused admission still stop publication.
Completed tuple values copy directly from managed spans into their final backing,
with capacity and scalar headroom admitted together. Inputs stay funded until the
copy succeeds; owned arrays and suspending materializers retain their distinct
ownership transfers. Long estimates and copies observe cooperative cancellation.
Formatted construction, literal appends, conversions and fields use ordinary
instructions and shared operand cleanup. Native fields, including evaluated dynamic
specifiers, need no lexical context solely for formatting. Actual protocol values
promote at their original instruction and await their callbacks. Formatting helpers
keep transient guest values outside the shared dispatch loop.
Adjacent plain local and constant reads can feed fixed-arity arithmetic, unary
operations, subscript reads/stores, member/receiver loads, iterator acquisition,
truth branches, local stores and returns directly. Captured or
mirrored locals keep their ordinary lookup. Missing direct locals retain their
original read spans in cold metadata. One shape pass after forwarding prepares
physical positions, live temporary extents, maximum workspace and exception-entry
depths. Calls can still determine the workspace capacity.
Handlers read those positions directly into references; synchronous type
subscription borrows immutable instruction metadata.
Comparisons consumed immediately as conditions use one comparison/branch
instruction. Exact numeric truth needs no boxed temporary; escaping comparisons
retain their Python result objects. Ordered comparison helpers share the operator
and exact numeric ordering. Custom comparison and truth protocols keep their
evaluation order and can suspend under RunAsync. Exact string comparisons complete
synchronously under either API.
Operation values are captured before protocols or admission; actual asynchronous
work retains those values until completion. Type-subscription getters use the
existing funded protocol slots to preserve the original receiver and key across
rebinding. Context promotion adds no separate operand cache or ownership layer.
Ordinary expression and iteration results can complete directly into an adjacent
local binding. A marker in the existing extent metadata preserves the physical
operand positions; the following store supplies its target and separate source
span. Inputs retire in their original order, then cancellation/refusal is checked
before binding. If disposal is pending, the existing funded result operand and
store remain the continuation after awaited cleanup. No result is published early
and no extra async completion stage is needed.
Failable reads retain their source spans; evaluated call and protocol packets
retain snapshots across callbacks. Ordinary instructions carry no native-return
provenance. Eligible helpers own compact prepared native operations, referring
to original source spans and shared call metadata. Nodes keep semantic operation
facts; they embed neither ordinary executable instructions nor physical stack
positions. Compiler preparation validates these facts once.
Proven captured helpers use these same operations with live definition-owned
cells. One non-owning readonly input view borrows the bound parameter prefix
and the definition-owned live cell collection separately; it does not pack or
clear a second buffer or encode cells as appended parameter indexes.
Numeric nonlocal stores and their proven continuation can omit function-workspace
binding and ordinary instruction dispatch. Actual owner/cell/namespace writes
preserve aliases and later reads. The complete proof precedes effects; unsupported
protocols or targets keep ordinary execution, and native errors never restart the
body. Mutable global/closure reads after earlier effects retain ordinary execution.
Existing native nesting and inline-capacity bounds remain optimization thresholds;
wider captured signatures keep ordinary binding and execution.
Closed native helpers can run while their caller owns active exception-handler
aliases. They share current exception state and global lookup, without binding or
unwinding the caller's aliases. Class/annotation contexts retain their lexical
machinery; custom protocols still promote to ordinary execution before effects.
Named and default arguments use the same binding fill as ordinary calls. Small
closed native helpers and methods can fill managed inline slots without renting
function workspace or creating an activation. Direct method calls and saved bound
methods pass the evaluated receiver separately through this same binder; exposing
a method as a value still produces its normal Python bound-method object. This path accepts only native default storage
that cannot invoke a protocol; values remain funded by their caller and definition
owners through completion. Wide signatures and dynamic defaults keep owned binding,
including across suspension. The inline capacity is an optimization threshold,
not a limit on Python arguments. Live default aliases retain their normal behavior.
The ordinary code is one instruction array with absolute branch and
protected-suite ranges; temporary builder blocks are discarded. An explicit
completion instruction distinguishes module completion from a guest return.
The executable engine runs both public APIs and local imports, using local/closure
slots, operand storage and resumable generator frames. Lowered evaluation remains
for class suites, annotations, lazy type metadata and actual statement-opcode
consumers. Supported expressions in executable code use instructions; there is no
generic expression opcode or whole-program alternate backend. Unknown compiler
nodes fail explicitly. Imports retain canonical module namespaces.
Eligible invocations begin with owned slot workstate. Native zero-argument string
operations, exact positional split/replace/affix calls, dictionary views and exact
list/tuple iteration run there without allocating a lexical context. Canonical
truth tests and unary `not` share eligibility; custom truth protocols retain their
normal frame. String operation selection, shape checking and invocation are shared
with prepared native helpers. Evaluated arguments stay snapshotted across admission
callbacks. Other operations materialize a context when needed; defining captured
locals, class cells, generators and protected bodies retain their lexical frames.
Dynamic index arguments, keyword binding and guest callbacks use ordinary execution.
In `RunAsync`, split and replacement counts can await a guest `__index__` that
reaches host I/O; replacement validates its old/new strings before that callback.
The same native dispatch serves both forms. Admitted iteration positions transfer
with the workstate, with one owner throughout. A separate interpreter object is
unnecessary. Terminal cleanup finishes pending users, then detaches
operand storage, private caches, handler records and caller references. Closures
can retain that context's names and cells without retaining refunded workspace.
Cleared local and cell storage represents an unbound name; guest `None` is the
distinct `PyNone` value. Reads raise Python's unbound-local or free-variable error
before enforcing the nonempty temporary-operand invariant. Workspaces are cleared
on return and need no sentinel fill at invocation entry. Unbound names stay absent
from exposed namespaces, including across generator suspension and cell deletion.
Nonlocal ownership comes from immutable scope facts and lexical parent links;
invocations do not rebuild a separate owner dictionary. Executable reads use
captured cells, and dynamic reads and writes resolve the same lexical owners.
Immediate instance-method calls keep the selected function and receiver in their
operand storage when default attribute lookup proves an ordinary function binding.
They do not construct a temporary bound-method object. Selection precedes argument
evaluation; custom lookup hooks, descriptors and exposed method values keep their
normal Python behavior. Both execution APIs use the same calling convention.
Common builtin methods also separate the receiver from a shared immutable
implementation and signature. Immediate regex match, list append, dictionary
get/setdefault/views, file write, CSV row and string casing/predicates/encoding,
join/replace/affix, segmentation, search and padding use these shared declarations.
There is one member-call load and no separate typed no-argument target protocol.
Ordinary calls borrow their existing operands; zero-argument declarations have a
fixed-arity invocation without empty argument-memory transport. Exposed methods retain a distinct bound value
with its receiver and metadata. Fixed zero-argument values reuse their existing
wrapper and raw argument validation; other shapes use the ordinary binder.
Suspended protocols keep the receiver and operands funded until completion
and cleanup. Custom string index, width and line-ending truth callbacks can suspend
under RunAsync. Native no-argument operations and helper-return proofs remain shared.
Zero-argument whitespace `split()` and `rsplit()` after native `lower()` or
`upper()` share one prepared
recipe with closed helpers, including inside branches and protected bodies. They
write final words without allocating a cased intermediate string. Custom receivers
keep ordinary method dispatch and suspension; intervening local bindings remain
observable on ordinary execution. Final storage is admitted before allocation and
keeps the current cancellation context and requesting source span.
UTF16 byte and file output transcodes bounded UTF8 spans into admitted final
storage, with explicit byte order, BOM and newline framing. Sizing remains a
preflight; cancellation checks separate scalar-aligned chunks. No intermediate
character buffer is allocated.
Ordinary positional class calls also borrow the existing operand storage. The
engine's default allocator needs no class-prefixed argument copy, and user
allocators/initializers bind the class or instance directly into function locals.
One construction sequence serves both APIs, selecting the actual returned type's
initializer after allocation. Static/classmethod and descriptor initializers keep
their binding rules; descriptor/body I/O can suspend under `RunAsync`.
Arguments and the new instance remain owned until those users and cleanup finish.
Dynamic engine callables receive admitted owned argument copies when required;
these copies conservatively remain charged to the execution/result owner.
Suspended generators keep their state until completion and own one context handle.
They initialize locals and cells through the shared executable-state constructor.
Bound arguments copy directly into their admitted retained workspace before the
call's binding storage returns. Complete lexical scopes need no blanket namespace
mirroring; lowered class construction and incomplete scopes retain their namespace
bridge. Nonlocal stores and deletions update captured cells even after the defining
workspace has detached, while live defining frames retain synchronized slots.
Match patterns have one compile-owned lowered representation shared by executable
code and class suites. Immutable literal keys, capture indexes and required working
positions are prepared once. Captures stay private until a complete pattern succeeds;
validated OR alternatives overwrite their own indexed slots. Executable frames admit
and clear this working capacity, including scalar headroom, before guards or bodies
run. This can refuse admission earlier than allocating scratch at the matching opcode.
Lowered consumers use the existing temporary reservation. Dynamic mapping keys retain
ordered key evaluation, duplicate checks, value extraction and subpattern execution.
Class patterns retain only their actual expression and child patterns; no
synthetic sequence or source-name dispatch table is prepared. Builtin match-self
patterns borrow the whole subject and reuse existing instance/type checks.
Class matching uses one extraction phase for user classes, named records and
builtins. Prepared scratch retains attributes until subpatterns finish; only
selected positional names are decoded, with temporary storage admitted before
decoding and cleared before refund. There is no per-match copy of the entire
`__match_args__` tuple or URL-specific duplicate set. Descriptor lookup, duplicate
checks and extraction errors precede subpatterns; value patterns share contextual
equality/truth handling. Pattern callbacks currently require synchronous completion.
String/bytes literals and integer magnitudes belong to lowered nodes and the existing
compiled constant owner; no global weak literal cache is needed.
Successful loop advances and true conditional arms continue at the next
instruction; they need no unconditional entry jump. Exception routing restores
the compiler-prepared protected-entry depth.

Actual executable frame exit restores the entry exception state and removes
abandoned handler bindings, including cancellation or refused admission at
instruction entry. Yielded generators and pending calls retain their frame state;
native cleanup and cursor disposal finish before owned storage is released.
Pending child-call failures and generator exceptions route when the frame resumes,
after pending operand cleanup. Active delegation forwards the exception to its
delegate. Completed returns use the return value itself as their marker; Python
None remains a real value. Ordinary instruction execution retains cancellation
and admission checks without polling a separate pending-exception/return flag.

Private loops over exact builtin lists and tuples keep the sequence in its operand
slot and advance by index. One fixed-size position array is admitted on first use
and shares the operand workspace's lifetime, including generator suspension;
terminal frame cleanup releases it. List iteration observes live replacement,
growth and removal. These private loop states never escape as Python iterators.
Exposed iterators, generator-expression outer acquisition, user protocols and
resource-bearing/asynchronous iteration retain their ordinary cursors and cleanup.
The additional workspace can reject a tighter allowance earlier.

Eligible dictionary counting loops run consecutive native iterations without
repeating operand and call setup. They preserve ordinary local binding,
optional integer totals and cancellation.
Custom protocols, wide values and growth resume the original executable body
after the current loop binding, preserving evaluation order and suspension.
Refused admission stops execution; it does not select that continuation.
Uncaptured totals without namespace or mirror stores can stay in a managed
integer during that segment. Every exit publishes the ordinary Python integer
before any handler or callback; wider values retain normal exact promotion.

Private cased-word iteration can probe existing counting keys directly from mapped
UTF-8 bytes within the string's inline capacity. Inserted keys and observable loop
bindings still materialize independent immutable Python strings. Every continuation,
callback and error handler sees the completed binding; native unwind publishes it
from already-funded storage without new admission. Larger words and dynamic
protocols keep ordinary materialization. Eager preflight still funds all eventual
words, including capacity not physically materialized during the native segment.
Private cased-word iteration prepares whitespace ranges once and consumes those
facts. Its range buffer stays funded through the cursor's actual users and returns
through operand-stack cleanup, including early exits and cancellation. Completed
bindings are published before a refund can reenter the host.

Private dictionary entries can retain exact counting results as managed integers
without boxing each update. A Python value read materializes and caches an ordinary
immutable integer; copies preserve its identity and retained older values remain
independent. Existing coarse value capacity funds the eventual box. Shared record
layouts materialize before publication and keep their own adopted value arrays.
The additional key capacity is admitted before allocation, including replacement
overlap, and can reject tighter allowances earlier. Protocols and wider arithmetic
retain their ordinary semantics.

Both public APIs advance the same non-async instruction loop. Completed operations,
including failures and cancellation, stay in that loop; asynchronous execution awaits incomplete operations and cursor
disposal before resuming it. Suspended failures keep their original instruction
span. Abandoned cursors finish cleanup before another guest instruction or handler
runs. Both APIs use the same identity-based member-cache eligibility rules;
mutable descriptors and guest protocols retain their normal resolution.

Functions and generators defined in class suites have executable bodies prepared once
during compilation, including definitions inside compound suites. Class suites,
decorators, defaults and annotations keep their existing evaluation order. Methods
reuse lexical/class cells and retained namespaces; nonlocals without known parent
cell slots use the shared namespace lookup. Unused lowered method bodies are
discarded. Other real lowered consumers remain.

Executable function bindings retain declaration inputs and prepared code without
the original function body tree. Defaults use one evaluator across both APIs and
lowered definitions; a continuation handles actual suspension. Empty construction
maps are immutable shared metadata. Python default tuples/dictionaries remain
distinct per function and reflect later mutation. Private default storage stays
admitted through awaited evaluation and construction.

Plain builtin functions and constructor parameter layouts are fixed shared
metadata. Every execution still constructs its own mutable builtin types,
exception objects and namespace. A function's `__self__` resolves through the
current caller; shared definitions retain no execution, host or caller token.

Ordinary calls to executable functions with slot-local storage advance one explicit
caller/callee chain, including keyword and variadic signatures. The shared binder
writes final invocation storage and observes live defaults. Completed binding
enters the body directly; genuinely pending default lookup retains call admission,
caller operands and bound storage until completion or failure. It then enters the
same body transition, without recursively starting another instruction driver.
Eligible functions and ordinary methods defer lexical contexts independently of
signature shape. Owner binding alone does not require a context; lexical class
cells, implicit `super`, captured local cells and protected bodies keep their
scope machinery.
Lowered calls and callbacks retain their general entry. Type subscription uses
that chain for executable `__class_getitem__` methods and raw descriptor `__get__`
methods, followed by the getter's callable result. Each invocation owns its
value array until its body and awaited cleanup finish. Local bindings occupy its
prefix and temporary operands its suffix; every compiled function binds directly
into that storage, including functions requiring namespace mirroring. Mirrored
namespaces copy values for their class, closure and lowered readers; they never
borrow the reusable array. Mirrored functions retain their scope context.
Suspended callers retain their operands.
Ownership admits the fixed whole-frame capacity and fresh cell/control storage
before construction and retains them through suspension. A cache-eligible member
lookup admits its frame's table before descriptor or protocol effects. Instances,
types, super objects, functions and contextual dynamic receivers keep uncached
lookup and acquire no unused table; direct builtin receiver paths also need none.
Refusal remains terminal, including this
later admission after earlier body effects. Lookup facts occupy a flat admitted
array, without individual member-cache objects. Modules and generators own their
invocation's table; ordinary functions borrow their definition's table across
calls. Function tables stay charged to the execution owner until cleanup,
including when guest names no longer reference the function. This conservative
retention can reject tighter allowances earlier. Reentrant first admission reuses
already-established backing and refunds only the unallocated pending reservation.
Repeated string field reads reuse positions in immutable shared CSV/JSON key
layouts across rows and helper calls. Those entries retain layout and key, never
a previous row or its value; every hit reads the current live slot. Deleted or
detached fields and general Python mappings keep ordinary lookup. Backing
admission precedes the layout check because a host callback can mutate the row.
Formatting still reads each actual field in order and supports protocols that
suspend after earlier formatting changes later fields. Native helper guards keep
their existing admission ordering.
Read-only lexical misses leave unused
namespace dictionaries unmaterialized; explicit namespace consumers retain the
same stable dictionary. Arrays do not
grow. Temporary retirement never clears the local prefix. Terminal cleanup refunds private working storage;
escaping cells first acquire retained ownership. Reused bound locals are charged
once. Generators keep their existing retained metadata owner and admit their
workspace, including local-cell capacity, before first advancement. These conservative estimates can reject
tighter allowances earlier, including capacity needed only by an unused branch.
Large native-call argument packets acquire separate working admission before
allocation, even when the operand buffer already has capacity. Their reservation
lasts through the awaited call and cleanup; construction observes cancellation.
Function binding uses one execution-owned idle workspace, retaining the largest
returned array. A call reuses sufficient capacity while reading and clearing only
its logical parameter or compiler workspace extent. Actual backing capacity stays
charged through smaller loans; unused slots contain no prior guest references.
Nested or suspended calls keep distinct active arrays until their users and cleanup
finish. Insufficient idle capacity is relinquished before one new admission, with
no retry after refusal. Resource cleanup ends idle reuse and refunds
the cache; later active returns release their own charge. All nonempty function
buffers, including small calls and generated dataclass methods, use this admission.
The entire compiled capacity is admitted before binding, including for mirrored
functions; it can remain idle in this cache after the call. Idle retention and
earlier capacity admission can reject tight shared allowances sooner. No workspace crosses
executions or retains a prior token or guest argument.
The same cache policy separately retains one private word-range buffer per
execution. Active range loans survive idle-cache shutdown; all backing capacity
remains charged, including oversized reuse. This additional capacity can reject
a tighter shared allowance earlier.
Each call opcode prepares the evaluated function and receiver at the instruction.
Native borrowed/inline binding and the ordinary deferred prefix remain shared by
both APIs. Completed bodies publish directly after bound argument, workspace and
call-admission cleanup, without staging a pending function on the caller. Only
actual pending binding or child execution transfers to the existing frame runner.
The caller's operands and the callee's inputs stay funded until actual completion.
Real children retain their source/completion metadata; their result is staged until
the parent's async driver can await disposal before publication. Constructor and
type-subscription protocols share this call entry and preserve their postprocessing.
Suspending transitions retain the originating source span before a completion can
advance the instruction. Dynamic calls keep the general binder.
Ordinary construction with the sealed object allocator and a compiled initializer
whose arguments occupy executable locals uses that same runner. Positional,
named, keyword-only and variadic calls share ordinary function binding: it
allocates once, binds self into the final locals and publishes the instance only
after the initializer returns None. Default-key protocols may genuinely suspend;
their owned input/local storage survives until completion and awaited cleanup.
This includes the builtin `staticmethod` wrapper around `object.__new__`; checking
that sealed wrapper does not invoke guest descriptor code.
Initializer selection observes metadata after allocation. If admission changed it
to a dynamic hook, initialization continues on the existing instance through the
general binder. Dynamic engine hooks receive admitted owned argument packets
when they may retain inputs; their storage remains funded with the execution.
Operand retirement recognizes the runtime's four temporary-value classes through
one internal base class and disposes them in reverse order. The existing compiler
shape pass bounds the slots that can hold private temporaries, including seeded
generator cursors and conservative duplication. Retirement skips classification
of higher guest operands while clearing the full retired range. Pending asynchronous
cursors transfer to the frame's awaited cleanup chain before their slots clear.
Return values and failures resume the existing instruction
and exception routing. Prepared operations route directly to their existing
handler; native stores do not traverse asynchronous display and definition
forwarders. Genuine suspension retains the original operand cleanup.
Guarded native return expressions retain binding and function/interpreter entry
checks while omitting resumable interpreter storage. Closed helpers, including ordinary nested definitions,
borrow their defining execution state and resolve globals in the module. Their
compiler proof excludes creating captured local cells and observable lexical work, so a separate
invocation context is unnecessary. Fully supplied positional
calls without a bound receiver borrow their admitted caller operands synchronously,
without constructing bound argument storage. These completed helpers run directly
in call dispatch, before pending-call state is created. Call and function stack
admission still apply; result publication and input retirement follow completion.
Eligible acyclic branch returns use this same prepared native expression proof.
Consecutive `not` operations used immediately by a branch are normalized during
preparation by exchanging the existing conditional roots. Other unary expressions
keep ordinary execution; no additional runtime unary plan is needed.
All type proofs precede effects; only selected operations execute, with the selected
return's source span. Empty lists remain fresh and admitted, including cancellation
after admission. Loops, shared joins, custom truth and format proofs that query
container values keep ordinary executable dispatch. No separate branch interpreter
or runtime working owner is introduced.
Native helper inputs share the ordinary direct constant/local operand convention;
consumed pure leaves need no separate read node or recursive read dispatch. Real
root and mutable-name reads retain their metadata. Both paths use one fixed local
prefix prepared with the code. Native operations, members, entries and returns
retain cancellation/refusal checks; pure input reads have no checkpoint-count contract.
The native evaluator borrows one readonly stack view of the actual parameter
prefix and the definition's fixed collection of live incoming cells. It neither
packs cells after parameters nor copies them into another argument buffer.
Parameter bounds remain separate from cell indexes, including missing-argument
checks before effects. The view cannot escape or suspend and owns no allowance.
Already-evaluated caller temporaries or the real binder keep parameters funded;
the defining function keeps its cells and their existing owners alive. Returning
an argument object preserves its existing ownership. Defaults, dynamic binding and suspending bodies retain owned
bindings. Functions requiring lexical storage or special scope handling retain
their function contexts. Native conversions,
dictionary string-key reads and compound
native string operations qualify only after the entire expression's runtime
guards pass, before native work. Compilation prepares operation kinds, method
arity and supported casing/formatting combinations. Closed zero-argument string
calls reuse the ordinary method declarations and frontend return facts; no separate
method-kind inventory is needed. Existing casing/split fusion retains its original
receiver and source checks, without a discarded casing node. Invocation checks actual
types, callable identities and namespaces. Native global reads require the
compiler's explicit global opcode; unresolved lexical names retain ordinary
scope resolution. Prepared values own immutable original opcode metadata and member/return source
spans independently of physical bytecode positions. Ordinary operand forwarding
still applies when native guards fail. One shared compiler/runtime result-shape
fact covers primitive integer, float and boolean binary operations and string
addition; execution uses the canonical operators. Custom protocols keep ordinary
dispatch. Evaluation requires no operand or argument arrays.
They retain no caller values or tokens. Deeper or unsupported
expressions keep the full engine. Single-value primitive formatting can share
that proof. Multipart formatted strings use ordinary executable instructions and
operand storage, including in context-free helpers. Closed primitive fields run
synchronously through the shared formatter; dynamic fields create their function
context at the current instruction and retain the genuine asynchronous protocol
path. Earlier fields, allocations and bindings are not replayed. One working
UTF-8 builder releases on failure or cancellation and transfers retained output
with its funding. The native preparation declines mutable name/global/subscript
reads after potentially allocating operations: admission can change their values,
so an earlier type proof cannot authorize a later cast.
Adjacent local stores/loads can forward their
value inside the native proof when no later read needs that binding. Original
instructions and local metadata remain intact for dynamic behavior and inspection.
Custom protocols, unproven captured effects, observable locals and
possible suspension keep the full executable path. Callee lookup observes current
name/builtin rebinding. No alternate execution begins after native work or refused
admission. Both paths share the existing operations, cancellation and ownership.
Synchronous calls, asynchronous calls and native return expressions share unary
builtin selection; ordinary calls keep broader protocol/error behavior while
native returns require a complete result/effect proof.
Eligible helpers begin with their owned local/operand storage and existing
incoming cells in the same instruction dispatch, without an invocation context.
Cell reads observe live values directly from that work state; unbound reads keep
their Python error/span. Cell assignment uses its known owner slot to update an
active local cache and applies the existing conditional namespace-mirroring policy.
It needs no repeated name-to-slot lookup, separate owner-store path or writer's lexical
context. Inactive owners may be retiring pooled storage and do not receive cache
writes. Generators synchronize captured cells or their mirrored namespace before
resuming, including lowered class writes and deletions during suspension.
Missing/ownerless cells and namespace-sensitive owners retain the ordinary lexical
context and resolver. Inlined comprehension bindings take precedence over the
surrounding scope's global/nonlocal directives when storing or deleting names. Plain list and tuple
construction reuse their admitted storage builders in this loop. Starred displays
and dictionary/set key protocols keep their ordinary protocol paths. Scope-sensitive work,
guest protocols, nested calls and host operations create the ordinary context at
the current instruction and adopt that storage; bindings and prior effects are
not replayed. Protected bodies and functions creating captured local cells start
with their normal context.
Native dictionary writes use this deferred context only when an already funded
table needs neither capacity growth nor key protocols. Growth creates the context
before admission. Operand storage retains shared execution services rather than
lexical identity; cancellation, nesting admission and cleanup remain unchanged.
Mirrored/lowered functions and native/protocol callbacks use their general
invocation routes. Short executable calls and shared native callback helpers pass
managed stack arguments through the same borrowed entry on the callable contract.
Function, builtin and JSON binding consume values into invocation-owned storage
before any default lookup or body can suspend. Bound receivers follow the same
rules. Constructors and wide calls retain owned packets through their actual
cleanup; other callable implementations receive invocation-private arrays whose
references are cleared after completion. Escaping guest values use their ordinary
retained owners. There are no thread-local argument pools or callable-type switches
in the shared callback helpers. A bound
function body reuses its call admission rather than adding a second logical
nesting charge. Raw protocols retain function-depth and managed-stack protection;
module entries own interpreter admission. Managed stack checks, large-stack
workers and configured recursion bounds remain active. Compiler
capture facts determine eligibility independently of lexical placement. Defining
closure chains remain retained; scope-sensitive operations promote before they
observe function locals or invoke guest protocols. Incoming cells retain their
defining owners; class, annotation and type-parameter scopes keep their existing
lexical handling.

Reusable workers provide the managed stack for the synchronous prefix. Actual
asynchronous suspension releases that worker instead of parking it through I/O.
Each execution shares one state containing host capabilities, caches, stream
capture, exception state, cancellation, memory admission and nesting counters.
Lexical frames and closure cells remain distinct from that shared state. Guard
checks test terminal admission refusal before cancellation; nesting entry also
checks the managed stack. Retained file owners use the same state without keeping
the lexical frame that opened them. Executable loops and checked enumerable
consumers share one cursor, with synchronous iterator dispatch resolved at
construction. Native acquisition stays lazy where required; dictionary iteration
captures its version at acquisition. Private cursor disposal is terminal, keeps
the original subject through actual asynchronous cleanup, then clears its roots.
Breaking a loop does not close a retained Python iterator. The core has no runtime NuGet dependencies;
the optional regex adapter owns its backend dependency. Python tokenization reads
normalized source directly, preserving original positions without a masked copy
or a generic regex lexer. The parser and executable engine consume that token stream.
Indentation uses eight-column tab stops and checks alternate columns to reject
ambiguous tab/space mixes. Blank and comment-only lines do not establish levels.

Implicit numeric methods bind from the type, bypassing instance shadows and
ordinary `__getattribute__`. Binary operators and `divmod` share reflected-method
precedence; RunAsync awaits supported descriptor and method callbacks. Sequence
repetition uses the same type-index conversion for expressions, in-place updates,
`operator.mul` and direct native methods, preserving callback order and aliases.
RunAsync awaits custom hash descriptors/bodies for public hashing, explicit tuple/
slice hashes, generated dataclass hashes and native dictionary reads. Dictionary
get/subscript and dictionary/set/view membership expressions share the prepared
lookup, including pending key hashing and equality. Native completed keys return
directly; the synchronous engine keeps its direct semantic walk. Dictionary/set
mutations, cache-key protocols and remaining Counter/defaultdict/ChainMap paths
can still require synchronous completion. Read support does not make every
mapping operation suspendable.
String-key reads without stored protocol keys share one native completion proof
across subscript/get/lookup. It is checked after argument evaluation on the retained
receiver; argument-side insertion can require the awaited equality path. Completed
results use direct publication while pending operations retain their borrows.
Contextual key preparation computes the hash and equality-dispatch requirement
in one walk. Tuple elements and slice bounds are visited in Python order: an
earlier hash callback runs before a later unhashable value is rejected. Native
dictionary copies retain stored hashes without replaying callbacks.
Explicit dictionary queries carry their execution context into hashing and probing.
Native string/bytes hashing carries the current token through4096-byte scan chunks,
including public hash calls, native hash methods and dictionary fast paths. Only a
completed scan publishes its cached value; caches retain no caller token. Raw CLR
comparers remain context-free and do not dispatch guest callbacks.
Native string reads and already-funded string-key writes avoid ambient state;
structural and protocol queries establish it at the query boundary and restore
the caller's provenance when they finish or fail.
Generated dataclass hashes admit their field snapshot before reads, complete all
reads before hashing fields and keep that storage funded through pending callbacks.
Hash guards carry the execution's explicit cycle state across awaits; no ambient
thread-local scope spans suspension. Cancelled/refused work awaits cleanup and
preserves the first failure. Empty set queries use the same ordered preparation;
they perform no preliminary structural hash.
Structural hash/equality guards keep active identities in one execution-owned
path. Single-object and pair cycles remain independent; scope exit removes their
references, including after asynchronous thread hops. Membership is linear in
the active nesting depth, bounded by managed stack admission.

Counter comparisons traverse both live key sources in Python order. Dictionary
view relations use dictionary membership, comparing stored pairs directly for two
items views. Value comparisons and truth tests can suspend under RunAsync. Each
active traversal
conservatively reserves the source backing until callbacks and cleanup finish,
including after a callback clears the source. This can require additional shared
allowance even when a particular callback does not mutate it.

Prepared pattern frames fund capture/working slots with scalar headroom; lowered
consumers use temporary admission. Only dynamic mapping keys need a private seen
set, released after synchronous extraction. Captured
values keep their independent payload owners and surviving binding slots. Rest
mappings stay charged on publication; a failed unpublished copy is refunded.
Pattern key/property hooks require synchronous completion; guards and bodies
retain asynchronous host support.

Copy operations scope hash/equality mutations to their active caller after awaited
hooks, and pass that caller to memo lookups. Thread-local provenance from another
execution cannot select the allowance. Shallow native copies preserve stored
hashes and key identities without calling those protocols again.

Lists own their backing array, count and retained-capacity accounting directly.
Known sizes receive that capacity; empty lists start without an element array.
Growth admits the complete replacement while the old array remains funded;
growth from empty starts small, then uses one geometric rule. It can require
headroom beyond the immediate count, so admission thresholds can change.
Construction failures refund private backing, while failed live extension keeps
its successful prefix charged. Clearing and nonpositive in-place repetition drop
backing before refunding capacity, retain metadata and independent value/alias
owners, and need no fresh allowance. An already refused command stays terminal.
Sorting keeps the detached backing funded through
callbacks and awaited cleanup, then restores it before releasing callback storage.
Its private empty marker preserves mutation-then-clear detection without retaining
an unnecessary replacement array.
In-place repetition fills the admitted array in bounded, cancellation-checked copies.

Immutable strings retain their independent UTF-8 backing. Their optional scalar
offset table admits its capacity before allocation and refunds the reservation
if construction fails. The retained table describes its own charge; no separate
cache-charge accumulator is maintained. String truth and text-file EOF checks
use the logical byte extent; they do not count scalars. `readlines()` counts
characters only for a positive hint. Lengths and indexed positions retain Python
scalar semantics. Search and prefix/suffix operations prepare one borrowed UTF-8
window. Whole-string windows use the byte extent directly; explicit bounds and
returned indexes retain scalar coordinates. Tuple affixes reuse the prepared
window, preserving validation and early-match behavior.

String character and UTF-16 width counts share a bounded UTF-8 traversal. Native
`len` and public projection supply the current cancellation token; cached strings
retain no caller token. Projection still admits the full CLR UTF-16 width before
allocation, including supplementary characters. Final CLR decoding is synchronous.

CSV parsing searches bounded UTF-8 spans for structural byte candidates and
verifies complete dialect characters at each boundary. Native text files append
a complete physical line directly into the parser's admitted buffer, which the
same parser compacts in place. Public file iteration still returns independent
strings. Raw line bytes and an unfinished multiline prefix can require more
working capacity than the decoded fields; replacement peaks remain admitted.
Each source item completes at most one record, with trailing CR/LF allowed.
One independently funded final field array awaits consumption. DictReader
transfers it to matching native-key records; otherwise it relinquishes private
array capacity after header/key protocols and awaited cleanup. Escaping
dictionaries/rest lists retain their storage funding.
csv.reader and exposed fieldnames adopt the funded array without copying.
Source and parse errors consume the failed item and discard its unfinished
record; already returned rows remain valid and funded.
Non-strict quoted EOF produces a final row before source/scratch cleanup; its
independent storage stays funded through cleanup and later guest use. Strict
quoted EOF fails without publishing that row.

Dictionaries and sets share one native entry authority: stored Python hashes,
insertion identities and bucket links live with their keys. Dictionaries preserve
insertion order; sets can move the last payload when deleting. Copies retain the
funded capacity and copy entries without rehashing keys. Mutable classes remain
contextual even if their hash/equality slots initially use object defaults. Current
entry checks distinguish a removed/reinserted key during callbacks; set insertion
also preserves positive comparisons whose callback removes its candidate.

Capacity estimates include metadata and scalar headroom, with one reference column
for sets and two for dictionaries. Removal keeps that capacity funded; set clear
drops backing before refunding it and retains a charged shell. Growth admits the
replacement while the old array is funded. Larger payloads keep independent owners.
Known set operands are borrowed during bulk operations; private materialized operands
release their backing after use. `set(existing_set)` still makes a distinct copy.

Namespace and environment views keep their live values. Their existing storage
owners supply stored-key hash metadata for contextual queries; no parallel protocol
inventory is maintained. Raw scans remain separate from mutation-checked iteration.
Private construction failure refunds only unpublished backing; live views cannot
use that rollback. Conservative storage estimates can change admission thresholds.

`str.format` and `format_map` scan field structure and nested specifications as
borrowed UTF-8. Names, suffixes and the final specification decode only at the
existing resolver/formatter boundary. Temporary metadata and nested working
capacity borrow the active execution allowance through suspended callbacks;
compiled templates retain their separate owner. Private expanded specifications
refund after use, and final strings remain funded. Structural scans and resumed
callbacks observe the current cancellation token.

Constant f-string specifications keep an immutable native layout with the compiled
program, funded by its compilation owner. The existing formatter reuses it without
per-field grammar parsing or descriptor allocation. Value-dependent checks remain
at execution; custom and date formatters receive the original raw specification.
Invalid native grammar can still reach a custom formatter. Dynamic specifications
and other formatting APIs use the same parser at their actual operation. Layouts
retain no execution token, callback state or separate cache owner.

Multi-part f-strings use their literal bytes and static widths to plan modest
initial builder storage, shared by ordinary execution and native helpers. Larger
output uses normal admitted growth. Capacity remains charged when transferred to
the result; conservative planning can retain more storage or reject a tight
allowance before a field callback runs. Refusal is terminal, without retrying a
smaller buffer. Cancellation after admission releases unpublished construction
storage before allocation; actual field reads retain their execution order.

String width and precision in f-strings, `str.format` and `format()` count Unicode
scalars directly in UTF-8. Standalone string padding shares its exact output
allocation with `ljust`, `rjust` and `center`, preserving unchanged string identity.
Multipart f-strings append native string and numeric fields into one admitted
UTF-8 output builder. Numeric padding avoids padded intermediate CLR strings;
integer digits and finite fixed/scientific float fields use admitted span storage
without intermediate CLR strings. Integer base conversion is linear in digit
count. .NET 10 uses an admitted character bridge for decimal integers; .NET 11
formats them directly into UTF-8. Scratch and final output overlap remain funded.
General/default/percentage float and Decimal fields retain their rounding/notation
algorithms, then encode ungrouped ASCII into the end of the final admitted output
window. They share the same sign, grouping and padding writer as native byte digits,
without a separate grouped/padded CLR string or encoding buffer. Grouping includes
sign-aware zero fill, including narrow fields with no separator; adding a separator
can make output one scalar wider than requested. Nonempty complex formatting uses
a private governed UTF-8 body, kept funded through final padding/copy and released
on unwind. That temporary overlap can require additional admission headroom.
Managed radix, grouping and padding loops check cancellation; opaque BCL conversion
and encoding is synchronous and cannot interrupt midway. Date renderers and
user-defined `__format__` results retain their supported algorithms and callbacks. Private output stays
funded across suspended callbacks and generator yields, and releases on unwind
or close. Capacity admission can reject before a later field is evaluated.

Consuming UTF-8 builders transfer their backing capacity into the immutable string,
whose content uses only the written prefix. Unused capacity stays charged to the
result owner; finalization admits new string metadata without duplicating payload
funding. This avoids a final copy but can retain more capacity than an exact-sized
copy. Builders detach before reuse. Nonconsuming snapshots remain independent
copies and admit their real working/output overlap; byte-array results stay exact-sized.

Whitespace and explicit-separator splitting collect UTF-8 ranges, then admit the complete string
output and final list capacity together. It fills and adopts the final element
array, as do fixed-count executable list displays, without an intermediate list
copy or per-element growth. Reverse splitting fills that same array in final order.
An unchanged whole string may retain its existing owner; a different output owner
receives independent storage. Larger range scratch is admitted separately and
refunded after materialization; result strings and aliases keep their batch owner.
This can reject earlier than incremental fragment admission. Scans and bounded
copies observe cancellation; an individual CLR allocation cannot be interrupted.

Native comparison, truth and Boolean bitwise results reuse immutable CLR Boolean
boxes. Custom comparison methods still return their original objects. Boolean
`id()` values follow Python singleton identity across literals, module results and
host inputs, consistently with `is`. Boolean identities remain distinct from equal
integer values.

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

`math.prod` keeps admitted multiplication workspace until its result takes
ownership, then returns only unused scratch. Integer results of at most 64 bits
use the existing minor-scalar headroom. Cancellation after substantial admission
stops computation before the CLR operation starts. Empty integer products retain
their supplied start, including bool; float accumulation returns a fresh float.

Integer text conversion scans its original UTF-8 with an unsigned 128-bit native
accumulator before arbitrary BigInteger growth. Values above 64 bits still admit
their retained magnitude before conversion, including values that fit the native
accumulator. Promotion reserves overlapping working magnitudes. Only the retained
result is committed; scratch is refunded after
completion or failure. Scans and arithmetic chunks observe the current token.
String digits use the existing Unicode decimal classification; byte digits use
ASCII syntax. No decoded CLR text copy is needed, so admission thresholds can
change while retained storage remains funded. Native overflow guards use one
precomputed quotient/remainder; no per-digit division or decoded text is needed.

Dictionary updates
drain unknown-length pairs without storing their contents, preserving iterator
effects and exact length errors; infinite pairs require cancellation. `defaultdict`
uses the same parser for general pair sources. Native mapping copies preserve
stored hashes and key identities, without invoking custom hash methods again.
Mixed native/custom-key operations preserve an equal existing key and invoke
earlier same-hash custom keys before selecting a native hit. Callback mutation
is revalidated, and errors remain observable before mutation. Candidate snapshots
borrow the existing allowance through asynchronous equality and cleanup; native-only
tables retain direct lookup. Snapshot headroom can reject tighter allowances
earlier. XML parsing
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

Pending allocation estimates are included before invoking a host resize, so
same-execution callbacks cannot refund an outer allocation still being constructed.
Callbacks that allocate in that execution must run after the changed estimate is
visible through `ILythonMemoryReservation.Bytes`; earlier re-entry fails the command
with terminal memory refusal. Independent executions can use separate reservations
against the shared root. Failed admission or callback failure returns only the
unallocated pending charge; retained callback results keep their funding. Native
refund/cleanup remains available, and callback suppression cannot erase first refusal.

`enumerate` admits a wide next-index magnitude before incrementing it. Its tuples
fund their exposed old indices while the iterator retains the funded next index.
Source pulling precedes index construction; exhausted pulls need no unused
increment. Both execution APIs preserve this order, including suspended sources.

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
admits working capacity and any final-copy overlap. String padding reserves its
exact final UTF-8 array and metadata before allocation, keeping the input funded
through construction; it needs no working builder or second result copy.
Byte joining keeps its admitted
input storage until the result is assembled; RunAsync awaits input iteration.
Byte and string tab expansion check cancellation between bounded output chunks;
casing, replacement, translation and hexadecimal rendering also check while scanning.
Casing checks cancellation after funding its final UTF-8 output. A cancelled,
unpublished result is refunded; retained inputs and aliases keep their funding.
Native split results and ordinary CSV field payloads stay reserved until their
complete result is ready, then transfer funding together. Cancellation during
construction refunds those private fragments without waiting for execution cleanup.
Earlier numeric conversions retain their existing execution ownership.
Byte searches use managed bulk operations over bounded candidate ranges; owned
predicate and splitting loops also check cancellation while scanning. Individual
BCL calls remain atomic, so cancellation provides no hard deadline. Explicit-separator
`str.split` and `str.rsplit` use the same bounded candidate searches as bytes.
Whitespace splitting checks during forward/reverse rune scans, including long words
and whitespace-only input. Its active context follows a suspended split-count
protocol; checks do not depend on thread-local state surviving an await.
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
Plain JSON strings use the native scan's validated boundary/control facts and an
independent admitted UTF-8 copy, without a second token parse or decoding scratch.
Property names can reuse immutable UTF-8 keys from the previous completed native
object, independently of field order. The native dictionary key table supplies
this lookup; names own their bytes and never borrow input. Ordered matching
records share immutable key/hash/order metadata with independently admitted values.
When an ordered record prefix outgrows its value backing, it reserves the
retained layout's full value capacity, avoiding repeated replacement arrays.
Shorter prefixes can keep their initial storage. A narrower record that does grow
can retain spare hinted capacity or be refused earlier; that capacity stays funded.
Refusal never retries a smaller allocation. Changed shapes retain ordinary
admitted dictionary growth.
Mutable records remain independent. Equal decoded strings have no interning or
cross-decode identity guarantee; duplicate dictionary entries retain their first
key and last value. Value strings use the ordinary decoder.
Layouts and key payloads remain charged to the execution/result owner through
nested, reentrant and suspended callbacks. Different order, duplicates and changed
fields retain ordinary dictionary behavior. New metadata is admitted before
allocation. Replaced layout
metadata stays conservatively charged until the bounded execution owner ends,
so changing schemas can reject earlier. There is no separate cache allowance,
admission fallback or collector-dependent refund.
JSON strings validate original bytes and escapes, measure decoded storage before
allocation, then fill the admitted final UTF-8 string. Lenient controls and valid
surrogate pairs follow the same decoder. String errors report Python messages and
original scalar coordinates. Unpaired surrogates raise `ValueError` because
surrogate-valued strings are unsupported.
Scanning, filling and error-coordinate walks check cancellation across bounded input windows, including
escapes that advance several bytes. Bulk UTF-8 validity checks remain atomic.
Input aliases and results stay funded across callbacks and source reuse.

Callback-free JSON integers use a bounded, non-allocating signed-64-bit parse
before reserving large-magnitude working storage. Small results use the existing
scalar/container headroom. Larger integers retain magnitude and text-workspace
preflights, exact parsing and retained-payload funding; there is no digit ceiling.
Parse hooks still receive independently funded text before invocation.
Default floating tokens use native UTF-8 conversion after the prefix lexer has
validated their grammar, preserving binary64 rounding and signed zero. Conversion
is atomic; token scanning and surrounding operations check cooperative cancellation.

Arrays, dictionaries and pairs-hook output share one iterative construction loop.
Decoding has no separate document-depth ceiling: nested containers use admitted
frame capacity rather than CLR recursion. Encoding, guest callbacks and public
projection retain their own managed nesting checks.
One managed decoding admission remains live through parsing and awaited hooks;
continuations check the current managed stack before resuming. Calls near the
existing managed nesting bound can reject before parsing begins. Deeper frame
backing is admitted before allocation, including replacement overlap, and stays
funded through pending hooks. Input, keys and containers retain their execution
ownership throughout.
Final containers retain their ordinary storage and admission; callbacks use the
same awaited path, including reentrant parsing and preserved property-name snapshots.
Default objects admit their shell and first native table together when the initial
property token is a quote, starting one four-entry block for the unknown width.
Empty objects and invalid initial property tokens use
shell-only storage; a quoted but truncated key can still admit the table before
its syntax error. This conservative preflight can refuse before the first key or
number callback. Refusal remains terminal, with ordinary awaited cleanup.

JSON encoders write UTF-8 directly into governed capacity, including string
escaping, keys, separators and indentation. Replacement buffers and the final
independent result are admitted while earlier storage remains funded. Eager and
incremental encoding share one iterative traversal. Eager encoding writes directly
into its final builder and holds temporary traversal funding through pending
callbacks; native completed operations do not create per-step async continuations.
Module calls consume already-bound options directly for the built-in encoder,
sharing initialization with explicit encoder objects. Guest classes retain their
constructor and method dispatch; native eager calls retain the managed call guard.
Incremental
encoding reuses one admitted working buffer; each yielded string is an independent
funded copy. Exhaustion or failure releases scratch; abandoned iterators retain it
until their enclosing owner ends. Keeping the largest working chunk can require
more headroom between yields. Reentrant advancement raises Python's generator
ValueError while preserving the active operation. Large integer and Python float formatting still use managed invariant
text renderers. Hooks, circular-reference checks and cancellation remain active.

Native JSON and CSV file output share the executable engine's guarded write
operation, including stack admission, cancellation, type checks and error handling.
JSON resolves guest writers for each chunk; CSV captures a guest write callable
at construction. Native file handles have immutable methods and can be retained
directly. Native text output copies the producer's admitted UTF-8 range into file
staging before its scratch can be reused, avoiding an intermediate Python string.
Public/custom chunks remain independent values. One codec/newline/BOM algorithm
serves both routes; an encoding error owns its input, and ordinary writes preserve
the original string identity. A native consumer error leaves an escaped encoder
resumable. Output remains staged and funded through existing flush/close cleanup.

Any refused admission fails the command, including cache admission. Guest code
cannot catch, suppress or return over it. There is no retry, waiting, algorithm
fallback or optional-storage recovery. Guest-raised `MemoryError` stays catchable.
Compilation refusal gives diagnostic `LA0008`. Native cleanup/refunds remain
available, and later commands can reuse returned allowance.
Refusals during regex constant analysis retain the provider's explanation,
the failing regex operation and the available Python source span in that diagnostic.
The same diagnostic is returned by compilation and compile-to-result APIs.

### Ownership lifetimes

| Owner | When storage can be released |
| --- | --- |
| Compiled program | On disposal after all runs/diagnostic aliases end; includes code, metadata and retained compilation caches |
| Execution/batch | After execution, pending host/protocol users and cleanup end |
| Scratch | When construction/operation relinquishes it, including failure/cancellation |
| Retained result | On disposal after consumers relinquish projected values/aliases |
| Cache or pool | At its owner's end or ordinary funded eviction, after users finish |

`sum` keeps eligible signed 64-bit and compensated-double prefixes in scalar
fields, materializing numbers at result or general-operation transitions. Larger
integers use the existing exact arithmetic and admission preflights. Iterator
and callback cleanup, cancellation and the supplied empty-start value are retained.

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

Instance and class namespace views share their actual attribute storage. Each class
`__dict__`/`vars` access admits a fresh read-only proxy over the shared backing;
instance `__dict__` retains its identity. Repeated proxies remain conservatively
charged until their execution/result owner ends. Instance attribute deletion keeps
reusable table capacity funded. Exposed key labels are
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
Operand retirement and awaited cleanup define ownership. Deleting a guest
reference does not promise immediate CLR collection or a particular JIT lifetime.

Text casing shares one governed UTF-8 mapping pipeline. Only capital Sigma
requires original-subject context inspection; title keeps its separate word rule.
A byte scan sizes conservative final capacity before the single Unicode mapping
pass, which uses static UTF-8 expansions. Each operation admits that capacity and
metadata before allocation, without a mutable casing builder. ASCII keeps exact
capacity and uses managed bulk conversion. Each non-ASCII scalar reserves room
for a scalar mapping or the largest full expansion; unused capacity remains
funded by the result and can cause earlier refusal. Both scans check cancellation
between bounded input windows; contextual scans keep their own checks. Input
storage stays borrowed and funded until completion. Cancelled admission or
construction returns only private output funding and preserves input aliases.

Eligible native helper returns combining `lower()` or `upper()` with a default
whitespace `split()` or `rsplit()` write independently owned words directly. One forward
word-range planner handles ordinary splitting and conservative case-output
capacity. Forward/reverse discovery use managed bulk searches over fixed ASCII
inventories derived from the same whitespace classifier, decoding
Unicode scalars at non-ASCII boundaries. The shared casing writer preserves Unicode expansions
and final-sigma context. Final word capacities are admitted together;
removing the unobservable cased line can change admission amounts, order and failure
spans. First refusal remains terminal. Custom protocols keep awaited execution.

Immediate iteration of an exact cased-split chain, an eligible closed helper or
a directly returned chain through ordinary executable helpers uses a private
native cursor in the existing engine. Direct returns inherit the caller's use
through ordinary call frames and suspension; a result stored in a guest local
keeps its normal Python representation. This replaces the
transient list and value array, sharing the forward scanner and casing writer.
All future word capacities and cursor metadata are admitted before any loop-body
effects. Each yielded word owns its bytes; the cursor retains its immutable input
through iteration and suspension. Unused admitted capacity remains charged until
execution ownership ends. Assigned, exposed and escaping split results keep their
ordinary mutable lists. Admission estimates and failure boundaries can differ.
Only a validated native fusion takes the final split call's consumption intent;
custom intermediate methods retain their own result use and Python error types.

Fixed-capacity UTF-8 copies, casing and padding reserve their final capacity and
metadata directly through the governor. Private construction refunds that balance
on failure; successful strings retain the full backing charge. Copies into a
different owner keep the source funded independently. Growing, shared and
asynchronous working storage retains its scoped reservations.

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
terminator. Readers consume borrowed UTF-8 lines directly and stage decoded record
bytes and field boundaries, without whole-line decoding or field re-encoding.
Text-field storage is admitted as a batch before constructing the final row; each
returned string owns a copy before scratch is reused. Non-numeric quoting converts
unquoted numbers at the field boundary, preserving error and source-consumption
order. Record capacity and replacement overlap stay funded. A wide record can need
more working capacity than its largest individual field, causing earlier admission
refusal; allow headroom for the record buffer alongside its output payloads.
EOF releases working capacity while
queued and retained rows remain charged. Parsing checks cancellation in bounded
chunks. Readers acquire one source iterator
at construction in the selected execution mode. Dictionary readers infer headers
on fieldnames/first-next access and retain that row, or the supplied fieldnames
alias; iterator fieldnames are materialized once. Dictionary writers share the
ordinary rendering path and admit writeheader storage before allocation. Header
mutations remain visible; no duplicate string-only header arrays are maintained.
Native string-list headers construct each dictionary directly at its admitted
capacity and reuse immutable key metadata between matching rows. When header key
identities and row width match, the dictionary adopts the funded final row array
after admitting its shell and rechecking headers after admission callbacks. This
avoids a second values allocation and per-field publication. Original conservative
row-capacity funding stays with the dictionary, including capacity retained by
clear; thresholds can change. Each row owns independent values; header edits
remain visible. Short and extra-width records,
duplicate headers or a colliding
restkey can leave unused capacity; it remains charged until ownership ends and
can cause earlier conservative refusal. Dynamic headers retain Python protocols.

Path common-prefix comparison scans immutable UTF-8 at whole-character boundaries.
`os.fsdecode` decodes bytes as strict UTF-8, preserving newlines and a leading BOM.
Malformed filesystem bytes raise `UnicodeDecodeError`; surrogateescape paths are unsupported.
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

Dictionary tables keep insertion order, including deletion followed by reinsertion.
One managed array holds keys and hash-bucket heads; dictionaries use a separate
values array, which key-only sets omit. Namespace views keep their live backing.
Native updates and admitted insertion share one
hash/probe. Eligible `d[k] = d.get(k, 0) + increment` updates also combine the read,
addition and assignment. The executable engine checks actual receivers, keys,
values and funded capacity; other cases retain ordinary evaluation and admission.
Private bucket mixing leaves Python-visible hashes unchanged.
Table capacity includes conservative key/value headroom. Removal and clear retain
that funding and reuse prepaid slots; allowance returns when the owner ends.
Instance attributes use the same capacity-level policy. Growth admits replacement
overlap before allocation; unused slots and peaks can cause earlier denial. A
competing budget user may cause later growth to refuse before a new binding is
published. After refused admission, even updates within funded capacity stop.
Instance growth rechecks capacity established by admission callbacks and charges
only additional storage. If a callback fills that capacity, the pending write
fails before publication; it does not retry or grow unadmitted backing. Refusal
raised during a refund also stops the write and preserves its original attribution.
JSON and CSV records can share immutable keys through their existing batch/cache
owner. Their value capacity grows from actual fields, independently of a previous
wide layout. Value updates, deletions and clear need no metadata admission;
deletion leaves an empty value slot. Adding a key first admits private replacement
storage while the original layout and values remain funded. Protocol-key mutation
conservatively makes metadata private before guest equality, so it may refuse
earlier than an ordinary value update. Shared keys retain their funding after
individual rows clear or change, until their execution/result owner ends.
Private-table middle deletion shifts the live suffix and adjusts its hash links;
last deletion only unlinks its bucket chain. Repeated middle deletions can have quadratic cost.
Collision scans and private growth observe cooperative cancellation. Structural
payload shifts are atomic; individual CLR allocations and structural mutations
are not internally interrupted. Native copies admit
their destination capacity before allocation and copy directly without an
intermediate entry array. Unknown-length internal sources admit growth as they
are consumed. Failed private copies refund their storage; long managed copies
observe cooperative cancellation between entries.

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

Regex preparation carries the original UTF-8 subject and independently clamped
scalar/byte window bounds. Captures identify absolute byte ranges; they do not
carry a second copy of their text. Lython projects Python scalar positions using
sparse capture endpoints and an iteration checkpoint, and copies retained values from
the original subject before a cursor can advance and checks capture bounds and
UTF-8 boundaries before projection. Provider working/result borrows remain alive
through that copy and actual cleanup. Native providers need no dense coordinate
map or UTF-16 capture snapshot; the optional adapter may use such bridges while
its backend evolves. Contract/semantic validation is separate from backend
performance and released-adapter qualification.

Supply a borrowed [`ILythonRegexProvider`](../src/Lokad.Lython/Host/ILythonRegexProvider.cs)
to `LythonEngine`. One provider handles frontend validation/capture analysis,
runtime `re` and imports, and must outlive its users. Core exposes no backend
types or Utf8Regex dependency. Missing functionality fails explicitly, without
silently choosing another dialect. Native `fnmatch`/`glob` need no regex provider.

Filename matching reads the original UTF-8 inputs with scalar cursors; it creates
no whole-string rune arrays or per-character string cache. `?` and character
ranges operate on Unicode scalars. Iterative star backtracking and class scans
observe cancellation; pathological patterns can still require substantial work.
Input storage and selected-result lists keep their ordinary owners and admission.
`fnmatch.filter` drives names through the active execution API. `RunAsync` awaits
generator pulls and user `__iter__`/`__next__` protocols, including host I/O, and
finishes cursor cleanup before returning. Selected names retain their identity;
an invalid item stops consumption there without closing a borrowed guest iterator.
`fnmatch.translate` and `glob.translate` return scoped-DOTALL patterns with strict
end anchors (`\Z`). Use `match` or `fullmatch` for whole-name matching; `search`
can find a suffix, as with Python's translated patterns. Glob translation respects
recursive segments, hidden names and scalar separator characters.
Both translators emit UTF-8 directly into admitted byte storage, without decoded
input copies, segment strings or UTF-16 regex builders. Completed output takes
over that storage; capacity and growth overlap remain charged until their owner
ends. Translation itself requires no provider, while executing its regex does.

Frontend regex diagnostics use the ordinary typed bindings, including imported
aliases and known flag masks; an unrelated object named `re` has no regex policy.
Raw pattern syntax, including named escapes, belongs to the provider. Pattern
rejection during constant inference leaves the value unknown; actual execution
reports the provider's catchable `re.PatternError`. There is no separate LA3044
substring ban. Non-raw Python string/f-string named escapes remain unsupported.
Flag constants and `RegexFlag()` values are integers; enum-style `name`/`value`
properties are not implemented.

`re.error` and `re.PatternError` are the same exception type. Their constructor
accepts `msg`, optional `pattern` and `pos`, including keywords. Errors preserve
mutable `msg`, `pattern`, `pos`, `lineno` and `colno` fields in `__dict__`; changing
those fields does not rewrite `args` or the formatted message. Text positions count
Unicode scalars, bytes positions count bytes, and lines/columns follow Python's
slice bounds. Provider messages exclude location suffixes: Lython formats them
from the original pattern and optional scalar position. Unknown positions leave
line/column as `None`. Error payloads and original-pattern aliases stay funded
until their owner ends, including after a failed compilation. Managed `strptime`
errors have no generated regex pattern/position and report `None` for those fields.
The optional PythonRe0.4 adapter normalizes its UTF-16 error offsets on failure;
this bridge does not add pattern conversions to successful matching.

Providers receive borrowed UTF-8 data and shared memory owners. Immutable,
concurrent-safe metadata includes effective Python flags (inline flags and implicit
Unicode included), capture slots including zero and UTF-8 names in capture order.
Lython copies borrowed names into its own admitted storage, once per frontend
analysis program and once per runtime cached pattern; no provider name storage is
adopted. Frontend analysis uses the same effective flags. Invalid provider metadata
fails explicitly (including a compilation exception for a broken host contract);
ordinary guest pattern errors retain their Python diagnostics. Prepared subjects
provide match/search/fullmatch and finditer progression. Lython owns captured
splitting, template/callable substitution, expansion and public scalar offsets;
providers expose no replacement or split dialect.

The per-execution 512-entry pattern cache compares immutable UTF-8 content and
complete compilation options, including the separate parsing-time verbose bit.
Lookup does not decode pattern text. Equal text reuses pattern identity; eviction
and `re.purge()` release cache slots without invalidating retained patterns or
their existing storage ownership.
Purge relinquishes the cache's table capacity before refunding its funding.
Successful public compilation retains the immutable provider programs used for
constant validation under the compiled script's owner. Runs borrow exact UTF-8
pattern/option matches; they retain their own Python wrappers and group-name
strings. This avoids recompiling the same request when a freshly compiled script
runs once. Dispose the script only after all runs finish. Its retained reservation
can be larger and admission can fail earlier than with analysis-only programs.
Dynamic programs remain execution-owned until cleanup, including programs
evicted from the cache; many distinct patterns can reject early.
Runtime group names are copied once under the pattern owner and shared by named
lookup, `groupdict`, `groupindex` and `lastgroup`; capture order stays unchanged.
One UTF-8 parser handles `match.expand` and template substitution, including Python
numeric/named references, octal escapes and error precedence. Each operation parses
once into a temporary literal/capture plan, even with no matches or negative count.
The plan and lookup scratch are refunded after actual matching/users/cleanup finish;
retained output is admitted independently. Result copying checks cancellation in
bounded chunks. Replacement plans are not cached between operations; this removes
the optional adapter's private replacement-cache funding without changing compiled-
pattern caching. Split/sub consume capture byte ranges directly; callable replacements
receive ordinary Python matches and RunAsync awaits their actual callbacks.

Preparation receives the original UTF-8 subject and a `LythonRegexWindow` with
independently clamped scalar `Pos`/`EndPos` and matching byte `StartByte`/`EndByte`.
Lython supplies valid UTF-8. Pattern bytes stay unchanged throughout compilation;
subject bytes stay unchanged and funded until all subject/cursor users finish.
Providers may rely on these guarantees without another validation scan. Direct
callers of the provider interfaces must uphold the same contract. Providers never
mutate input, retain subject borrows past disposal, or retain compilation input
without copying it into their own funded storage.
Provider captures use absolute byte coordinates, including lookbehind captures before `Pos`; slicing at `Pos` would
change anchors and lookbehind. Frontend constant analysis uses the same window;
unknown bounds remain uncertain. Substitution rewrites the selected window and
preserves the original prefix/suffix; splitting returns the window's parts.
Both extensions preserve original assertion context. Reversed
substitution bounds raise `ValueError` before invoking replacement callbacks.

`ILythonRegexSubject.TryMatch` and `ILythonRegexCursor.TryNext` fill caller-owned
capture spans with exactly `CaptureSlotCount` entries, including slot zero and
null for unmatched groups. They report success and the last-closed group index;
buffer contents after exhaustion or failure are unspecified. Providers finish
all writes before returning and retain no buffer reference. Lython admits one
working buffer per prepared operation and snapshots retained matches separately.

The prepared operation owns Unicode projection working storage and its scalar
cursor, seeded from the original window. One admitted coordinate array is reused
across matches and released after cursor/provider cleanup; ASCII needs no such
array. It stays charged while iteration or a replacement callback is suspended.
Projection seeks from the nearer known byte boundary for lookbehind captures.
Forward and backward scans observe current cancellation; no dense subject index
is retained. Arbitrary far captures can still require long walks.

Python matches retain immutable byte/scalar ranges and their original funded
subject through cursor advances, disposal and cache purge. Position access,
including `regs`, needs no group-text copy. Partial group strings are admitted
and copied on access; empty groups use the empty string and full-subject groups
reuse the original string. Group copies are not cached. Admission refusal can
therefore occur at group access; it remains terminal and cannot be hidden by
guest exception handling. Expansion reads the retained ranges directly.

Provider compilation, preparation, matching and enumeration receive the current
token. Lython replacement/split/result processing checks that same token. Cached
patterns save no prior token.
Entry/exit checks do not prove in-flight interruption: providers must cooperate
or report the limitation, without private timeout/work quotas or abandoned work.
Retained patterns/caches and working/replacement peaks use supplied owners.
Each successful provider `Compile` transfers a fresh disposable pattern handle.
Public compilation retains successful constant programs until script disposal;
invalid compilation, standalone frontend and import analysis close theirs after
analysis. Execution closes its own dynamic handles after active cursors, subjects
and callbacks finish, including failure/cancellation. A failed runtime wrapper
must not close a program borrowed from the compiled script.
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
| UTF-8/offsets | Matching uses scalars, including supplementary characters. Providers return absolute capture byte ranges; Lython projects scalar offsets from the original subject. |
| Windows | Original-prefix views preserve anchors/lookbehind and end bounds without a prefix copy. Python `match` with `pos > endpos` needs an unavailable backend capability and fails explicitly; reversed search/fullmatch/iteration are empty. |
| Capture metadata | Opening-order numbering and last-closed capture selection come from the backend. Global prefix flags are read from the validated original UTF-8, including verbose trivia; scoped flags stay local. Reported flags preserve guest bitwise results independently of dispatched parsing behavior. |
| Iteration/storage | Lazy cursors fund current scratch/captures, including empty-match progression. Lython template plans are private per-operation storage; retained output is funded independently. Conservative estimates may reject early and require backend/framework headroom. |

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
destination before copy. `shutil` returns the original non-directory destination
object; joined child paths get independent admitted storage before mutation.
Asynchronous `__fspath__` preparation is awaited. ZIP path protocols also await
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
nesting bounds are independent protections. Tokenization limits delimiter nesting
to 512 before recursive parsing, using its existing string/f-string scanner;
unary nesting is at most 256. Larger call-depth settings do not bypass stack safety.
There is no native stack probing, unsafe C# or P/Invoke.
