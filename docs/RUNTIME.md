# Embedding and host contract

Lython runs a Python subset in fully managed C# on .NET 10. It exposes no guest
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

`sys.modules` is the execution's live import registry, containing loaded modules
rather than the builtin inventory. Aliases observe subsequent imports; deletion,
cached values and `None` blockers affect later imports. Builtin grants are checked
before consulting cached values. Repeated registry reads reuse the same storage.

## Execution internals

Compilation tokenizes/parses source, validates the subset, computes scope and
host-requirement facts, lowers constructs, and builds executable instructions.
The executable engine runs both public APIs and local imports, using local/closure
slots, operand storage and resumable generator frames. Lowered evaluation remains
for actual class-body, annotation and expression/opcode consumers; there is no
whole-program alternate backend. Imports retain canonical module namespaces.

Reusable workers provide the managed stack for the synchronous prefix. Actual
asynchronous suspension releases that worker instead of parking it through I/O.
Per-run services retain host capabilities, caches, stream capture, exception state,
cancellation and memory admission. The core's only runtime package dependency is
Lokad.Parsing; the optional regex adapter owns its backend dependency.

Exact integer math uses cooperative cancellation and shared memory admission,
without an iteration quota. Cancellation is checked between arithmetic operations;
it cannot interrupt an individual CLR `BigInteger` operation. Dictionary updates
drain unknown-length pairs without storing their contents, preserving iterator
effects and exact length errors; infinite pairs require cancellation. XML parsing
retains its 1,024-level depth protection.

`math.nextafter` computes the requested adjacent floating-point value directly,
including very large step counts, without iterating once per step.

## Results and lifetimes

[`LythonExecutionResult`](../src/Lokad.Lython/Public/LythonExecutionResult.cs) is
disposable. `State` distinguishes `SucceededState`, `CompilationFailedState` and
`RuntimeFailedState`. `Success`, `ReturnValue`, `Diagnostics`, `Failure`,
`FailureAttribution` and `ExitCode` are convenience accessors. Failures carry
Python-shaped exceptions and available source/stack information; `SystemExit`
preserves its exit-code semantics.

Top-level `return` projects CLR-friendly values, including `byte[]` for bytes.
Projection detects cycles and admits substantial storage. Stdout/stderr are
retained in `StandardOutput`/`StandardError`; `Consumption` reports accepted
UTF-8 capture sizes. Memory properties report the accounting estimates described below.

Numeric `statistics.LinearRegression` records retain their supplied field types
and project through the existing sequence boundary as two-element CLR arrays.

Consume values and relinquish aliases before disposing a result. Await all runs
and relinquish compiled diagnostic aliases before disposing a script. Disposal
does not cancel work. Borrowed budgets and providers must outlive their users.

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

Derived byte buffers, byte slices/representations and text transformations use
the active execution owner, including operations on compiled literals. Construction
admits builder capacity and final-copy overlap. Byte joining keeps its admitted
input storage until the result is assembled; RunAsync awaits input iteration.
Byte and string tab expansion check cancellation between bounded output chunks;
casing, replacement, translation and hexadecimal rendering also check while scanning.
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

Random byte and integer generation admits capacity before drawing; generation,
seed folding and shuffle loops poll cancellation. Actual managed buffer bounds
apply, without an independent bit-width quota. RunAsync awaits supported random
count/index/multiplication hooks. SequenceMatcher stores its autojunk option and
evaluates it when building the index; construction/reindexing awaits that conversion
and junk predicate results. Other difflib producer and custom sequence/key protocol
paths can still require synchronous completion.
If SequenceMatcher reindexing fails, Lython restores the previous sequence/index
and their charges. CPython can expose partially replaced state after that error.

JSON indentation has no fixed width ceiling. Encoding prepares admitted indentation
storage, which can remain charged for the execution; constructors retain the option
without materializing unused padding. Checked string-representation bounds apply.

JSON decoders evaluate a guest `strict` option once when preparing the scanner,
including asynchronous callbacks. The public attribute retains the supplied
object; replacing it later does not alter the scanner's captured setting.

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

Byte construction, hexadecimal parsing, splitting and date/time formatting admit
working capacity before allocation. Temporary conversion buffers end with the
operation; retained results remain funded by their owners. `strptime` uses a
managed directive parser with cancellation during scans and alternative selection,
without a regex provider. Its conservative input/format working estimates can
reject earlier; scratch funding returns when parsing ends. Duplicate directives
raise `re.PatternError`, including when no regex provider is supplied.

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

Compilation, preparation, matching, enumeration, replacement, splitting and
result processing receive the current token. Cached patterns save no prior token.
Entry/exit checks do not prove in-flight interruption: providers must cooperate
or report the limitation, without private timeout/work quotas or abandoned work.
Retained patterns/caches and working/replacement peaks use supplied owners.
Dispose cursors before subjects, finish users before refunds, and propagate
`MemoryAdmission` refusal without retry. Disposal is synchronous/nonthrowing;
borrowed results obey the interface's operation lifetime.

### Optional Utf8Regex adapter

`new LythonEngine(Lokad.Lython.Utf8Regex.Utf8RegexProvider.Instance)` selects the
optional `Lokad.Lython.Utf8Regex` adapter over pinned PythonRe 0.2.0. Its separate
qualification does not certify backend safety or couple core releases to it.

| Capability | Current adapter behavior |
| --- | --- |
| In-flight cancellation | Unsupported by the pinned API. Cancellable operations fail explicitly; already-cancelled tokens cancel. No background matching/private quota. |
| UTF-8/offsets | Normal captures use UTF-8/scalar ranges; one-shot supplementary Unicode uses a validated coordinate fallback. |
| Buffered supplementary Unicode | Affected multi-scalar operations fail before backend paths known not to terminate. |
| Capture metadata | Mixed named/unnamed numbering and nested last-capture selection retain backend limitations. |
| Iteration/storage | Buffered operations pre-admit captures/overlap; `.*?` has an adapter-local workaround. Large inputs may reject early. |

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

Operations receive the active token. Write buffers remain valid until their
returned operation completes; consume or copy before completion. Storage retained
afterward belongs to the host. Pending borrows stay funded through actual users
and cleanup, including cancellation.

For streaming, override `ReadTextUtf8RangeAsync` and `ReadBytesRangeAsync`: their
compatibility defaults load the whole file before slicing. Override
`AppendBytesAsync` for native append; its default reads and rewrites the prefix,
can perform quadratic work and is not atomic with concurrent writers. Bounded
runtime chunks alone cannot make these defaults streaming.

## Cancellation and protection

Arrange cancellation or `CancelAfter` for finite execution, including
non-allocating infinite loops. Checks cover instruction execution, iteration,
long native loops and host calls. Some CLR conversion/parsing operations finish
before their next checkpoint. Non-cooperating host/provider work cannot be
forcibly interrupted; see [regex limitations](#optional-utf8regex-adapter).

Cancellation and refused admission terminate the command through guest handlers
and suppression. Cleanup is awaited and funded; refusal retains its first
structured attribution. Completed effects are not transactionally rolled back.

Managed stack admission, Python recursion limits and conservative frontend
nesting bounds are independent protections. Delimiter nesting is at most 512;
unary nesting at most 256. Larger call-depth settings do not bypass stack safety.
There is no native stack probing, unsafe C# or P/Invoke.
