# Testing Lython

## Assembly boundaries

- `Lokad.Lython.PublicApi.Tests` references `Lokad.Lython.csproj` normally. It
  verifies agent-facing behavior through the shipped public assembly and must
  not use runtime or frontend implementation types.
- `Lokad.Lython.Tests` is the white-box suite. It references the built core and
  adapter; `InternalsVisibleTo` grants only `Lokad.Lython.WhiteBox.Tests` internal
  access to the core. It does not compile a duplicate runtime or widen public APIs.

Behavior that can be expressed entirely through `LythonEngine`, `ILythonHost`,
and other public contracts belongs in the public-API suite. Implementation
invariants belong in the white-box suite.

## Test responsibilities

[Declaration namespaces](Lokad.Lython.PublicApi.Tests/DeclarationNamespaceTests.cs)
retain CPython-checked failed definitions, live rebinding/deletion, lazy aliases,
key iteration and argument-target snapshots under both APIs, with budget reuse.
Existing function-workspace tests also exercise declarations across separate
executable frames and retain their cancellation, refusal and ownership assertions.

[Integer conversion](Lokad.Lython.PublicApi.Tests/GeneralIntegerTextTests.cs)
uses CPython-frozen radix, promotion, separator and whitespace cases; Unicode and
bytes controls retain their distinct grammars. [Ownership/cancellation checks](Lokad.Lython.Tests/IntegerTextCancellationTests.cs)
preserve first refusal, input funding and scratch cleanup. Separate Stress coverage
cancels actual magnitude work and joins its worker before returning the allowance.

[Direct operand ownership](Lokad.Lython.Tests/DirectOperandOwnershipTests.cs)
checks three-input subscript stores through protocol rebinding and real suspension.
Cancellation or first refusal stops mutation and later effects, preserves aliases
and restores managed depths and shared funding after cleanup. Public
[operand read failures](Lokad.Lython.PublicApi.Tests/OperandReadFailureTests.cs)
retain the original failing name/span and evaluation order. Class-subscription
controls preserve the original type/key through descriptor and body suspension.

[Receiver methods](Lokad.Lython.PublicApi.Tests/ReceiverMethodSemanticsTests.cs)
check receiver snapshots, exposed metadata, default presence, invalid-call effects
and genuinely suspended regex selectors. Concurrent runs keep independent tokens
and return the shared allowance after awaited cleanup. Capture materialization
also checks terminal admission refusal through immediate and exposed methods.

[Regex scalar positions](Lokad.Lython.PublicApi.Tests/RegexScalarCursorTests.cs)
check consecutive Unicode lookbehind/lookahead, callbacks and original windows
against independent Python output. [Active projection cancellation](Lokad.Lython.Tests/RegexScalarCursorCancellationTests.cs)
keeps its subject/captures funded until the scan actually ends, preserves the
unpublished cursor on cancellation and verifies shared-budget reuse.

[Builtin root identity](Lokad.Lython.PublicApi.Tests/BuiltinRootIdentityTests.cs)
checks independently verified Python behavior for user classes named `object` and
`type`, including descriptors and original builtin aliases. Both execution APIs
return the shared budget to baseline after cleanup.

[Instruction failure routing](Lokad.Lython.Tests/InstructionFailureRoutingTests.cs)
distinguishes entry cancellation/refusal from operation errors, preserving the
first origin, active Python exception state, stopped effects and released storage.

[Completed helper ownership](Lokad.Lython.Tests/CompletedHelperCallOwnershipTests.cs)
triggers real output admission through a helper and its deferred caller. Both
execution APIs preserve input funding, stop following effects on cancellation or
first refusal, restore managed depths and return the shared budget after cleanup.

[Dynamic formatted ownership](Lokad.Lython.Tests/DynamicFormattedOwnershipTests.cs)
keeps aliases funded through native dynamic padding and real format-callback
suspension. Cancellation or first refusal prevents publication; callback cleanup
finishes before funding returns, and the allowance can be reused.

