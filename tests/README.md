# Testing Lython

## Assembly boundaries

- `Lokad.Lython.PublicApi.Tests` references `Lokad.Lython.csproj` normally. It
  verifies agent-facing behavior through the shipped public assembly and must
  not use runtime or frontend implementation types.
- `Lokad.Lython.Tests` is the isolated white-box suite. It source-links the
  production implementation so subsystem tests can inspect internal details
  without `InternalsVisibleTo` or a wider package API.

Behavior that can be expressed entirely through `LythonEngine`, `ILythonHost`,
and other public contracts belongs in the public-API suite. Implementation
invariants belong in the white-box suite.

## Focused development

Use focused Release regressions during development. Build the affected project
once, then run its class-qualified selection without overlapping builds/tests:

```powershell
dotnet build tests/Lokad.Lython.PublicApi.Tests -c Release
dotnet test tests/Lokad.Lython.PublicApi.Tests -c Release --no-build --filter 'Category!=Stress&FullyQualifiedName~Lokad.Lython.PublicApi.Tests.GovernedMemoryContractTests'
```

The worker fixture builds with the public suite.

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
dotnet test Lokad.Lython.slnx -c Release --no-build
dotnet test Lokad.Lython.slnx -c Release --no-build --filter 'Category=Stress'
./tools/PackageSmokeTest/verify-package.ps1
```

CI runs Windows/Ubuntu. Dispatch `ci` with `stress=true` for original stress scales
on both. The package gate explicitly packs Release, checks contents, identity and
dependencies, and restores isolated core-only/adapter consumers. Debug output is
not a release artifact. Keep GitHub queries minutes apart; cache terminal proof
instead of requerying historical green runs.

## Contract coverage

- [Syntax corpus](Lokad.Lython.PublicApi.Tests/Fixtures/SyntaxCorpus/cases.json): independent goldens, invalid/unsupported forms and both APIs.
- [Shared memory](Lokad.Lython.PublicApi.Tests/GovernedMemoryContractTests.cs): shared roots, cleanup/baseline reuse, retained results and terminal refusal.
- [Representative scripts](Lokad.Lython.PublicApi.Tests/RepresentativeScriptTests.cs): complete CSV, text, JSON and file results.
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
