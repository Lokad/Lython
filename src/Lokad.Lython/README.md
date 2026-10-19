# Lokad.Lython

Lokad.Lython is the single production assembly for the rebuilt Lython runtime.

The project now implements a contained runtime for a strict subset of Python. The public surface includes:

- `LythonEngine` and `LythonCompiledScript`
- structured diagnostics and runtime failures
- the async `ILythonHost` abstraction for host-managed path operations
- `LythonRunOptions` for globals, cancellation, local import policy, and borrowed memory owners and managed recursion protection

Substantial compilation, execution, cache and result storage borrows the supplied host allowance. Without one, the standalone root is unbounded. There are no independent source/string/collection/read/output caps. Hosts control time with CancellationToken; managed stack admission and bounded I/O chunks remain. Builtin modules are allowlisted, and local script imports require explicit paths through `AllowedLocalModules`; paths are resolved relative to the importing script's `SourcePath` directory.

For the product direction, see `../../SPEC.md`.