[Executable result lifetimes](Lokad.Lython.Tests/LocalAugmentedLifetimeTests.cs)
keep original operands funded across protocol rebinding and real suspension.
Native calls, compiled children and subscript callbacks leave targets unchanged
while pending; cancellation or refused admission preserves them and awaits cleanup.

[Hash queries](Lokad.Lython.PublicApi.Tests/HashQueryCompatibilityTests.cs) preserve
Python callback effects and errors, while native copies and set pop use stored
keys without rehashing. [Ownership checks](Lokad.Lython.Tests/HashQueryOwnershipTests.cs)
exercise actual refusal/cancellation inside those callbacks, unchanged containers,
first-origin propagation and reusable shared funding after cleanup.
[Native hash cancellation](Lokad.Lython.Tests/NativeHashCancellationTests.cs) interrupts
actual string/bytes scans before cache publication, keeps current caller tokens
independent, preserves refusal priority and awaits users before returning funding.
The same checks refuse set-entry metadata before publication. Reentrant set cases
distinguish positive insertion equality from a current lookup/removal match and
keep the stored-hash relation controls, including mutated dataclass fields.
[Record compatibility](Lokad.Lython.PublicApi.Tests/RecordKeyCompatibilityTests.cs)
compares JSON/CSV mutation, key order/identity, protocols, hooks and header edits
with CPython. [Ownership checks](Lokad.Lython.Tests/RecordKeyOwnershipTests.cs)
preserve independent values, retained key funding, refusal before insertion,
cancelled replacement, admission-driven mutation and reuse after cleanup. A wide
prior layout cannot force wide value storage for the next small record. Async
hash/read controls also mutate shared records during genuinely suspended equality.
[Nested error goldens](Lokad.Lython.PublicApi.Tests/UnhashableKeyOriginTests.cs)
identify the failing value through slices, tuples, dataclasses and cache keys.
Tuple queries also preserve earlier hash effects and errors before a later
unhashable element. Refusal or cancellation in that earlier callback remains
terminal, preserves the container and releases working funding after cleanup.
Mixed hash/equality callbacks keep single-object and pair cycles independent.
Async hash/read controls await actual descriptor/body/field I/O and revalidate
entries after suspended equality clears the table. Pending snapshots and aliases
stay funded through cancellation/refusal, finally-return cannot hide the first
failure, and cleanup permits shared-budget reuse. Managed depth/cycle checks also
run after a real thread-pool continuation and recover for the next hash.
Completed CSV record controls cover independently mutable values at exact row
capacity, copy/new-key growth and clear retaining its backing charge. Admission
callbacks change header identities/width or cancel/refuse before transfer; private
row funding remains intact and the shared allowance returns after owner cleanup.
Dictionary read-prefix controls insert a custom key and rebind the source during
key/default evaluation. Direct/exposed get and subscript must retain the selected
receiver, await equality and propagate cancellation with shared-budget cleanup.
Builtin class-pattern goldens preserve whole-subject identity, nested sequences,
actual aliases, shadowed class names, excess-position errors and lowered class
bodies. Both execution APIs return shared funding to baseline after cleanup.
Class extraction goldens cover descriptor metadata, requested-name validation,
duplicate/error order, mutation snapshots and contextual equality/truth.
Cancellation/refusal cannot become an AttributeError mismatch; scratch is
admitted before effects and extracted aliases stay funded through a suspended body.
Constructor goldens cover allocator/initializer order, returned subtype binding,
static/classmethod and descriptor initializers, defaults and variadics. Real
allocator/descriptor/initializer I/O retains arguments after source rebinding;
successful escaping results stay charged. Cancellation/refusal stops later hooks,
awaits cleanup and restores shared funding. Native hooks cannot hide first failure
by returning an otherwise valid object or None.
Admission-hook mutation also verifies that construction initializes the single
existing instance when the new initializer needs the general binder; it must not
restart allocation or call a newly installed allocator.
Structural controls retain immediate reversed-pair rejection, cross-thread
state disposal and out-of-order unwind without retaining active identities.
[Nested hash protocols](Lokad.Lython.PublicApi.Tests/NestedHashProtocolTests.cs)
preserve bound ordering, defining dataclass fields and all reads before field
hashes. Managed slice-depth checks run after real host suspension and recover on
the next run. [Ownership regressions](Lokad.Lython.Tests/NestedHashOwnershipTests.cs)
deny field snapshots before reads and stop later guest callbacks after caught
refusal/cancellation, with shared-budget recovery and traversal cleanup.

