# Test assembly boundaries

- `Lokad.Lython.PublicApi.Tests` references `Lokad.Lython.csproj` normally. It
  verifies agent-facing behavior through the shipped public assembly and must
  not use runtime or frontend implementation types.
- `Lokad.Lython.Tests` is the isolated white-box suite. It source-links the
  production implementation so subsystem tests can inspect internal details
  without `InternalsVisibleTo` or a wider package API.

Behavior that can be expressed entirely through `LythonEngine`, `ILythonHost`,
and other public contracts belongs in the public-API suite. Implementation
invariants belong in the white-box suite.
