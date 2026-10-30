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

## Focused development

Put the SDK pinned in `global.json` on PATH (currently .NET 11 RC3,
`11.0.100-rc.3.26522.148`). Install a .NET 10 runtime for compatibility checks.
Both suites, the probe and the worker fixture build for `net11.0` and `net10.0`;
use .NET 11 for ordinary development and select .NET 10 explicitly at checkpoints.

Use focused Release regressions during development. Build the affected project
once, then run its class-qualified selection without overlapping builds/tests:

```powershell
dotnet build tests/Lokad.Lython.PublicApi.Tests -c Release
dotnet test tests/Lokad.Lython.PublicApi.Tests -c Release -f net11.0 --no-build --filter 'Category!=Stress&FullyQualifiedName~Lokad.Lython.PublicApi.Tests.GovernedMemoryContractTests'
```

The worker fixture builds with the public suite.
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
Complete compilation before suspension/cancellation checks.

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
- [Shared memory](Lokad.Lython.PublicApi.Tests/GovernedMemoryContractTests.cs): shared roots, cleanup/baseline reuse, retained results and terminal refusal.
- [Projection boundaries](Lokad.Lython.PublicApi.Tests/ProjectionBoundaryContractTests.cs)
  reject lossy CLR key/member collisions; [projection cancellation](Lokad.Lython.PublicApi.Tests/ProjectionCancellationTests.cs)
  covers in-flight admission/traversal, retained partial storage and reuse.
- [List sort callbacks](Lokad.Lython.PublicApi.Tests/ListSortCallbackTests.cs)
  cover CPython visibility/mutation/error goldens and real suspension; native
  [sort ownership](Lokad.Lython.Tests/ListSortOwnershipTests.cs) checks refusal
  before detachment and funding/restoration through awaited cancellation/refusal.
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