| Contract | Primary coverage |
| --- | --- |
| Language/module values, types and errors | Public goldens and semantic families; syntax cases run once through each API |
| Frontend rejection and diagnostics | Public compilation/corpus controls; white-box analysis invariants where needed |
| Native admission, capacity, rollback and internal invariants | White-box tests against the real assembly |
| Host protocols, retained aliases and resource lifetimes | Public host/ownership families; shared mock and gated hosts |
| Real suspension, cancellation and terminal refusal | Focused gated operations with awaited cleanup, separate from ordinary semantics |
| Compiled-code reuse and isolation | Small syntax reuse set plus function/builtin isolation and regex-cache token controls |
| Large scale and process-wide GC/finalizer behavior | Original Stress cases and existing nonparallel collections |
| Installed package assets and public embedding | Isolated core consumers on both actual runtimes |

Reuse the existing case/host fixtures. Add API/delay/budget combinations when their
interaction can fail independently, not as a blanket product for every semantic case.

[Function workspace ownership](Lokad.Lython.Tests/FunctionWorkspaceOwnershipTests.cs)
checks funded idle reuse, distinct active and shared-root execution buffers, first
refusal and explicit cleanup. Repeated-call controls assert stable retained
capacity, then exact recovery when cache ownership ends. Retained inputs, kwargs,
closures and suspended users keep their independent funding throughout.

[Completed invocation lifetimes](Lokad.Lython.Tests/CompletedInvocationLifetimeTests.cs)
keep an escaped closure usable while checking that relinquished workspace becomes
collectible, and verify retained-context headroom against actual managed shell
construction. These checks share the existing nonparallel allocation collection.
Active frame inspections are borrows: after completion, operand checks require
detached storage and bindings. Surviving namespace observations preserve the
independent unchanged-target checks after cancellation or admission refusal.

[Deferred helper contexts](Lokad.Lython.PublicApi.Tests/DeferredFunctionContextTests.cs)
preserve modified locals, module globals, prior writes, late protocols/slices and
function traces when native work reaches a scope boundary. Real I/O keeps aliases
funded through cancellation and awaited cleanup. The
[admission controls](Lokad.Lython.Tests/DeferredFunctionOwnershipTests.cs) verify
that capacity growth sees current function locals, refusal leaves the target
unchanged and earlier writes are never replayed.

[Ordinary method calls](Lokad.Lython.PublicApi.Tests/OrdinaryMethodCallTests.cs)
preserve selected receivers and functions through argument-side rebinding, custom
lookup and descriptors. Pending argument reads cancel with awaited cleanup and
shared-budget reuse; refused admission prevents invocation even when guest cleanup
returns. Exposed method values keep their Python identity and metadata behavior.

## Focused development

Put the SDK pinned in `global.json` on PATH (currently .NET 11 RC3,
`11.0.100-rc.3.26522.148`). Install a .NET 10 runtime for compatibility checks.
Both suites, the probe and the worker fixture build for `net11.0` and `net10.0`;
use .NET 11 for ordinary development and select .NET 10 explicitly at checkpoints.

If sources were restored from timestamp-preserving copies or snapshots, first
use `dotnet build Lokad.Lython.slnx -c Release --no-incremental`. An ordinary
incremental build can reuse a previous prototype's output. Freeze comparison
workers only after this rebuild and verify that their core binary matches the
newly built core for the selected target.

Use focused Release regressions during development. Build the affected project
once, then run its class-qualified selection without overlapping builds/tests:

