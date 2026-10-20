# Testing

Use focused Release regressions during development. Build the affected project
once, then run its class-qualified selection without overlapping builds/tests:

```powershell
dotnet build tests/Lokad.Lython.PublicApi.Tests -c Release
dotnet test tests/Lokad.Lython.PublicApi.Tests -c Release --no-build --filter 'Category!=Stress&FullyQualifiedName~Lokad.Lython.PublicApi.Tests.GovernedMemoryContractTests'
```

The public suite references the built library; the white-box suite source-links
production code for internal invariants. Publicly observable behavior belongs
in public tests. The worker fixture builds with that suite.

Ordinary tests exclude `Category=Stress`. An explicit filter replaces the default;
include `Category!=Stress` for broad selections. Keep minimal ordinary fixtures
and original longer stress scales. Never relax timeouts, containment assertions
or semantics for faster passes. Process-wide GC/finalizer tests share the existing
nonparallel collections: white-box `AllocationSensitive`, public `RuntimeStress`.
Complete compilation before suspension/cancellation checks.

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

- [Syntax corpus](../tests/Lokad.Lython.PublicApi.Tests/Fixtures/SyntaxCorpus/cases.json): independent goldens, invalid/unsupported forms and both APIs.
- [Shared memory](../tests/Lokad.Lython.PublicApi.Tests/GovernedMemoryContractTests.cs): shared roots, cleanup/baseline reuse, retained results and terminal refusal.
- [Representative scripts](../tests/Lokad.Lython.PublicApi.Tests/RepresentativeScriptTests.cs): complete CSV, text, JSON and file results.
- [Regex boundary](../tests/Lokad.Lython.PublicApi.Tests/RegexProviderContractTests.cs): cooperating in-flight cancellation/ownership, separate from backend qualification.
- [Text predicates](../tests/Lokad.Lython.PublicApi.Tests/TextPredicateExecutionTests.cs): Unicode and split-scalar results after real I/O, alongside internal forwarding/active-scan controls without checkpoint counting.

Preserve before-mutation admission, aliases/cursors, real suspension, terminal
refusal and awaited cleanup. Reference deletion does not imply immediate refund.
Large diagnostics and performance observations belong with benchmark evidence,
not ordinary timing assertions.

The [probe](../tools/LythonProbe/README.md) runs trusted pure snippets and optional
isolated CPython comparison without I/O/process capabilities. Test-project builds
do not build it; build its project in the matching configuration before dependent
tests, or build the solution.
