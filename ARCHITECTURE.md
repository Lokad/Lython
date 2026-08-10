# Lython Architecture

This document is a maintainer map of Lython. The user-facing compatibility
contract remains in [SPEC.md](SPEC.md); this file explains where that contract is
implemented and which boundaries a change must preserve.

## Design Constraints

Lython is a Python-shaped runtime, not a CLR scripting escape hatch. Supported
behavior follows Python semantics, while unsupported behavior is rejected
explicitly. Scripts receive no ambient filesystem, process, standard-stream,
clock, entropy, or import authority. Those effects exist only when the embedder
supplies the corresponding public host capability.

Three invariants dominate the implementation:

1. Compilation is effect-free. Host-dependent checks may inspect capability
   availability immediately before execution, but they do not invoke the host.
2. Every runtime effect crosses an `ILythonHost`-derived boundary, is charged to
   the execution budget, and translates host failures to stable Python-shaped
   failures.
3. Static analysis rejects only provable errors. Unknown or data-dependent
   behavior remains a runtime concern.

## Compilation Pipeline

`LythonEngine.Compile` drives these stages:

```text
source
  -> normalization and lexical analysis
  -> Parser / syntax tree
  -> annotation and static-analysis passes
  -> LoweredScript
  -> ExecutableScript when executable lowering supports the program
```

The parser and syntax model live under `Frontend/`. `LythonFrontend` normalizes
line endings, lexes, parses, and combines diagnostics. `StaticAnalyzer` runs
separate passes for scope directives, abstract values, regex contracts, and name
binding. Host-requirement analysis runs before execution because only then is a
concrete host available.

Frontend passes that need syntax recursion should use
`ExpressionSyntaxTraversal` and `StatementSyntaxTraversal`. Comprehension
iterables and eager result expressions are traversed in the current execution
scope; lambda and generator bodies are deferred. A new syntax node must update
the shared traversal and its coverage tests before pass-specific behavior is
added.

`AbstractValue` is a closed analysis value: its factories establish the payload
required by each `AbstractValueKind`, and `RequirePayload<T>` is the checked
access path. `AbstractValue.Join` computes conservative control-flow merges.
Protected `try` bodies suppress abstract runtime diagnostics when an `except`
body can catch them, but their successful-path facts still flow into `else` and
the post-`try` merge.

## Lowering And Execution

`LoweredScript` is the complete, general intermediate representation. Both sync
and async execution support it. `ExecutableScript` is an optional, block-based
representation for the synchronous fast path. Unsupported executable lowering
falls back to `LoweredScript`; it is not a compilation error.

The executable path has two layers:

- `LythonRuntime.Executable.cs` creates locals and closure cells, establishes the
  interpreter frame, and guarantees frame teardown.
- `ExecutableFrameInterpreter` owns the operand stack, inline caches, block
  entry depths, and pending abrupt-control state for one invocation. Its opcode
  handlers are grouped by definitions, stack transfers, structures, value
  operations, and control flow.

Every executable control-flow edge must enter a block at one consistent stack
depth. Exceptions and return/break/continue signals remain explicit while a
`finally` block runs, then propagate when that block completes. Changes to
lowered semantics should be tested against both the general and executable paths
whenever the opcode path can represent the construct.

`ExecutionContext` owns per-run services and Python-visible state: globals,
locals, closure resolution, exception state, module cache, decimal context,
host handles, and limits. Values entering from public options are normalized to
Lython's governed runtime values before script code observes them.

## Runtime Values And Protocols

Python-shaped values live under `Runtime/`, with text and numeric primitives in
their corresponding subdirectories. Cross-cutting behavior is expressed through
small protocols such as callability, truthiness, indexing, rendering, and member
access.

Dynamic attributes deliberately distinguish reading from mutation:

- `IPyDynamicAttributes` provides lookup.
- `IPyMutableDynamicAttributes` additionally provides assignment.

A value must implement the mutable protocol only when assignment can actually
succeed. Unsupported mutation should raise a Python-shaped error at the member
boundary, not advertise a setter that always returns `false`.

Async iteration uses `PyIterationResult`, whose factories make yielded and ended
states explicit. Consumers must test `HasValue` before reading `Value`; the
default struct value is the ended state.

## Host Boundary And Containment

`ILythonHost` is the authority root. Optional interfaces add bounded binary I/O,
subprocesses, timing, and standard streams. Module implementations must not use
ambient filesystem APIs, process APIs, environment variables, clocks, delays,
or entropy as fallbacks.

Before a host operation, runtime code registers the call with the execution
budget. Synchronous entry points reject operations that require unavailable
synchronous behavior before preceding side effects can occur. Sync and async
host-await helpers share exception translation: Lython exceptions and
`OutOfMemoryException` retain their identity, cancellation and output-limit
failures keep their dedicated categories, binary-I/O failures retain their
contract, and ordinary host exceptions become stable `RuntimeError` failures.

Runtime allocations that can scale with script input must use the execution
`MemoryGovernor`. Prefer single-pass builders and sized buffers; do not create an
ungoverned proportional intermediate merely to populate a governed value.

Path behavior is split between lexical providers and cohesive host-backed
providers. Lexical operations never call the host. Status, mutation, unsupported
host-boundary members, and content/enumeration members are independently
dispatched so authority-bearing behavior remains reviewable.

## Module Organization

`LythonRuntime` is a partial runtime facade. Its files are responsibility slices,
not independent services: builtin/module registration, individual standard
library modules, execution, calls, member access, and host adapters. Add behavior
to the narrowest existing slice. When a switch or state machine grows beyond one
reviewable responsibility, extract a named component or provider rather than a
generic utility class.

Avoid duplicating Python semantics across paths. Shared rules belong in focused
operations or traversal components; sync and async loops may remain distinct
when their control flow makes awaiting and side-effect order clearer. Private
logic used at only one call site should normally be a local function.

## Verification Boundaries

`tests/Lokad.Lython.Tests` is the white-box subsystem suite. It compiles production
sources into the test assembly so internals remain internal without a friend
assembly. `tests/Lokad.Lython.PublicApi.Tests` references the built library and
guards the real consumer boundary. `tools/LythonProbe` provides public-API
compatibility probes and optional comparison with trusted local CPython.

Before committing a behavior change:

1. Add focused tests for success, Python-shaped failure, and sync/async parity
   where relevant.
2. Test the public boundary when public contracts, projection, or host behavior
   changes.
3. Run the complete solution tests with warnings treated as errors.
4. Keep package/version changes in a separate, explicit release commit.