```powershell
dotnet build tests/Lokad.Lython.PublicApi.Tests -c Release
dotnet test tests/Lokad.Lython.PublicApi.Tests -c Release -f net11.0 --no-build --filter 'Category!=Stress&FullyQualifiedName~Lokad.Lython.PublicApi.Tests.GovernedMemoryContractTests'
```

The worker fixture builds with the public suite.
Validated regex programs stay funded across sequential/concurrent executions and
cache purge, independently of the compilation token. Cooperating-provider tests
hold matching cleanup after cancellation, refuse wrapper admission and replay
under the same allowance. Real-adapter controls retain Unicode group dictionaries
after script disposal; their result owners remain funded until release.

Regex adapter cancellation runs in an owned fixture process so a broken backend
cannot leave matching work running in the test host. Its semantic goldens use
frozen CPython output; cooperating-provider forwarding controls remain separate.
Pattern-lifetime controls cover frontend analysis/imports, unpublished cancellation,
publication refusal, retained programs across purge/eviction, suspended users and
separately owned provider caches. Direct adapter controls dispose patterns before
their borrowed parent and verify funding is reusable without CLR collection.
Window controls use independent CPython anchor/lookbehind/capture/default goldens
and a cooperating provider for original-subject forwarding, absolute captures and
the reversed zero-width match that the pinned adapter cannot yet express. Real
adapter capability rejection is checked separately from core semantics.
Matching-only provider controls exercise Lython's shared native split/sub/expand
pipeline without backend template or split APIs. They include UTF-8 names,
unmatched captures, cancellation during capture processing and retained-result
funding. Private per-operation plans and name lookups return their working
allowance without collection; retained output is admitted independently.
Frontend binding controls distinguish actual regex calls from unrelated receivers,
preserve imported flag aliases/masks and let the provider own raw pattern syntax.
[Independent goldens and a narrow cooperating provider](Lokad.Lython.PublicApi.Tests/RegexAnalysisBindingTests.cs)
keep that evidence separate from backend capability qualification.
Formatter controls cover UTF-8 field/spec structure, Unicode conversion diagnostics,
active execution admission for compiled literals, cancelled pending lookups and
reuse of private nested-spec storage without CLR collection. Both invocation APIs
retain the existing protocol/effect and genuinely suspended host checks.
Directory/mutation controls additionally preserve recursive prefix effects,
relative traversal results, package search roots and actual asynchronous path
protocols. Refused copy keeps an existing destination intact without delete/replay.
Metadata controls verify original dot segments, empty paths, trailing slash and
explicit contained Cwd against the shared host resolver. They distinguish false
predicates from metadata errors without swallowing refusal, cancellation or other
host failures. Owned byte-file controls verify stable identities after rename/replacement,
independent cursors, unresolved path intent, short reads and accepted-prefix writes.
Gated acquisition/read/close cases retain funding through actual completion and
uncancelled disposal; refusal remains terminal before create/truncate effects.
Text cases preserve codec/newline windows, BOMs after retry, partial encoded
prefixes, accepted-data retirement after backend flush failure and Python-shaped
direction/closed-context errors. Decoder tests use actual opened byte identities. Whole-file
helper cases cover progressive admission, retained returned bytes, text encodings
and EOF at capacity boundaries. Archive traffic checks total contents and bounded
windows instead of requiring the retired single whole-file host transfer. Writable ZIP controls additionally cover initial
create/truncate effects, allowed rename/replacement, same-identity append rewrite,
execution-end publication, missing rewrite capability, failed-prefix non-replay,
refusal before mutation and late cancellation borrows.

Class subscription and native type checks use independent CPython goldens.
Focused class lifetime cases cover delayed descriptor/body work, cancellation,
terminal refusal and retained results; resumed recursive subscription also runs
inside the existing stack-guard fixture process. Its witnesses trigger one
default-depth rejection, then make three real calls through the same protocol
to verify guest-call admission recovered. The 25-second process bound and
original stress scales remain unchanged. These deadline-sensitive owned stack
probes share the existing quiet RuntimeStress collection with resource-sensitive
checks; ordinary semantic collections still run in parallel. Timeout failures
record child CPU time before killing/reaping it, without a new timing policy.

