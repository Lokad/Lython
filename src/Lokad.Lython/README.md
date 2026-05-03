# Lokad.Lython

Lokad.Lython is the single production assembly for the rebuilt Lython runtime.

The project now implements a contained runtime for a strict subset of Python. The public surface includes:

- `LythonEngine` and `LythonCompiledScript`
- structured diagnostics and runtime failures
- the async `ILythonHost` abstraction for host-managed path operations
- `LythonRunOptions` for globals, cancellation, local import policy, and practical execution limits

Default execution limits are enabled unless `DisableDefaultLimits` is set, including 1 GiB execution and projection memory budgets, bounded host text reads, and bounded stdout/stderr capture. Builtin modules are allowlisted, and local script imports require explicit paths through `AllowedLocalModules`; paths are resolved relative to the importing script's `SourcePath` directory.

For the product direction, see `../../SPEC.md`.
