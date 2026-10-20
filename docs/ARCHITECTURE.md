# Architecture

Lython is a managed, host-mediated Python interpreter targeting .NET 10.
`src/Lokad.Lython` is the production core; its only runtime package dependency
is `Lokad.Parsing`. The optional `Lokad.Lython.Utf8Regex` assembly owns backend
adaptation. Unsafe compilation is disabled.

## Compilation

`LythonEngine.Compile` tokenizes/parses source, validates the subset, computes
binding/scope and host-requirement facts, lowers constructs, and builds executable
instructions and function metadata. Diagnostics cover invalid syntax, unsupported
features and conservative statically known errors; dynamic values remain runtime
concerns. Compilation performs no host I/O.

The disposable script retains executable code and metadata needed by diagnostics
and actual consumers. Code/caches use its borrowed memory owner. The engine's
regex provider handles constant validation and capture metadata analysis.

## Execution

The executable engine is the normal public path for both execution APIs and
local imports. Instructions use local/closure slots, operand storage, protected
cleanup and resumable generator state. Ordinary native operations take synchronous
paths until real work suspends; shared helpers preserve evaluation order,
exception state, borrows and cleanup across suspension.

Lowered evaluation remains for real class-body, annotation and expression/opcode
consumers. There is no raw-syntax statement backend or whole-program fallback.
Compiler defects are not silently retried through another interpreter.

Per-run services include host/capabilities, module/member caches, admission,
cancellation, stream capture and exception state. Lexical frames retain canonical
namespaces and closures. Imports use canonical module namespaces, not export copies.

Reusable startup workers provide the managed stack for the synchronous prefix.
Real asynchronous suspension releases the worker rather than parking a thread
through outstanding I/O. Managed nesting/recursion admission needs no native
stack telemetry.

## Values and boundaries

Strings use UTF-8 storage with Python character indexing. Containers retain
admitted backing capacities; integers preserve arbitrary precision. Runtime
operations distinguish exact native values from guest protocols and use type-based
special-method lookup where Python requires it. General calls preserve expansion,
binding, descriptors and aliases. Both APIs support genuine delayed callbacks.

Classification loops receive the active context/span and poll between small
UTF-8 chunks; identifiers decode UTF-8 directly. Bounded CLR operations keep
their documented checkpoint boundaries. Public projection detects cycles and
admits CLR-friendly output. Escaping values, metadata and frames keep their owners.

The host supplies authority, cancellation and any finite shared allowance.
Major allocations admit capacity/replacement overlap before publication; scopes
end after their users and cleanup. Admission refusal is terminal. There is no
execution fuel, host-call quota, independent storage cap, collector, forced GC
or regex time/work policy. See [runtime](RUNTIME.md) and [memory](MEMORY.md).

The Python-specific [regex boundary](REGEX.md) exposes UTF-8 data, effective flags,
capture metadata and operation-local tokens without backend types. Cached
patterns save no caller tokens; cursors/subjects have explicit cleanup.

## Verification

The white-box suite source-links production code for internal invariants; the
public suite references the built library. The Release package gate restores
isolated core-only/adapter consumers. Windows/Ubuntu checks and separately
selectable original Stress workloads qualify delivery. See [testing](TESTING.md).