Ordinary tests exclude `Category=Stress`. An explicit filter replaces the default;
include `Category!=Stress` for broad selections. Keep minimal ordinary fixtures
and original longer stress scales. Never relax timeouts, containment assertions
or semantics for faster passes. Process-wide GC/finalizer tests share the existing
nonparallel collections: white-box `AllocationSensitive`, public `RuntimeStress`.
White-box call-transport fixtures also use `AllocationSensitive`: their clearing
assertions inspect returned thread-local buffers and must exclude a concurrent
borrower that can legitimately overwrite them before the assertion resumes.
Complete compilation before suspension/cancellation checks. False synchronous
promise controls release their producer only after the returned ValueTask's
pending status is observed; callback-entry signals alone permit completion races.
All real work and cleanup remain joined, without sleeps or runtime test hooks.
Startup-worker controls hold host I/O pending and observe actual release/retirement;
callback entry and CLR wait states do not establish idle-cache membership. Separate
completed-async calls check sequential reuse. Caller/suspended contexts, cancellation,
nested calls and awaited cleanup retain their independent assertions.

Writer-publication cancellation uses finite guest programs and cancels from a
real host callback after buffering. Pure-loop interruption remains in the quiet
RuntimeStress collection, with independently started callers and joined cleanup.

Qualify allocation traffic in optimized Release builds; Debug async state machines
have different allocation costs. Keep semantic, admission, balance and cleanup
assertions active in both. Per-thread counters must measure the actual operation
on that thread; verify synchronous completion for CPU-only async fixtures. Process
counters also include test-host/background work and require separate controls.

## Delivery checks

At a coherent runtime, dependency or packaging checkpoint:

```powershell
dotnet build Lokad.Lython.slnx -c Release
dotnet test Lokad.Lython.slnx -c Release -f net11.0 --no-build
dotnet test Lokad.Lython.slnx -c Release -f net10.0 --no-build
dotnet test Lokad.Lython.slnx -c Release -f net11.0 --no-build --filter 'Category=Stress'
dotnet test Lokad.Lython.slnx -c Release -f net10.0 --no-build --filter 'Category=Stress'
./tools/PackageSmokeTest/verify-package.ps1
```

CI builds both frameworks on Windows/Ubuntu and ordinarily tests .NET 11.
Dispatch `ci` to add the .NET 10 ordinary suite; `stress=true` adds original
stress scales on both frameworks. The package gate explicitly packs Release,
checks both runtime assets/symbols, identity and dependencies, and restores
isolated core-package consumers under each actual runtime. The optional
Utf8Regex adapter is non-packable and tested through its project reference in
the public-API suite. Debug output is not a release artifact.
Core packages and symbols go to `artifacts/nuget/`;
`./tools/pack.ps1` generates them without running the consumer checks.
Keep GitHub queries minutes apart; cache terminal proof
instead of requerying historical green runs.

## Contract coverage

- [Regex errors](Lokad.Lython.PublicApi.Tests/RegexPatternErrorTests.cs) use isolated
  CPython constructor, field, protocol and adapter/template-error goldens.
  [Error lifetimes](Lokad.Lython.PublicApi.Tests/RegexPatternErrorLifetimeTests.cs)
  verify scalar/unknown positions, actual provider-user cleanup, held asynchronous
  rendering, terminal cancellation/refusal, retained results and shared-budget reuse.

- [Native regex cache](Lokad.Lython.PublicApi.Tests/NativeRegexCacheTests.cs):
  equal UTF-8 content, Unicode/NUL, parsing-time options, retained patterns after
  purge and shared-budget reuse; native capacity tests preserve LRU/refusal controls.
  [Expansion ownership](Lokad.Lython.Tests/RegexExpansionOwnershipTests.cs) checks
  native cancellation, growth/copy overlap refusal and retained output; the
  [semantic corpus](Lokad.Lython.PublicApi.Tests/Fixtures/RegexAdapter/cases.json)
  keeps independent CPython escape/error goldens and public suspended execution.

- [Structured host failures](Lokad.Lython.PublicApi.Tests/StructuredHostFailureTests.cs):
  safe Python categories across file/stream/clock/process work, launch versus exit,
  terminal nested refusal, falsely synchronous settlement and budget reuse.
- [Subprocess ownership](Lokad.Lython.PublicApi.Tests/SubprocessOwnershipTests.cs)
  and [buffered completion](Lokad.Lython.PublicApi.Tests/SubprocessCompletionSubsystemTests.cs)
  cover independent captured buffers, admission before inherited effects, awaited
  cancellation/downstream failure, retained pipeline borrows and returned bytes.
  Native settlement checks dispose unpublished completions from falsely synchronous hosts.

- [Executable calls](Lokad.Lython.Tests/ExecutableCallFrameTests.cs): bounded CLR
  stack growth, guest traceback/finally order and restored execution state;
  [call lifetimes](Lokad.Lython.PublicApi.Tests/ExecutableCallLifetimeTests.cs)
  cover pending I/O, cancellation, terminal refusal and funded escaping results.
- [Format mappings](Lokad.Lython.PublicApi.Tests/FormatMappingTests.cs): CPython
  lookup/error-order goldens, live dictionaries, delayed item/attribute callbacks,
  cancellation, retained results and terminal admission refusal. Formatting
  protocol tests separately cover conversion/format descriptors and cleanup.
- [Syntax corpus](Lokad.Lython.PublicApi.Tests/Fixtures/SyntaxCorpus/cases.json): independent goldens, invalid/unsupported forms and both APIs.
- [Annotation flow](Lokad.Lython.PublicApi.Tests/AnnotationFlowBoundaryTests.cs):
  later mismatches retain preceding branch and cleanup effects, nested scopes
  preserve defaults/returns, conservative joins avoid false errors, and successful
  runs return their shared allowance through both invocation APIs.
- [Shared memory](Lokad.Lython.PublicApi.Tests/GovernedMemoryContractTests.cs): shared roots, cleanup/baseline reuse, retained results and terminal refusal.
- [Projection boundaries](Lokad.Lython.PublicApi.Tests/ProjectionBoundaryContractTests.cs)
  reject lossy CLR key/member collisions; [projection cancellation](Lokad.Lython.PublicApi.Tests/ProjectionCancellationTests.cs)
  covers in-flight admission/traversal, retained partial storage and reuse.
- [List sort callbacks](Lokad.Lython.PublicApi.Tests/ListSortCallbackTests.cs)
  cover CPython visibility/mutation/error goldens and real suspension; native
  [sort ownership](Lokad.Lython.Tests/ListSortOwnershipTests.cs) checks refusal
  before detachment and funding/restoration through awaited cancellation/refusal.
  [List capacity ownership](Lokad.Lython.Tests/ListCapacityOwnershipTests.cs) also
  covers allocation-free clear/repetition under a full allowance, retained aliases
  and immediate budget reuse. Growth and attachment still refuse before mutation.
- [Benchmark ownership](Lokad.Lython.PublicApi.Tests/BenchmarkOwnershipTests.cs)
  verifies the actual shared benchmark helper releases results after consumption,
  including consumer and guest failures.
- [Representative scripts](Lokad.Lython.PublicApi.Tests/RepresentativeScriptTests.cs): complete CSV, text, JSON and file results.
- [JSON string storage](Lokad.Lython.Tests/JsonStringTokenStorageTests.cs) and
  [escaped strings](Lokad.Lython.PublicApi.Tests/EscapedJsonStringTests.cs) cover
  native unescaping across buffer sizes, independent copies, construction overlap,
  strictness, Unicode, callbacks and budget reuse.
- [Regex boundary](Lokad.Lython.PublicApi.Tests/RegexProviderContractTests.cs): cooperating in-flight cancellation/ownership, separate from backend qualification.
- [Text predicates](Lokad.Lython.PublicApi.Tests/TextPredicateExecutionTests.cs): Unicode and split-scalar results after real I/O, alongside internal forwarding/active-scan controls without checkpoint counting.
- [Binary contracts](Lokad.Lython.PublicApi.Tests/BinaryContractTests.cs): independent byte/codec goldens, delayed protocols, native error fields and gated encoding ownership. Native byte cancellation is checked directly without a VM exit checkpoint.
- [Argument parsing](Lokad.Lython.PublicApi.Tests/ArgparseContractTests.cs): independent parsing/metadata goldens, delayed protocols, governed formatting and pending file-open ownership. Keyword decoding has separate before-copy refusal coverage.
- [Numeric hash invariants](Lokad.Lython.PublicApi.Tests/NumericHashContractTests.cs): independent cross-domain, tuple/cache, class-slot and dataclass controls through both APIs, with shared-budget cleanup.
- [Numeric protocols](Lokad.Lython.PublicApi.Tests/NumericProtocolContractTests.cs): independent repetition/reflection controls and actual suspended descriptor/index/divmod callbacks, with separate cancellation/refusal unwind checks.
- [Numeric arithmetic](Lokad.Lython.PublicApi.Tests/NumericArithmeticContractTests.cs): independent division, power/shift edges and error ordering through expression, operator and builtin entry points, with separate before-allocation admission/scratch-return controls.
- [Numeric ingress](Lokad.Lython.PublicApi.Tests/NumericIngressContractTests.cs): all integral widths, canonical float/Decimal inputs, signed zeros, aliases and retained results; [DecimalTuple](Lokad.Lython.PublicApi.Tests/DecimalTupleContractTests.cs) covers shared tuple behavior, real async construction/indexing and admitted public projection.
- [Decimal contexts](Lokad.Lython.PublicApi.Tests/DecimalContextLifetimeTests.cs): independent identity/copy/clear controls, manager capture/reuse and execution isolation, with native refusal/alias-funding and actual callback cancellation/cleanup coverage.
- [Decimal arithmetic](Lokad.Lython.PublicApi.Tests/DecimalContextArithmeticTests.cs): independent finite precision, rounding-mode, tie, zero-sign, signal payload and fused-operation goldens through both APIs; native ownership controls cover before-mutation refusal and scratch reuse.
- [Decimal conversion](Lokad.Lython.PublicApi.Tests/DecimalConversionBoundaryTests.cs): finite representation boundaries, explicit quiet-result rejection and unchanged inputs; arithmetic goldens also cover exact float/context conversion, constructor distinctions and syntax payloads. Native refusal checks preserve live signal aliases.
- [Decimal power ownership](Lokad.Lython.Tests/DecimalPowerOwnershipTests.cs): magnitude admission/refusal, cancellation after working-storage admission and allowance reuse; public arithmetic goldens cover precision, reciprocal/unit powers, modular signs and trap payloads.
- [Decimal rounding](Lokad.Lython.PublicApi.Tests/DecimalContextArithmeticTests.cs): independent quantize, integral and builtin-round goldens cover eight modes, selected precision, signed zeros, quantum and trap payloads. Native arithmetic controls cover refused scratch before signal mutation and working allowance reuse.
- Decimal arithmetic goldens also cover context-width digit transforms and Counter sign filtering before rounding; native controls preserve source aliases and fund borrowed Counter results.
- Decimal function goldens cover finite sqrt ties/quantum, half-even rounding independence and exact logarithms; [function boundaries](Lokad.Lython.PublicApi.Tests/DecimalFunctionBoundaryTests.cs) keep unsupported transcendental and native-scale requests explicit. Math/isqrt retains its existing admission/cancellation coverage.
- [Namedtuple replacement](Lokad.Lython.PublicApi.Tests/NamedTupleReplacementTests.cs): independent Python 3.13 error/alias semantics and shared-budget reuse; native admission controls deny or cancel before the major array copy and retain source/result funding.
- [Namedtuple construction](Lokad.Lython.PublicApi.Tests/NamedTupleConstructionTests.cs): independent binding/default/iterator-tail behavior; native [admission checks](Lokad.Lython.Tests/NamedTupleAdmissionTests.cs) cover before-allocation refusal/cancellation and arity/binding rollback.
- [Spreadsheets](Lokad.Lython.PublicApi.Tests/SpreadsheetContractTests.cs): independent openpyxl 3.1.5 cell identity, blank-row and iterator goldens, awaited append callbacks, style round trips and pending-save ownership. Native [admission checks](Lokad.Lython.Tests/SpreadsheetAdmissionTests.cs) cover capacity retention, refusal before mutation/copy and cancellation after working-storage admission.
- [Subprocess exceptions](Lokad.Lython.PublicApi.Tests/SubprocessExceptionContractTests.cs): original argument shapes, live fields/aliases, native messages, delayed metadata, terminal rendering refusal and construction without proportional copies.
- [Shell lexing](Lokad.Lython.PublicApi.Tests/ShlexStreamContractTests.cs): lazy shared cursors, Unicode, source resumption, writable input, actual equality/truth suspension and cancellation; StringIO accounting covers refusal before borrowed input is copied or materialized.
- [Statistical contracts](Lokad.Lython.PublicApi.Tests/StatisticsContractTests.cs): independent exact-moment, namedtuple, distribution and random-state controls, real protocol suspension and terminal refusal. [Accuracy vectors](Lokad.Lython.PublicApi.Tests/StatisticsAccuracyTests.cs) state binary64 tolerances. Native moment/model ownership tests check admission before major work, alias funding and unpublished rollback; original 20K distribution fixtures run as Stress.
- [Diff contracts](Lokad.Lython.PublicApi.Tests/DifflibContractTests.cs): independent input/cache mutation, namedtuple, context-width, option-order and lazy-producer goldens; actual comparison/truth suspension, cancellation, terminal refusal and retained shared results. Native [ownership controls](Lokad.Lython.Tests/DifflibOwnershipTests.cs) cover admission before input/tag construction, canonical alias funding, grouped output, failed replacement rollback and thread-context isolation during suspension.
- [Copy contracts](Lokad.Lython.PublicApi.Tests/CopyContractTests.cs): independent memo/hook/order goldens, actual delayed callbacks, exposed memo/result funding and explicit unsupported registry access. Original 20K identity-collision coverage runs as Stress; ordinary controls stay small.
- [Pending synchronous host I/O](Lokad.Lython.PublicApi.Tests/SynchronousHostBorrowTests.cs): borrowed buffers stay funded until started operations settle, including partial faults/cancellation, with one publication and cleanup. Native completion checks cover generic results and rejection before starting async-only capabilities.
- [Native protocols](Lokad.Lython.PublicApi.Tests/NativeProtocolContractTests.cs): independent context-manager slot/truth/exception and random selection goldens, genuinely delayed preparation, in-flight cancellation, terminal refusal and retained-result owners. Native [selection admission](Lokad.Lython.Tests/RandomSelectionAdmissionTests.cs) checks before-copy/arithmetic denial and unchanged RNG state.
- [Time fields](Lokad.Lython.PublicApi.Tests/TimeFieldContractTests.cs): independent struct_time leap-day, leap-second, weekday/year-day and formatting results, with unchanged datetime validation.

Preserve before-mutation admission, aliases/cursors, real suspension, terminal
refusal and awaited cleanup. Reference deletion does not imply immediate refund.
Large diagnostics and performance observations belong with benchmark evidence,
not ordinary timing assertions.

The [probe](../tools/LythonProbe/README.md) runs trusted pure snippets and optional
isolated CPython comparison without I/O/process capabilities. Test-project builds
do not build it; build its project in the matching configuration before dependent
tests, or build the solution.
